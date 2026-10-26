using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using WinUI3Localizer;

namespace CDPIUI.Helper.CreateConfigHelper;

// Builds the localized description of one zapret2 option for the preset structure tree.
internal sealed class Zapret2PresetOptionDescriber(ILocalizer localizer)
{
    private readonly ILocalizer _localizer = localizer;

    public string Describe(
        ConfigCommandOption option,
        IEnumerable<ComponentCommandHelpOption> helpOptions)
    {
        List<string> descriptions = [];
        string resourceKey = Zapret2OptionDescriptionProvider.GetOptionResourceKey(option.Name);
        string contextualDescription = GetContextualDescription(option);
        if (!string.IsNullOrWhiteSpace(contextualDescription))
        {
            descriptions.Add(contextualDescription);
        }
        else if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            descriptions.Add(_localizer.GetLocalizedString(resourceKey));
            string value = GetReadableOptionValue(option.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                descriptions.Add(string.Format(
                    _localizer.GetLocalizedString("ConfigMakerZapret2CurrentValueDescription"),
                    value));
            }
        }
        if (string.Equals(option.Name, "--lua-desync", StringComparison.OrdinalIgnoreCase))
        {
            string functionResourceKey = Zapret2OptionDescriptionProvider
                .GetLuaFunctionResourceKey(option.Value);
            if (!string.IsNullOrWhiteSpace(functionResourceKey))
            {
                descriptions.Add(_localizer.GetLocalizedString(functionResourceKey));
            }
        }

