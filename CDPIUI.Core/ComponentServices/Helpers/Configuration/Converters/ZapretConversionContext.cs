using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// The state of one Zapret1 to Zapret2 conversion: the issues found, the files the command
/// line refers to and the blobs the converted command line defines.
/// </summary>
internal sealed class ZapretConversionContext
{
    private sealed record BlobDefinition(string Name, string Expression);

    private readonly ZapretConversionOptions _options;
    private readonly List<ZapretConversionIssue> _issues = [];
    private readonly List<ZapretConversionFileReference> _referencedFiles = [];
    private readonly Dictionary<string, BlobDefinition> _blobs = new(StringComparer.Ordinal);

    public ZapretConversionContext(ZapretConversionOptions options)
    {
        _options = options;
    }

    public ZapretConversionOptions Options => _options;

    public void Reset()
    {
        _issues.Clear();
        _referencedFiles.Clear();
        _blobs.Clear();
    }

    /// <summary>
    /// The --blob options for every blob used by the converted command line, sorted by name.
    /// </summary>
    public IEnumerable<string> GetBlobOptionValues() =>
        _blobs.Values
            .OrderBy(blob => blob.Name, StringComparer.Ordinal)
            .Select(blob => $"{blob.Name}:{blob.Expression}");

    public string GetBlobValueOrDefault(
        string? value,
        string defaultValue,
        int profileIndex,
        string optionName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : GetBlobValue(value, profileIndex, optionName);
    }

    public string GetBlobValue(string value, int profileIndex, string optionName)
    {
        string normalized = value.Trim().Trim('"');
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized == "!")
        {
            return "fake_default_tls";
        }

        if (normalized.StartsWith("!", StringComparison.Ordinal))
        {
            AddWarning(
                "TLS_FAKE_OFFSET_IGNORED",
                $"The standard TLS fake offset '{normalized}' has no direct Zapret2 equivalent.",
                optionName,
                profileIndex);
            return "fake_default_tls";
        }

        ParseFileExpression(normalized, out string sourcePath, out long offset);
        string convertedPath = _options.FilePathMapper?.Invoke(sourcePath) ?? sourcePath;
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{offset}:{convertedPath}");

        if (_blobs.TryGetValue(key, out BlobDefinition? existing))
        {
            return existing.Name;
        }

        string blobName = "z1_" + global::System.Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
        string prefix = offset > 0 ? $"+{offset.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        string expression = $"{prefix}@{convertedPath}";

        _blobs.Add(key, new BlobDefinition(blobName, expression));
        _referencedFiles.Add(new ZapretConversionFileReference(
            optionName,
            sourcePath,
            convertedPath,
            offset,
            ZapretConversionFileAccess.Read,
            blobName));

        return blobName;
    }

    public string MapDirectFileValue(
        string optionName,
        string value,
        ZapretConversionFileAccess access,
        bool onlyWhenAtPrefixed = false)
    {
        string normalized = value.Trim().Trim('"');
        bool atPrefixed = normalized.StartsWith('@');
        if (onlyWhenAtPrefixed && !atPrefixed)
        {
            return value;
        }

        string sourcePath = atPrefixed ? normalized[1..] : normalized;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return value;
        }

        string convertedPath = _options.FilePathMapper?.Invoke(sourcePath) ?? sourcePath;
        if (!_referencedFiles.Any(file =>
                file.OptionName.Equals(optionName, StringComparison.OrdinalIgnoreCase) &&
                file.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) &&
                file.ConvertedPath.Equals(convertedPath, StringComparison.OrdinalIgnoreCase) &&
                file.Offset == 0 &&
                file.Access == access))
        {
            _referencedFiles.Add(new ZapretConversionFileReference(
                optionName,
                sourcePath,
                convertedPath,
                0,
                access));
        }

        return atPrefixed ? $"@{convertedPath}" : convertedPath;
    }

    private static void ParseFileExpression(string value, out string path, out long offset)
    {
        offset = 0;
        path = value;

        // "+N@path" or "@path": an '@' later in the path is part of the file name.
        int atIndex = value.IndexOf('@');
        if (atIndex == 0 || (atIndex > 0 && value[0] == '+' && value[1..atIndex].All(char.IsDigit)))
        {
            string offsetPart = value[..atIndex];
            path = value[(atIndex + 1)..];

            if (offsetPart.StartsWith('+') &&
                long.TryParse(offsetPart[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                offset = parsed;
            }
        }

        path = path.Trim().Trim('"');
    }

    public ZapretConversionResult CreateResult(string startupString)
    {
        return new ZapretConversionResult
        {
            StartupString = startupString,
            Issues = _issues.ToArray(),
            ReferencedFiles = _referencedFiles.ToArray()
        };
    }

    public void AddWarning(
        string code,
        string message,
        string? optionName = null,
        int? profileIndex = null)
    {
        _issues.Add(new ZapretConversionIssue(
            ZapretConversionIssueSeverity.Warning,
            code,
            message,
            optionName,
            profileIndex));
    }

    public void AddError(
        string code,
        string message,
        string? optionName = null,
        int? profileIndex = null)
    {
        _issues.Add(new ZapretConversionIssue(
            ZapretConversionIssueSeverity.Error,
            code,
            message,
            optionName,
            profileIndex));
    }
}
