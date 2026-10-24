using System.Collections.Specialized;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// The navigation parameters of the pages in the config helper window. CreateNewConfigPage
// reads the "type" value to choose how it opens.
internal static class CreateConfigNavigationParameters
{
    public const string FromStringType = "CFGSTRING";
    public const string ImportType = "CFGIMPORT";
    public const string EditType = "CFGEDIT";
    public const string CreateByIdType = "CFGCREATEBYID";
    public const string ReturnEditedType = "CFGRETURNEDITED";

    public static NameValueCollection EditConfig(ConfigItem configItem) => new()
    {
        { "type", EditType },
        { "configItem", JSONConvertor.SerializeObject(configItem) },
    };

    public static NameValueCollection CreateForComponent(string componentId) => new()
    {
        { "type", CreateByIdType },
        { "componentId", componentId },
    };

    public static NameValueCollection CreateFromString(string componentId, string startupString) => new()
    {
        { "type", FromStringType },
        { "componentId", componentId },
        { "startupString", startupString },
    };

    public static NameValueCollection ImportConfig(ConfigItem configItem, bool errorHappens, string filePath) => new()
    {
        { "type", ImportType },
        { "configItem", JSONConvertor.SerializeObject(configItem) },
        { "errorHappens", errorHappens.ToString() },
        { "filePath", filePath },
    };

    public static NameValueCollection GoodCheckReport(string filePath) => new()
    {
        { "type", NavigationState.LoadFileFromPath.ToString() },
        { "filePath", filePath },
    };

    public static NameValueCollection ConfigKit(string kitId) => new()
    {
        { "kitId", kitId },
    };
}
