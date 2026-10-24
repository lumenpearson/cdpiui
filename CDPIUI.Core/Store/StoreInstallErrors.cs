using CDPIUI.Shared.PrettyErrorConversionService;
using static CDPIUI.Core.Basic.ErrorsHelper;

namespace CDPIUI.Core.Store;

/// <summary>
/// Error codes of the item installation.
/// </summary>
internal static class StoreInstallErrors
{
    public static ErrorModel FromCode(PrettyErrorCode error)
    {
        return new() { ErrorCode = $"ERR_STORE_{error}" };
    }

    public static string FromException(Exception ex)
    {
        string errorCode = Convertor.GetPrettyErrorCode("ERR_ITEM_INSTALLING", ex);
        return errorCode;
    }
}
