using System.Text.RegularExpressions;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Converts a .bat or .cmd file that starts winws into a preset.
/// </summary>
internal static partial class BatConfigConverter
{
    // start ["title"] [/min /b ...] program args: the program may be quoted or not, so that
    // `start "t" /min %BIN%winws.exe ...` does not leave the program in the arguments.
    private const string BatStartRegex = @"^start\s+(?:""[^""]*""\s+)?(?:/\w+\s+)*(?:""[^""]*""|\S+)(?<args>.*?)\^?\s*$";

    [GeneratedRegex(BatStartRegex, RegexOptions.IgnoreCase)]
    private static partial Regex BatFileStartRegex();

    [GeneratedRegex(@"!(.*?)!")]
    private static partial Regex VarInVarRegex();

    [GeneratedRegex(@"^rem\s+set\s", RegexOptions.IgnoreCase)]
    private static partial Regex RemSetRegex();

    private sealed class ParseState
    {
        public Dictionary<string, string> Vars { get; } = [];

        public List<AvailableVarValues> AvailableVarValues { get; } = [];

        public bool ErrorHappens { get; set; }

        public bool CommaBuilderMode { get; set; }

        public string Comment { get; set; } = "";

        public string Comma { get; set; } = "";

        public List<string>? Target { get; set; }
    }

    /// <summary>
    /// Returns the preset and whether a variable of the file could not be resolved.
    /// </summary>
    public static Tuple<ConfigItem, bool> Convert(string filepath)
    {
        // TODO: recursevily call other .BAT files via GOTO check
        string[] lines = CDPIUI.Core.System.BatchFileEncoding.ReadAllLines(filepath);
        ParseState state = new();

        foreach (string line in lines)
        {
            if (!ParseLine(line, state))
                break;
        }

        RemoveScriptFolderVariables(state);

        ConfigItem configItem = new()
        {
            meta = "pUC:v1.0",
            name = Path.GetFileNameWithoutExtension(filepath),
            target = state.Target,
            commaVars = state.Vars.Count > 0 ? state.Vars : null,
            availableCommaVarsValues = state.AvailableVarValues.Count > 0 ? state.AvailableVarValues : null,
            jparams = [],
            variables = [],
            startup_string = state.Comma,
        };

        return Tuple.Create(configItem, state.ErrorHappens);
    }

    /// <summary>
    /// Handles one line of the file. Returns false when the rest of the file is not read.
    /// </summary>
    private static bool ParseLine(string line, ParseState state)
    {
        if (string.IsNullOrEmpty(line))
            return true;

        if (line.StartsWith("set", StringComparison.OrdinalIgnoreCase))
        {
            ParseSetLine(line, state);
            return true;
        }
        if (line.StartsWith("::") && line.EndsWith("::"))
        {
            state.Comment = line[2..^2].Trim();
            return true;
        }
        // "rem set NAME=value" lists another value of a variable; a comment such as
        // "rem settings = ..." is no such line.
        if (RemSetRegex().IsMatch(line))
        {
            ParseRemSetLine(line, state);
            return true;
        }
        if (line.StartsWith("start", StringComparison.OrdinalIgnoreCase))
        {
            ParseStartLine(line, state);
            return true;
        }
        if (state.CommaBuilderMode)
        {
            state.Comma += line.Replace('^', ' ');

            Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Comma++ {state.Comma}");

            if (!line.EndsWith('^'))
            {
                state.CommaBuilderMode = false;
                return false;
            }
        }
        return true;
    }

    private static void ParseSetLine(string line, ParseState state)
    {
        Dictionary<string, string> vars = state.Vars;
        string[] setLine = line[3..].Split("=");
        if (setLine.Length < 2)
            return;

        // set "WF=..." keeps a space before the name: trimmed here, or the
        // variable was not found by the alternatives below.
        string key = setLine[0].Replace("\"", "").Trim();
        string value = line[3..].Replace($"{setLine[0]}=", "").Replace("\"", "").Trim();

        // Every !VAR! of the value, not only the first one.
        foreach (Match match in VarInVarRegex().Matches(value))
        {
            string varInVar = match.Groups[1].Value;
            if (vars.ContainsKey(varInVar))
            {
                value = value.Replace($"!{varInVar}!", vars[varInVar]);
            }
            else
            {
                state.ErrorHappens = true;
                Logger.Instance.CreateWarningLog(
                    nameof(ConfigurationService),
                    $"Error happens, cannot find var {varInVar}");
            }
        }
        if (!vars.ContainsKey(key.Trim()))
            vars.Add(key.Trim(), value);
        else
            vars[key.Trim()] = value;

        AvailableVarValues availableVar = state.AvailableVarValues.FirstOrDefault(x => x.VarName == key);
        if (availableVar != null)
        {
            state.AvailableVarValues.Remove(availableVar);
            availableVar.Values.Add(value);
            availableVar.CurrentValueIndex = availableVar.Values.Count - 1;
            state.AvailableVarValues.Add(availableVar);
        }

        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found var {key} = {value}");
    }

    private static void ParseRemSetLine(string line, ParseState state)
    {
        string _l = line[3..];
        var rx = new Regex(@"\s+set\b", RegexOptions.None);
        _l = rx.Replace(_l, "", 1);

        string[] remLine = _l.Split("=");
        if (remLine.Length < 2)
            return;

        remLine[0] = remLine[0].Trim();
        remLine[1] = remLine[1].Trim();
        string value = rx.Replace(line[3..], "", 1).Replace($"{remLine[0]}=", "").Trim();

        bool found = false;
        foreach (var availableVar in state.AvailableVarValues)
        {
            if (availableVar.VarName.Trim() == remLine[0])
            {
                if (!availableVar.Values.Contains(value))
                    availableVar.Values.Add(value);
                found = true;
                break;
            }
        }
        if (!found)
        {
            List<string> values = [];
            if (state.Vars.TryGetValue(remLine[0], out string _value))
                values.Add(_value);

            if (!values.Contains(value))
                values.Add(value);

            state.AvailableVarValues.Add(new()
            {
                Comment = state.Comment,
                VarName = remLine[0],
                // Index 0 is the value of "set X=..." when it came first; it was
                // left out, so "rem set X=b" after "set X=a" lost "a".
                CurrentValueIndex = 0,
                Values = values
            });
            state.Comment = "";
        }
        if (!state.Vars.ContainsKey(remLine[0]))
            state.Vars.Add(remLine[0], "$EMPTY");
        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found REM var {remLine[0]} = {value}");
    }

    private static void ParseStartLine(string line, ParseState state)
    {
        if (line.Contains("winws.exe"))
        {
            string ver = DatabaseHelper.Instance
                .GetItemById(HardcodedItemIds.ComponentIds[Components.Zapret])?.CurrentVersion ??
                "%CURRENT%";
            state.Target = [HardcodedItemIds.ComponentIds[Components.Zapret], ver];
        }

        if (line.Contains('^'))
            state.CommaBuilderMode = true;
        Match match = BatFileStartRegex().Match(line);
        if (match.Success)
        {
            state.Comma = match.Groups["args"].Value.Trim().Replace('^', ' ');
            Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found start line {state.Comma}");
        }
    }

    // A path relative to the script folder (%~dp0) is written into the startup string.
    private static void RemoveScriptFolderVariables(ParseState state)
    {
        foreach (var _var in state.Vars)
        {
            if (_var.Value.StartsWith("%~dp0"))
            {
                state.Vars.Remove(_var.Key);
                state.Comma = state.Comma.Replace($"%{_var.Key}%", _var.Value.Replace("%~dp0", ""));
            }
        }
    }
}
