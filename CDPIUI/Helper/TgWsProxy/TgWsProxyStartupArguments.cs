using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CDPIUI.Extensions;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.TgWsProxy;

/// <summary>
/// Reads and writes the tg-ws-proxy startup string through the graphic designer model.
/// </summary>
internal static partial class TgWsProxyStartupArguments
{
    public const string HostOption = "--host";
    public const string PortOption = "--port";
    public const string SecretOption = "--secret";
    public const string BufferSizeOption = "--buf-kb";
    public const string PoolSizeOption = "--pool-size";
    public const string LogMaxSizeOption = "--log-max-mb";
    public const string VerboseOption = "--verbose";
    public const string DataCenterOption = "--dc-ip";

    private static readonly string[] StringOptions =
    [
        PortOption,
        HostOption,
        SecretOption,
        BufferSizeOption,
        PoolSizeOption,
        LogMaxSizeOption,
    ];

    public static string CreateDefault() =>
        $"--host=127.0.0.1 --port=1443 --secret={SharedUtils.GetRandomHexNumber(32)} --dc-ip=2:149.154.167.220 --dc-ip=4:149.154.167.220 --buf-kb=256 --pool-size=4 --log-max-mb=5";

    public static TgWsProxyStartupValues Parse(string startupString)
    {
        ObservableCollection<GraphicDesignerSettingItemModel> lst = [];
        GraphicDesignerHelper.LoadTgWsProxyDesignerConfig(lst, []);
        GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(lst, [], startupString, exclusive: false, model: GraphicDesignerHelper.TgWsProxyDesignerConfig);

        TgWsProxyStartupValues values = new();
        foreach (var item in lst)
        {
            if (!item.IsChecked && item.Type == "string")
                continue;
            if (StringOptions.Contains(item.DisplayName))
            {
                values.Options[item.DisplayName] = item.Value;
            }
            else if (item.DisplayName == VerboseOption)
            {
                values.Verbose = item.IsChecked;
            }
            else if (item.DisplayName == DataCenterOption)
            {
                var matchResult = FindSupportedString().Match(item.Value);
                // Groups.Count is the same with or without a match.
                if (matchResult.Success)
                {
                    values.DataCenters.Add(new(
                        item.Guid,
                        matchResult.Groups[1].Value,
                        matchResult.Groups[2].Value));
                }
            }
        }
        return values;
    }

    /// <summary>
    /// Writes <paramref name="values"/> into <paramref name="startupString"/> and keeps every
    /// other argument of it. The data centers replace all "--dc-ip" entries.
    /// </summary>
    public static string Build(string startupString, TgWsProxyStartupValues values)
    {
        ObservableCollection<GraphicDesignerSettingItemModel> lst = [];
        string addArgs = GraphicDesignerHelper.ConvertStringToGraphicDesignerSettings(lst, [], startupString, exclusive: false, model: GraphicDesignerHelper.TgWsProxyDesignerConfig);

        foreach (string option in StringOptions)
        {
            if (values.Options.TryGetValue(option, out string value))
                SetValue(option, value, lst);
        }
        SetValue(VerboseOption, string.Empty, lst, values.Verbose);

        lst.RemoveAll(x => x.DisplayName == DataCenterOption);

        foreach (var item in values.DataCenters)
        {
            lst.Add(new()
            {
                Guid = Guid.NewGuid().ToString(),
                DisplayName = DataCenterOption,
                IsChecked = true,
                Value = $"{item.Number}:{item.Ip}",
                EnableTextInput = true,
                Type = "string",
                Description = string.Empty
            });
        }
        return GraphicDesignerHelper.ConvertGraphicDesignerSettingsToString(lst, [], addArgs);
    }

    /// <summary>
    /// Finds every "number:ip" pair in a text, for example one pasted from the clipboard.
    /// </summary>
    public static IReadOnlyList<(string Number, string Ip)> FindDataCenters(string text)
    {
        return FindSupportedString().Matches(text)
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value))
            .ToList();
    }

    private static void SetValue(string key, string value, ObservableCollection<GraphicDesignerSettingItemModel> list, bool isChecked = true)
    {
        var flag = list.FirstOrDefault(x => x.DisplayName == key);
        if (flag != null)
        {
            flag.IsChecked = isChecked;
            flag.Value = value;
        }
        else
        {
            list.Add(new()
            {
                Guid = Guid.NewGuid().ToString(),
                DisplayName = key,
                IsChecked = isChecked,
                Value = value,
                EnableTextInput = true,
                Type = "string",
                Description = string.Empty
            });
        }
    }

    [GeneratedRegex(@"(\d.*?):(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\D*", RegexOptions.Singleline)]
    private static partial Regex FindSupportedString();
}
