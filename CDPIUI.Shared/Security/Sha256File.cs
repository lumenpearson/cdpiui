using System;
using System.IO;
using System.Security.Cryptography;

namespace CDPIUI.Shared.Security;

/// <summary>Checks a file against an expected SHA-256 digest.</summary>
public static class Sha256File
{
    public static string Compute(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
    }

    public static bool Matches(string path, string? expectedSha256)
    {
        if (expectedSha256 == null || expectedSha256.Length != 64)
            return false;
        foreach (char character in expectedSha256)
        {
            if (!Uri.IsHexDigit(character))
                return false;
        }

        string actual = Compute(path);
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
