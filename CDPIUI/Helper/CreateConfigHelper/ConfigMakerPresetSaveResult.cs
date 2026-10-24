#nullable enable

using System.Collections.Generic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ConfigMakerPresetSaveResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorDetails { get; init; } = string.Empty;
    public string PackId { get; init; } = string.Empty;
    public string ConfigFileName { get; init; } = string.Empty;
    public int CopiedFileCount { get; init; }
    public IReadOnlyList<ConfigMakerResourceMetadata> StoredResources { get; init; } = [];

    public static ConfigMakerPresetSaveResult Failed(string code, string details = "") => new()
    {
        ErrorCode = code,
        ErrorDetails = details,
    };
}
