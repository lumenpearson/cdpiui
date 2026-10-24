using System.Text;
using CDPIUI.Shared.PrettyErrorConversionService;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Reads the output pipe of a pseudo console, remembers the error the component reports and
/// passes each decoded chunk, with the line prefix, to a callback.
/// </summary>
internal sealed class ConPTYOutputReader
{
    private readonly string _prefix;
    private readonly Action<string> _outputRead;

    public ConPTYOutputReader(string prefix, Action<string> outputRead)
    {
        _prefix = prefix;
        _outputRead = outputRead;
    }

    /// <summary>
    /// The error found in the output read so far; also valid after the read was canceled.
    /// </summary>
    public PrettyErrorCode LastError { get; private set; } = PrettyErrorCode.SUCCESS;

    public async Task ReadAsync(SafeFileHandle outputHandle, CancellationToken token)
    {
        using var reader = new FileStream(outputHandle, FileAccess.Read);
        byte[] buffer = new byte[4096];
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        // Keeps a multi-byte character that is split between two reads.
        Decoder decoder = Encoding.UTF8.GetDecoder();
        int bytesRead;

        while (!token.IsCancellationRequested)
        {
            bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: token);
            // The pipe was closed: reading again returns 0 at once and spun the loop.
            if (bytesRead == 0)
                break;

            string output = new string(chars, 0, decoder.GetChars(buffer, 0, bytesRead, chars, 0));
            LastError = ConPTYErrorClassifier.Merge(LastError, output);
            _outputRead(AddPrefix(_prefix, output));
        }
    }

    internal static string AddPrefix(string prefix, string output)
    {
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(output))
            return output;

        string newOutput = string.Empty;
        foreach (string str in output.Split("\n"))
        {
            newOutput += $"[{prefix}] {str}\n";
        }
        return newOutput;
    }
}
