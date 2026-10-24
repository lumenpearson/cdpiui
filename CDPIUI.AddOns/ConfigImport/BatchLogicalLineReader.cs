using System.Text;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Reads a batch or text file as cmd sees it: a line that ends with an unescaped caret
/// continues on the next physical line.
/// </summary>
internal static class BatchLogicalLineReader
{
    public static IReadOnlyList<BatchLogicalLine> Read(string filePath)
    {
        string[] physicalLines = CDPIUI.Core.System.BatchFileEncoding.ReadAllLines(filePath);
        var result = new List<BatchLogicalLine>();
        var current = new StringBuilder();
        int currentLineNumber = 0;

        for (int index = 0; index < physicalLines.Length; index++)
        {
            string physicalLine = physicalLines[index].TrimEnd();
            if (current.Length == 0)
                currentLineNumber = index + 1;

            bool continues = HasContinuationCaret(physicalLine);
            if (continues)
                physicalLine = physicalLine[..^1];

            if (current.Length > 0)
                current.Append(' ');
            current.Append(physicalLine.Trim());

            if (!continues)
            {
                result.Add(new BatchLogicalLine(currentLineNumber, current.ToString()));
                current.Clear();
            }
        }

        if (current.Length > 0)
            result.Add(new BatchLogicalLine(currentLineNumber, current.ToString()));

        return result;
    }

    private static bool HasContinuationCaret(string line)
    {
        int caretCount = 0;
        for (int index = line.Length - 1; index >= 0 && line[index] == '^'; index--)
            caretCount++;
        return caretCount % 2 == 1;
    }
}
