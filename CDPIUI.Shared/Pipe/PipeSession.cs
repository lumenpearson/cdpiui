using System;
using System.Security.Cryptography;

namespace CDPIUI.Shared.Pipe;

/// <summary>
/// The pipe name and the pipe key of one tray start. A fixed global name let any program create
/// the pipe before the tray did; a random name is known only to the readers of the session file.
/// </summary>
public sealed record PipeSession(string PipeName, string Key)
{
    public const string PipeNamePrefix = "CDPIUI_";

    // 128 bits for the name and 256 bits for the key, both as upper-case hexadecimal.
    public const int PipeNameRandomLength = 32;
    public const int KeyLength = 64;

    public static string CreatePipeName() => PipeNamePrefix + CreateRandomHex(PipeNameRandomLength / 2);

    public static string CreateKey() => CreateRandomHex(KeyLength / 2);

    public static bool IsValidPipeName(string? value) =>
        value != null &&
        value.StartsWith(PipeNamePrefix, StringComparison.Ordinal) &&
        IsUpperHex(value.Substring(PipeNamePrefix.Length), PipeNameRandomLength);

    public static bool IsValidKey(string? value) => value != null && IsUpperHex(value, KeyLength);

    // Only the readers of the session file may know the values, so they must not end up in log files.
    public override string ToString() => nameof(PipeSession);

    private static string CreateRandomHex(int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    private static bool IsUpperHex(string value, int length)
    {
        if (value.Length != length)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9' or >= 'A' and <= 'F'))
                return false;
        }
        return true;
    }
}
