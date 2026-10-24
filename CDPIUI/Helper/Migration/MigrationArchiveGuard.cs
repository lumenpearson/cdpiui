using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CDPIUI.Helper.Migration;

/// <summary>
/// Field-level checks shared by the migration archive validators. Each check throws
/// <see cref="InvalidDataException"/> when the value is unsafe.
/// </summary>
internal static class MigrationArchiveGuard
{
    public static void ValidateDiagnostics(IEnumerable<MigrationDiagnostic> diagnostics)
    {
        foreach (MigrationDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity is not ("Information" or "Warning" or "Error"))
                throw Invalid("A migration diagnostic has an invalid severity.");
            EnsureText(diagnostic.Code, "diagnostic code", 256);
            if ((diagnostic.Message?.Length ?? 0) > 8192 ||
                (diagnostic.RelatedPath?.Length ?? 0) > 4096)
                throw Invalid("A migration diagnostic exceeds the size limit.");
        }
    }

    public static void EnsureEntityId(string? value, string prefix)
    {
        string expectedPrefix = prefix + "-";
        if (value == null || !value.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            value.Length != expectedPrefix.Length + 24 ||
            value[expectedPrefix.Length..].Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid($"A {prefix} ID is invalid.");
    }

    public static void EnsureHash(string? value, string? context)
    {
        if (value == null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid($"The SHA-256 hash for '{context}' is invalid.");
    }

    public static void EnsureText(string? value, string context, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw Invalid($"The {context} is empty or too long.");
    }

    public static void EnsureSafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 ||
            Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\\'))
            throw Invalid("The migration archive contains an unsafe path.");
        string[] parts = path.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".."))
            throw Invalid("The migration archive contains an unsafe path segment.");
    }

    public static InvalidDataException Invalid(string message) => new(message);
}
