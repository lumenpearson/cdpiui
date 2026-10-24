namespace CDPIUI.Core.Basic;

/// <summary>
/// A small INI file model for config.ini of GoodCheck. It keeps comments, blank lines and the
/// order of keys, and changes only the lines of the values that are set.
/// </summary>
public sealed class IniDocument
{
    private sealed class Line
    {
        public string Text = string.Empty;
        public string? Section;
        public string? Key;
    }

    private readonly List<Line> _lines = [];

    public static IniDocument Parse(string text)
    {
        var document = new IniDocument();
        string? section = null;
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = new Line { Text = raw };
            string trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                line.Section = section;
            }
            else if (section != null && !IsComment(trimmed) && trimmed.Contains('='))
            {
                line.Section = section;
                line.Key = trimmed[..trimmed.IndexOf('=')].Trim();
            }
            document._lines.Add(line);
        }
        // Split gives an empty last element for a file that ends with a line break.
        if (document._lines.Count > 0 && document._lines[^1].Text.Length == 0)
            document._lines.RemoveAt(document._lines.Count - 1);
        return document;
    }

    private static bool IsComment(string trimmed) =>
        trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith(';') || trimmed.StartsWith('#');

    private Line? Find(string section, string key) =>
        _lines.FirstOrDefault(line => line.Key != null &&
            string.Equals(line.Section, section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(line.Key, key, StringComparison.OrdinalIgnoreCase));

    public bool TryGetValue(string section, string key, out string value)
    {
        Line? line = Find(section, key);
        value = line == null ? string.Empty : line.Text[(line.Text.IndexOf('=') + 1)..].Trim();
        return line != null;
    }

    public void SetValue(string section, string key, string value)
    {
        Line? existing = Find(section, key);
        if (existing != null)
        {
            existing.Text = $"{existing.Text[..existing.Text.IndexOf('=')].TrimEnd()}={value}";
            return;
        }

        var line = new Line { Text = $"{key}={value}", Section = section, Key = key };
        int header = _lines.FindIndex(item => item.Key == null &&
            string.Equals(item.Section, section, StringComparison.OrdinalIgnoreCase));
        if (header < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Text.Trim().Length > 0)
                _lines.Add(new Line());
            _lines.Add(new Line { Text = $"[{section}]", Section = section });
            _lines.Add(line);
            return;
        }

        // After the last key of the section, before the blank lines that separate it from the next one.
        int index = header + 1;
        for (int current = header + 1; current < _lines.Count; current++)
        {
            if (_lines[current].Key == null && _lines[current].Section != null)
                break;
            if (_lines[current].Text.Trim().Length > 0)
                index = current + 1;
        }
        _lines.Insert(index, line);
    }

    public override string ToString() =>
        string.Concat(_lines.Select(line => line.Text + Environment.NewLine));
}
