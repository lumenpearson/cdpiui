namespace CDPIUI.Core.Store.Queue
{
    public class QueueItemModel(string itemId, string operationId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFilePath = null, bool isSignedPack = false)
    {
        public string OperationId { get; } = operationId;
        public string ItemId { get; } = itemId;
        public string Version { get; } = version ?? string.Empty;
        public bool CleanDirectoryBeforeInstalling { get; } = cleanDirectoryBeforeInstalling;
        public string? PackFilePath { get; } = packFilePath ?? string.Empty;
        // A local pack whose catalog signature was verified; only such packs may bring executables and install actions.
        public bool IsSignedPack { get; } = isSignedPack;
        public string Status { get; set; } = "WAIT";
        public string DownloadStage { get; set; } = string.Empty;
        public double DownloadProgress { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
    }
}
