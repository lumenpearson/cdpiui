namespace CDPIUI.Helper.AddOns.BlockCheck2;

public sealed class BlockCheck2PresetStorageResult
{
    public bool Success { get; init; }
    public string PresetName { get; init; } = string.Empty;
    public string ConfigFileName { get; init; } = string.Empty;
    public int CopiedFileCount { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorDetails { get; init; } = string.Empty;

    public static BlockCheck2PresetStorageResult Failed(
        string errorCode,
        string errorDetails = null) => new()
        {
            ErrorCode = errorCode,
            ErrorDetails = errorDetails ?? string.Empty,
        };
}
