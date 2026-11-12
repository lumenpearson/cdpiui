#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Converts existing designer models to/from command text; model creation stays in the host.
internal static class GraphicDesignerCommandCodec
{
    public static string Parse(
        ObservableCollection<GraphicDesignerSettingItemModel> list,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList,
        string args,
        bool exclusive = true,
        Func<string, GraphicDesignerCommandValue, bool>? addModelItem = null
        )
    {
        ClearSelection(list, exList);

        var notSupportedFlags = new StringBuilder();
        List<string> tokens = Tokenize(args);

        for (int i = 0; i < tokens.Count; i++)
        {
            (string token, string value, bool separateValue) = ReadOption(tokens, ref i);

            var item = exclusive ? list.FirstOrDefault(x => x.DisplayName == token) : null;
            bool supported;
            if (item == null && exclusive)
            {
                supported = TrySelectExclusiveItem(exList, token, value, separateValue);
            }
            else if (item == null && !exclusive && addModelItem != null)
            {
                supported = addModelItem(token, new(DecodeQuotedValue(value), IsQuotePair(value)));
            }
            else
            {
                CheckItem(item!, value, separateValue);
                supported = true;
            }

            if (!supported)
            {
                notSupportedFlags.Append(token);
                if (!string.IsNullOrEmpty(value))
                {
                    // Inline ownership must survive when a filename also looks
                    // like a real option on the next designer visit.
                    bool keepInline = !separateValue && LooksLikeOption(value);
                    notSupportedFlags.Append(keepInline ? '=' : ' ').Append(value);
                }
                notSupportedFlags.Append(' ');
            }
        }

        return notSupportedFlags.ToString();
    }

    private static void ClearSelection(
        ObservableCollection<GraphicDesignerSettingItemModel> list,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList)
    {
        foreach (var item in list)
        {
            item.IsChecked = false;
        }
        foreach (var exItem in exList)
        {
            foreach (var item in exItem.Items)
            {
                item.IsChecked = false;
            }
        }
    }

    private static List<string> Tokenize(string args)
    {
        var tokenPattern = @"(?<=\s|^)(?:(?:(?:--?|/)[^\s]*=""[^""]*"")|""[^""]*""|[^ ]+)+";
        return Regex.Matches(args, tokenPattern)
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .ToList();
    }

    // Reads the option at index i and its value: "--name=value", or "--name value" when the
    // next token is not an option (then i moves past the value).
    private static (string Token, string Value, bool SeparateValue) ReadOption(List<string> tokens, ref int i)
    {
        string token = tokens[i];
        // Quoted values must reach the model/additional text intact. Only unwrap a
        // whole quoted option here, where its role as an option is already known.
        bool quotedOption = token.Length >= 3 && token[0] == '"' && token[^1] == '"' &&
            LooksLikeOption(token);
        if (quotedOption)
        {
            token = token[1..^1];
        }
        string value = string.Empty;
        bool separateValue = false;
        if (token.Contains('='))
        {
            // The value may contain '=' itself (base64, fake payload hex and so on).
            int separator = token.IndexOf('=');
            value = token[(separator + 1)..];
            token = token[..separator];
            // The outer quote pair grouped the entire --name=value argument. Move
            // that grouping to a value which would otherwise split or disappear.
            if (quotedOption && (value.Length == 0 || value.Any(char.IsWhiteSpace)) &&
                !(value.Length >= 2 && value[0] == '"' && value[^1] == '"'))
            {
                value = $"\"{value}\"";
            }
        }
        else if (tokens.Count - 1 >= i + 1 && !LooksLikeOption(tokens[i + 1]))
        {
            value = tokens[i + 1];
            separateValue = true;
            i++;
        }
        return (token, value, separateValue);
    }

    private static bool LooksLikeOption(string token)
    {
        string name = token.Trim('"');
        if (IsQuotePair(token))
        {
            int separator = name.IndexOf('=');
            string optionName = separator < 0 ? name : name[..separator];
            // Whitespace in the option name identifies a grouped value. Spaces
            // after '=' still belong to a genuine whole quoted option's value.
            if (optionName.Any(char.IsWhiteSpace))
                return false;
        }
        return name.StartsWith('-') || name.StartsWith('/');
    }

    private static bool TrySelectExclusiveItem(
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList,
        string token,
        string value,
        bool separateValue)
    {
        foreach (var exModel in exList)
        {
            var item = exModel.Items.FirstOrDefault(x => x.DisplayName == token);
            if (item != null)
            {
                CheckItem(item, value, separateValue);
                exModel.SelectedItemGuid = item.Guid;
                return true;
            }
        }
        return false;
    }

    private static bool IsQuotePair(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"';

    private static string DecodeQuotedValue(string value) => IsQuotePair(value) ? value[1..^1] : value;

    private static void CheckItem(GraphicDesignerSettingItemModel item, string value, bool separateValue)
    {
        item.IsChecked = true;
        bool preserveGrouping = IsQuotePair(value);
        if (separateValue || item.Type == "enum")
            value = DecodeQuotedValue(value);
        if (value.StartsWith('"') && !value.EndsWith('"'))
            value += "\"";
        item.SetParsedValue(value, preserveGrouping);
    }

    private static readonly List<string> TextSupportedValues = ["string", "integer", "file_path", "enum"];



    public static string Build(ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList, string additionalArgs)
    {
        string startupString = string.Empty;
        foreach (var item in list)
        {
            (bool flowControl, startupString) = ProcessItemActions(startupString, item);
            if (!flowControl)
            {
                continue;
            }
        }
        foreach (var itemGroup in exList)
        {
            var item = itemGroup.Items.FirstOrDefault(x => x.Guid == itemGroup.SelectedItemGuid);
            if (item != null)
            {
                (bool flowControl, startupString) = ProcessItemActions(startupString, item);
                if (!flowControl)
                {
                    continue;
                }
            }
        }
        return $"{startupString}{additionalArgs}";
    }

    private static (bool flowControl, string value) ProcessItemActions(string startupString, GraphicDesignerSettingItemModel item)
    {
        if (!item.IsChecked && item.Type != "enum")
            return (flowControl: false, value: startupString);
        startupString += $"{item.DisplayName}";
        string? value = item.Value;
        if ((item.EnableTextInput || TextSupportedValues.Contains(item.Type)) && value != null &&
            (value.Length > 0 || item.PreserveValueGrouping))
        {
            if (item.PreserveValueGrouping && !IsQuotePair(value) &&
                (value.Length == 0 || value.Any(char.IsWhiteSpace)))
                value = $"\"{value}\"";
            startupString += $"={value}";
        }
        startupString += " ";
        return (flowControl: true, value: startupString);
    }

}

internal readonly record struct GraphicDesignerCommandValue(string Text, bool PreserveGrouping);