        if (descriptions.Count == 0)
        {
            string helpDescription = helpOptions
                .FirstOrDefault(item => item.Matches(option.Name))
                ?.Description
                ?.Trim()
                .TrimStart(';')
                .Trim();
            descriptions.Add(string.IsNullOrWhiteSpace(helpDescription)
                ? _localizer.GetLocalizedString("ConfigMakerZapret2NoFlagDescription")
                : helpDescription);
        }
        return string.Join(' ', descriptions.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private string GetContextualDescription(ConfigCommandOption option)
    {
        string value = GetReadableOptionValue(option.Value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string optionName = option.Name.ToLowerInvariant();
        return optionName switch
        {
            "--filter-l3" => string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2FilterL3SpecificDescription"),
                NormalizeIpVersion(value)),
            "--wf-l3" => string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2WinDivertL3SpecificDescription"),
                NormalizeIpVersion(value)),
            "--filter-tcp" => FormatTransportFilterDescription("TCP", value),
            "--filter-udp" => FormatTransportFilterDescription("UDP", value),
            "--filter-l7" => string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2FilterL7SpecificDescription"),
                FormatListValue(value)),
            "--hostlist" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListFileSpecificDescription",
                value),
            "--hostlist-domains" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListDomainsSpecificDescription",
                FormatListValue(value)),
            "--hostlist-exclude" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListExcludeFileSpecificDescription",
                value),
            "--hostlist-exclude-domains" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListExcludeDomainsSpecificDescription",
                FormatListValue(value)),
            "--hostlist-auto" => FormatSpecificDescription(
                "ConfigMakerZapret2HostListAutoSpecificDescription",
                value),
            "--ipset" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetFileSpecificDescription",
                value),
            "--ipset-ip" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetInlineSpecificDescription",
                FormatListValue(value)),
            "--ipset-exclude" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetExcludeFileSpecificDescription",
                value),
            "--ipset-exclude-ip" => FormatSpecificDescription(
                "ConfigMakerZapret2IpSetExcludeInlineSpecificDescription",
                FormatListValue(value)),
            "--payload" => FormatSpecificDescription(
                "ConfigMakerZapret2PayloadSpecificDescription",
                FormatListValue(value)),
            "--out-range" => FormatRangeDescription(value, isOutgoing: true),
            "--in-range" => FormatRangeDescription(value, isOutgoing: false),
            "--name" => FormatSpecificDescription(
                "ConfigMakerZapret2ProfileNameSpecificDescription",
                value),
            "--lua-init" => FormatSpecificDescription(
                "ConfigMakerZapret2LuaInitSpecificDescription",
                value.TrimStart('@', '$')),
            "--blob" => FormatBlobDescription(value),
            "--lua-desync" => FormatLuaInvocationDescription(value),
            "--wf-iface" => FormatSpecificDescription(
                "ConfigMakerZapret2WinDivertInterfaceSpecificDescription",
                value),
            "--ssid-filter" => FormatSpecificDescription(
                "ConfigMakerZapret2SsidFilterSpecificDescription",
                FormatListValue(value)),
            "--nlm-filter" => FormatSpecificDescription(
                "ConfigMakerZapret2NlmFilterSpecificDescription",
                FormatListValue(value)),
            _ => string.Empty,
        };
    }

    private string FormatTransportFilterDescription(string protocol, string value)
    {
        bool excludes = value.StartsWith('~');
        string ports = FormatListValue(excludes ? value[1..] : value);
        if (!excludes && ports == "*")
        {
            return string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2TransportFilterAllSpecificDescription"),
                protocol);
        }
        return string.Format(
            _localizer.GetLocalizedString(excludes
                ? "ConfigMakerZapret2TransportFilterExcludedSpecificDescription"
                : "ConfigMakerZapret2TransportFilterIncludedSpecificDescription"),
            protocol,
            ports);
    }

    private string FormatBlobDescription(string value)
    {
        int separator = value.IndexOf(':');
        if (separator <= 0 || separator >= value.Length - 1)
        {
            return string.Empty;
        }
        string blobName = value[..separator];
        string source = value[(separator + 1)..].TrimStart('@', '$');
        return string.Format(
            _localizer.GetLocalizedString("ConfigMakerZapret2BlobSpecificDescription"),
            source,
            blobName);
    }

    private string FormatLuaInvocationDescription(string value)
    {
        string[] parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        string functionName = parts.FirstOrDefault() ?? value;
        return parts.Length > 1
            ? string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2LuaInvocationWithArgumentsDescription"),
                functionName,
                string.Join(", ", parts.Skip(1)))
            : string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2LuaInvocationDescription"),
                functionName);
    }

    private string FormatRangeDescription(string value, bool isOutgoing)
    {
        string direction = _localizer.GetLocalizedString(isOutgoing
            ? "ConfigMakerZapret2RangeOutgoingDirection"
            : "ConfigMakerZapret2RangeIncomingDirection");
        if (string.Equals(value, "a", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeAlwaysDescription"),
                direction);
        }
        if (string.Equals(value, "x", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeNeverDescription"),
                direction);
        }
        if (!Zapret2Range.TryParse(value, out Zapret2Range range))
        {
            return FormatGenericRangeDescription(value, isOutgoing);
        }

        if (TryFormatPacketRange(range, direction, out string packetDescription))
        {
            return packetDescription;
        }

        List<string> conditions = [];
        if (range.Lower is Zapret2RangeBoundary lower)
        {
            conditions.Add(string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeLowerCondition"),
                GetRangeCounterDescription(lower.Mode),
                lower.Value));
        }
        if (range.Upper is Zapret2RangeBoundary upper)
        {
            conditions.Add(string.Format(
                _localizer.GetLocalizedString(range.UpperExclusive
                    ? "ConfigMakerZapret2RangeUpperExclusiveCondition"
                    : "ConfigMakerZapret2RangeUpperInclusiveCondition"),
                GetRangeCounterDescription(upper.Mode),
                upper.Value));
        }
        if (conditions.Count == 0 || conditions.Any(string.IsNullOrWhiteSpace))
        {
            return FormatGenericRangeDescription(value, isOutgoing);
        }
        return string.Format(
            _localizer.GetLocalizedString("ConfigMakerZapret2RangeConditionsDescription"),
            direction,
            string.Join(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeConditionSeparator"),
                conditions));
    }

    private string FormatGenericRangeDescription(string value, bool isOutgoing) =>
        FormatSpecificDescription(
            isOutgoing
                ? "ConfigMakerZapret2OutRangeSpecificDescription"
                : "ConfigMakerZapret2InRangeSpecificDescription",
            value);

    private bool TryFormatPacketRange(
        Zapret2Range range,
        string direction,
        out string description)
    {
        description = string.Empty;
        char mode = range.Lower?.Mode ?? range.Upper?.Mode ?? '\0';
        if (mode is not ('n' or 'd') ||
            (range.Lower is Zapret2RangeBoundary lower && lower.Mode != mode) ||
            (range.Upper is Zapret2RangeBoundary upper && upper.Mode != mode))
        {
            return false;
        }

        string singularUnit = _localizer.GetLocalizedString(mode == 'n'
            ? "ConfigMakerZapret2RangePacketSingular"
            : "ConfigMakerZapret2RangeDataPacketSingular");
        string pluralUnit = _localizer.GetLocalizedString(mode == 'n'
            ? "ConfigMakerZapret2RangePacketPlural"
            : "ConfigMakerZapret2RangeDataPacketPlural");
        long? first = range.Lower?.Value;
        long? last = range.Upper?.Value;
        if (last.HasValue && range.UpperExclusive)
        {
            last--;
        }
        if (last < 1 || first > last)
        {
            description = string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeNeverDescription"),
                direction);
            return true;
        }
        if (!first.HasValue && last == 1)
        {
            description = string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeFirstPacketDescription"),
                singularUnit,
                direction);
            return true;
        }
        if (!first.HasValue && last.HasValue)
        {
            description = string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeFirstPacketsDescription"),
                last.Value,
                pluralUnit,
                direction);
            return true;
        }
        if (first.HasValue && !last.HasValue)
        {
            description = string.Format(
                _localizer.GetLocalizedString("ConfigMakerZapret2RangeFromPacketDescription"),
                singularUnit,
                first.Value,
                direction);
            return true;
        }

        description = string.Format(
            _localizer.GetLocalizedString("ConfigMakerZapret2RangePacketIntervalDescription"),
            pluralUnit,
            first,
            last,
            direction);
        return true;
    }

    private string GetRangeCounterDescription(char mode) =>
        _localizer.GetLocalizedString(mode switch
        {
            'b' => "ConfigMakerZapret2RangeByteCounter",
            's' => "ConfigMakerZapret2RangeSequenceStartCounter",
            'p' => "ConfigMakerZapret2RangeSequenceEndCounter",
            'n' => "ConfigMakerZapret2RangePacketNumberCounter",
            'd' => "ConfigMakerZapret2RangeDataPacketNumberCounter",
            _ => "ConfigMakerZapret2RangeUnknownCounter",
        });

    private string FormatSpecificDescription(string resourceKey, string value) => string.Format(
        _localizer.GetLocalizedString(resourceKey),
        value);

    private static string GetReadableOptionValue(string value) =>
        ConfigCommandLine.Unquote(value).Trim();

    private static string NormalizeIpVersion(string value) => value.ToLowerInvariant() switch
    {
        "ipv4" => "IPv4",
        "ipv6" => "IPv6",
        _ => value,
    };

    private static string FormatListValue(string value) =>
        string.Join(", ", value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
