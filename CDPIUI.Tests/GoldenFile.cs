namespace CDPIUI.Tests;

/// <summary>
/// Expected results stored in TestData. A line "[name]" starts a section, the lines after it
/// are the values of the section. Empty lines and lines that start with "#" are skipped.
/// </summary>
internal sealed class GoldenFile
{
    private readonly Dictionary<string, List<string>> _sections = new(StringComparer.OrdinalIgnoreCase);

    private GoldenFile() { }

    public static string GetTestDataPath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "TestData", .. parts]);

    public static GoldenFile Load(params string[] parts)
    {
        var file = new GoldenFile();
        List<string>? current = null;
        foreach (string rawLine in File.ReadAllLines(GetTestDataPath(parts)))
        {
            string line = rawLine.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = [];
                file._sections[line[1..^1]] = current;
                continue;
            }

            if (current == null)
                throw new InvalidDataException($"The line '{line}' is outside a section.");
            current.Add(line);
        }
        return file;
    }

    public IReadOnlyList<string> Lines(string section) =>
        _sections.TryGetValue(section, out List<string>? lines) ? lines : [];

    public string Single(string section) => Assert.Single(Lines(section));

    /// <summary>
    /// A long command line is stored one section per line. The lines are joined with a space.
    /// </summary>
    public string CommandLine(string section) => string.Join(" ", Lines(section));
}
