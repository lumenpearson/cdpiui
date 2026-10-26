namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Compares the executable names of import targets: no path, no quotes, no ".exe".
/// </summary>
internal static class ConfigImportExecutables
{
    public static string Normalize(string executable)
    {
        string name = Path.GetFileName(executable.Trim().Trim('"'));
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    public static bool AreEqual(string left, string right) =>
        Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsZapret(string executable) =>
        executable.Equals("winws", StringComparison.OrdinalIgnoreCase) ||
        executable.Equals("winws2", StringComparison.OrdinalIgnoreCase);

    public static string GetZapretExecutable(ZapretConfigGeneration generation) =>
        generation == ZapretConfigGeneration.Winws2 ? "winws2" : "winws";
}
