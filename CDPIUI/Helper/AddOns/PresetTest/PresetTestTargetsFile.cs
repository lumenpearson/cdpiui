using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// The test_targets.txt file with the endpoints the standard preset test checks.
/// </summary>
internal static class PresetTestTargetsFile
{
    public static string GetPath()
    {
        return Path.Combine(Directories.SettingsDirectory, "test_targets.txt");
    }

    public static void EnsureExists()
    {
        string path = GetPath();
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
                File.WriteAllText(path, GetDefaultContent(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't create targets file: {ex.Message}");
        }
    }

    public static List<PresetTestTarget> Load()
    {
        EnsureExists();
        var targets = new List<PresetTestTarget>();
        try
        {
            foreach (var rawLine in File.ReadAllLines(GetPath()))
            {
                PresetTestTarget target = ParseLine(rawLine);
                if (target != null)
                    targets.Add(target);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't read targets file: {ex.Message}");
        }
        return targets;
    }

    // The default endpoints ship as Template\PresetTest\test_targets.txt, so they can change
    // without a change of the code. Without the template, the file gets only its format help.
    private static string GetDefaultContent()
    {
        string template = Path.Combine(Directories.TemplatesDirectory, "PresetTest", "test_targets.txt");
        try
        {
            if (File.Exists(template))
                return File.ReadAllText(template, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't read the default targets file: {ex.Message}");
        }
        return
            "# test_targets.txt - endpoints checked for each preset (standard mode)" + Environment.NewLine +
            "# Format:  Name = \"https://host\"  (HTTPS+ping)  or  Name = \"PING:1.2.3.4\"  (ping only)" + Environment.NewLine;
    }

    private static PresetTestTarget ParseLine(string rawLine)
    {
        string line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#"))
            return null;

        int eq = line.IndexOf('=');
        if (eq < 0)
            return null;

        string name = line.Substring(0, eq).Trim();
        string value = line.Substring(eq + 1).Trim().Trim('"').Trim();

        if (name.Length == 0 || value.Length == 0)
            return null;

        if (value.StartsWith("PING:", StringComparison.OrdinalIgnoreCase))
            return new PresetTestTarget { Name = name, PingHost = value.Substring("PING:".Length).Trim() };

        // The value goes to curl as an argument, so only an absolute http or https URL is accepted:
        // a value that starts with "-" would be read as a curl option.
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Skipped target {name}: not an http or https URL");
            return null;
        }
        return new PresetTestTarget { Name = name, Url = value, PingHost = uri.Host };
    }
}
