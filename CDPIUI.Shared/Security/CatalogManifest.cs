using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Shared.Security;

/// <summary>The catalog's bounded member list, compared only with already held files.</summary>
internal sealed class CatalogManifest
{
    private readonly Dictionary<string, string> _members = new(StringComparer.OrdinalIgnoreCase);
    private string Algorithm { get; }
    private CatalogManifest(string algorithm) { Algorithm = algorithm; }

    public static CatalogManifest? Parse(string output)
    {
        using var lines = new StringReader(output);
        if (lines.ReadLine() != "CDPIUI-CATALOG-V1") return null;
        string? algorithm = lines.ReadLine();
        if (algorithm != "SHA256" && algorithm != "SHA1") return null;
        var manifest = new CatalogManifest(algorithm);
        var utf8 = new UTF8Encoding(false, true);
        try
        {
            string? line;
            while ((line = lines.ReadLine()) != null)
            {
                int colon = line.IndexOf(':');
                if (colon <= 0 || manifest._members.Count >= ArchivePaths.MaxEntries) return null;
                string name = utf8.GetString(Convert.FromBase64String(line.Substring(0, colon))).Replace('/', '\\');
                string[] parts = name.Split('\\');
                if (parts.Length > 64 || parts.Any(part => !WindowsPathNames.IsOrdinaryName(part))) return null;
                string hash = line.Substring(colon + 1);
                int hashLength = algorithm == "SHA256" ? 64 : 40;
                if (hash.Length != 0 && (hash.Length != hashLength || hash.Any(c => !Uri.IsHexDigit(c)))) return null;
                if (manifest._members.ContainsKey(name)) return null;
                manifest._members.Add(name, hash);
            }
            return manifest;
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException) { return null; }
    }

    public bool Matches(ReadOnlyDirectorySnapshot snapshot, IEnumerable<string> filesToSkip, string catalogRelativePath)
    {
        var skip = new HashSet<string>(filesToSkip, StringComparer.OrdinalIgnoreCase) { "catalog.cat" };
        var expected = _members.Where(pair => !skip.Contains(Path.GetFileName(pair.Key))).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        using var hash = new WindowsCatalogHash(Algorithm);
        foreach (ReadOnlyDirectorySnapshot.FileEntry file in snapshot.Files)
        {
            if (string.Equals(file.RelativePath, catalogRelativePath, StringComparison.OrdinalIgnoreCase) || skip.Contains(Path.GetFileName(file.RelativePath))) continue;
            if (!expected.TryGetValue(file.RelativePath, out string? digest) ||
                !string.Equals(hash.Compute(file.Stream), digest, StringComparison.OrdinalIgnoreCase)) return false;
            expected.Remove(file.RelativePath);
        }
        return expected.Count == 0;
    }
}
