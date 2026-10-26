using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret1OptionCatalog;
using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret1OptionValues;
using static CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters.Zapret2ArgumentFormatter;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Converts an already expanded winws/nfqws1 command line to a winws2 command line.
/// Converts only command line - config files conversion isn't supported
/// </summary>
public sealed class Zapret1ToZapret2Converter
{
    private readonly ZapretConversionOptions _options;
    private readonly ZapretConversionContext _context;
    private readonly Zapret1ActionArgsBuilder _actionArgs;
    private readonly Zapret1DesyncModeConverter _modeConverter;

    public Zapret1ToZapret2Converter(ZapretConversionOptions? options = null)
    {
        _options = options ?? new ZapretConversionOptions();
        _context = new ZapretConversionContext(_options);
        _actionArgs = new Zapret1ActionArgsBuilder(_context);
        _modeConverter = new Zapret1DesyncModeConverter(_context);
    }

    public ZapretConversionResult Convert(string startupString)
    {
        _context.Reset();

        if (string.IsNullOrWhiteSpace(startupString))
        {
            _context.AddError("EMPTY_COMMAND_LINE", "The Zapret1 startup string is empty.");
            return _context.CreateResult(string.Empty);
        }

        List<Zapret1CommandOption> parsed = Zapret1CommandLineParser.Parse(startupString, _context);
        SplitOptions(parsed, out List<Zapret1CommandOption> globals, out List<Zapret1Profile> profiles);

        List<string> convertedProfiles = [];
        foreach (Zapret1Profile profile in profiles)
        {
            convertedProfiles.Add(ConvertProfile(profile));
        }

        List<string> output = [];
        foreach (Zapret1CommandOption option in globals)
        {
            output.Add(ConvertGlobalOption(option));
        }

        foreach (string blob in _context.GetBlobOptionValues())
        {
            output.Add(FormatOption("blob", blob));
        }

        output.Add(FormatOption("lua-init", $"@{_options.ZapretLibraryPath}"));
        output.Add(FormatOption("lua-init", $"@{_options.ZapretAntiDpiLibraryPath}"));
        output.Add(FormatOption("lua-init", CompatibilityLua));

        for (int i = 0; i < convertedProfiles.Count; i++)
        {
            if (i > 0)
            {
                output.Add("--new");
            }

            if (!string.IsNullOrWhiteSpace(convertedProfiles[i]))
            {
                output.Add(convertedProfiles[i]);
            }
        }

        return _context.CreateResult(string.Join(' ', output.Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    private static void SplitOptions(
        List<Zapret1CommandOption> parsed,
        out List<Zapret1CommandOption> globals,
        out List<Zapret1Profile> profiles)
    {
        globals = [];
        profiles = [new Zapret1Profile(1)];

        foreach (Zapret1CommandOption option in parsed)
        {
            if (option.Name.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                profiles.Add(new Zapret1Profile(profiles.Count + 1));
                continue;
            }

            if (GlobalOptions.Contains(option.Name))
            {
                globals.Add(option);
                continue;
            }

            profiles[^1].Options.Add(option);
        }
    }

    private string ConvertGlobalOption(Zapret1CommandOption option)
    {
        string name = option.Name.ToLowerInvariant() switch
        {
            "wf-tcp" => "wf-tcp-out",
            "wf-udp" => "wf-udp-out",
            _ => option.Name
        };

        string? value = option.Value;
        if (value != null)
        {
            if (name.Equals("debug", StringComparison.OrdinalIgnoreCase))
            {
                value = _context.MapDirectFileValue(
                    name,
                    value,
                    ZapretConversionFileAccess.Write,
                    onlyWhenAtPrefixed: true);
            }
            else if (name.Equals("wf-raw-part", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("wf-raw-filter", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("wf-raw", StringComparison.OrdinalIgnoreCase))
            {
                value = _context.MapDirectFileValue(
                    name,
                    value,
                    ZapretConversionFileAccess.Read,
                    onlyWhenAtPrefixed: true);
            }
            else if (name.Equals("wf-save", StringComparison.OrdinalIgnoreCase))
            {
                value = _context.MapDirectFileValue(name, value, ZapretConversionFileAccess.Write);
            }
        }

        return FormatOption(name, value);
    }

    private string ConvertProfile(Zapret1Profile profile)
    {
        List<string> output = [];
        Dictionary<string, List<string?>> legacy = CollectProfileOptions(profile, output);

        _actionArgs.AppendDuplicateAction(output, legacy, profile.Index);

        string? strategyValue = LastValue(legacy, "dpi-desync");
        if (string.IsNullOrWhiteSpace(strategyValue))
        {
            return string.Join(' ', output);
        }

        string[] modes = strategyValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(mode => LegacyModeAliases.TryGetValue(mode, out string? current) ? current : mode)
            .ToArray();

        if (modes.Length == 0)
        {
            _context.AddError(
                "EMPTY_STRATEGY",
                "The '--dpi-desync' option does not contain a strategy.",
                "dpi-desync",
                profile.Index);
            return string.Join(' ', output);
        }

        AppendStrategy(output, modes, legacy, profile.Index);
        return string.Join(' ', output);
    }

    /// <summary>
    /// Writes the filter options of the profile to <paramref name="output"/> and returns the
    /// desync options, which are converted together.
    /// </summary>
    private Dictionary<string, List<string?>> CollectProfileOptions(Zapret1Profile profile, List<string> output)
    {
        Dictionary<string, List<string?>> legacy = new(StringComparer.OrdinalIgnoreCase);

        foreach (Zapret1CommandOption option in profile.Options)
        {
            if (ProfileOptions.Contains(option.Name))
            {
                output.Add(FormatOption(option.Name, ConvertProfileOptionValue(option, profile.Index)));
                continue;
            }

            if (DroppedLegacyOptions.Contains(option.Name))
            {
                _context.AddWarning(
                    "DROPPED_OPTION",
                    $"Zapret1 option '--{option.Name}' has no Zapret2 counterpart and was left out.",
                    option.Name,
                    profile.Index);
                continue;
            }

            if (!SupportedLegacyOptions.Contains(option.Name))
            {
                _context.AddError(
                    "UNSUPPORTED_OPTION",
                    $"Zapret1 option '--{option.Name}' has no conversion rule.",
                    option.Name,
                    profile.Index);
                continue;
            }

            if (!legacy.TryGetValue(option.Name, out List<string?>? values))
            {
                values = [];
                legacy.Add(option.Name, values);
            }
            values.Add(option.Value);
        }

        return legacy;
    }

    private string? ConvertProfileOptionValue(Zapret1CommandOption option, int profileIndex)
    {
        string? value = option.Value;
        if (value != null && ProfileInputFileOptions.Contains(option.Name))
        {
            value = _context.MapDirectFileValue(
                option.Name,
                value,
                ZapretConversionFileAccess.Read);
        }
        else if (value != null && option.Name.Equals("hostlist-auto", StringComparison.OrdinalIgnoreCase))
        {
            value = _context.MapDirectFileValue(
                option.Name,
                value,
                ZapretConversionFileAccess.ReadWrite);
            _context.AddWarning(
                "HOSTLIST_AUTO_SEMANTICS_CHANGED",
                "Zapret2 does not route a connection through an auto-hostlist profile until its hostname is known.",
                option.Name,
                profileIndex);
        }
        else if (value != null && option.Name.Equals("hostlist-auto-debug", StringComparison.OrdinalIgnoreCase))
        {
            value = _context.MapDirectFileValue(
                option.Name,
                value,
                ZapretConversionFileAccess.Write);
        }

        return value;
    }

    private void AppendStrategy(
        List<string> output,
        string[] modes,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        string desyncRange = _actionArgs.BuildRange(
            LastValue(legacy, "dpi-desync-start"),
            LastValue(legacy, "dpi-desync-cutoff"),
            profileIndex,
            "dpi-desync");

        bool anyProtocol = IsEnabled(legacy, "dpi-desync-any-protocol");
        bool skipNoSni = !legacy.ContainsKey("dpi-desync-skip-nosni") ||
                         IsEnabled(legacy, "dpi-desync-skip-nosni");
        List<string> commonArgs = _actionArgs.BuildCommonActionArgs(legacy, "dpi-desync", profileIndex);

        foreach (string mode in modes.Where(mode => ZeroPhaseModes.Contains(mode)))
        {
            output.Add("--payload=all");
            output.Add(FormatOption("out-range", desyncRange));
            _modeConverter.AppendMode(output, mode, legacy, commonArgs, anyProtocol, skipNoSni, profileIndex);
        }

        string[] dataModes = modes.Where(mode => !ZeroPhaseModes.Contains(mode)).ToArray();
        if (dataModes.Length > 0)
        {
            output.Add(FormatOption("out-range", desyncRange));
            output.Add(anyProtocol ? "--payload=all" : "--payload=known");

            foreach (string mode in dataModes)
            {
                _modeConverter.AppendMode(output, mode, legacy, commonArgs, anyProtocol, skipNoSni, profileIndex);
            }
        }
    }
}
