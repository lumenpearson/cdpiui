using System.Diagnostics;
using System.Text.RegularExpressions;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.Core.LScript;

public class LScriptCore
{

    // The argument takes balanced parentheses: "$LOCALCONDITION(a==true ? $GETCURRENTDIR()/x $SEPARATOR y)"
    // ended at the first ")" before, and the condition was cut.
    private const string BalancedArgument = @"((?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))";
    public const string ScriptGetArgsRegex = @"\$[A-Za-z_0-9]*\(" + BalancedArgument + @"\)";
    public const string Pattern = @"\$(STATICIMAGE|DYNAMICIMAGE|LOADDYNAMIC|GETCURRENTDIR|LOCALCONDITION|GETSRDIR)(?:\(" + BalancedArgument + @"\))?";

    public const string ConditionPattern = "{0}==true ? {1} : {2}";

    public LScriptCore() { }

    public static string GetArgumentsFromScript(string scriptString)
    {
        Match match = Regex.Match(scriptString, ScriptGetArgsRegex);
        string scriptData = "";

        if (match.Success)
        {
            scriptData = match.Groups[1].Value;
        }

        return scriptData;
    }

    public static string GetScriptArgs(string scriptString, string? scriptArgs = null)
    {
        string scriptData = "";
        if (scriptString != null && scriptString.StartsWith("$"))
        {
            Match match = Regex.Match(scriptString, ScriptGetArgsRegex);

            if (match.Success)
            {
                scriptData = match.Groups[1].Value;
            }

            if (scriptArgs != null)
                scriptData = Regex.Replace(scriptData, @"{.*?}", _ => scriptArgs);
        }
        return scriptData;
    }

    public static string ExecuteScript(
        string scriptString,
        string? scriptArgs = null,
        string? callItemId = null,
        Dictionary<string, bool>? jparams = null
        )
    {
        return new LScriptCore().ExecuteScriptWork(scriptString, scriptArgs, callItemId, jparams);
    }

    public virtual string ExecuteScriptWork(
        string scriptString,
        string? scriptArgs = null,
        string? callItemId = null,
        Dictionary<string, bool>? jparams = null
        )
    {
        if (string.IsNullOrEmpty(scriptString))
            return string.Empty;
        string executeResult = scriptString.Replace("$EMPTY", "");
        try
        {
            if (scriptString != null && scriptString.StartsWith("$"))
            {
                string scriptData = GetScriptArgs(scriptString, scriptArgs);

                if (scriptString.StartsWith("$STATICIMAGE"))
                {
                    executeResult = DefaultMessageHandler.StaticImageScript(scriptData);
                }
                else if (scriptString.StartsWith("$DYNAMICIMAGE"))
                {
                    executeResult = DefaultMessageHandler.DynamicPathConverter(scriptData, scriptArgs);
                }
                else if (scriptString.StartsWith("$LOADDYNAMIC"))
                {
                    executeResult = DefaultMessageHandler.LoadAllTextFromFile(
                        DefaultMessageHandler.DynamicPathConverter(scriptData));
                }
                else if (scriptString.StartsWith("$GETCURRENTDIR"))
                {
                    string localItemsFolder = Directories.StoreItemsDirectory;

                    if (callItemId != null)
                    {
                        executeResult = Path.Combine(localItemsFolder, callItemId) +
                            scriptString.Replace("$GETCURRENTDIR()", "");
                    }
                    else
                    {
                        executeResult = localItemsFolder + scriptString.Replace("$GETCURRENTDIR()", "");
                    }
                }
                else if (scriptString.StartsWith("$LOCALCONDITION"))
                {
                    executeResult = LocalCondition(scriptData, jparams);
                }
                Logger.Instance.CreateDebugLog(nameof(LScriptCore),
                    $"Script {scriptString} execute result is {executeResult}, {scriptData}");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"Script {scriptString} failed: {ex.Message}");
        }

        return executeResult;
    }

    public static string ExecuteScriptUnsafe(
        string scriptString,
        string? scriptArgs = null,
        string? callItemId = null,
        Dictionary<string, bool>? jparams = null,
        bool quotePaths = false
        )
    {
        if (string.IsNullOrEmpty(scriptString))
            return scriptString;

        string result = scriptString.Replace("$EMPTY", "");

        try
        {
            Debug.WriteLine(Directories.StoreDirectory);
            string localItemsFolder = Directories.StoreItemsDirectory;

            if (callItemId != null)
            {
                localItemsFolder = Path.Combine(localItemsFolder, callItemId);
            }


            string source = result;
            result = Regex.Replace(result, Pattern, (Match m) =>
            {
                string command = m.Groups[1].Value.ToUpperInvariant();
                string rawArg = m.Groups[2].Success ? m.Groups[2].Value : "";
                string scriptData = rawArg;

                if (!string.IsNullOrEmpty(scriptArgs))
                {
                    scriptData = Regex.Replace(scriptData, @"{.*?}", _ => scriptArgs);
                }

                string replacement = m.Value;

                try
                {
                    switch (command)
                    {
                        case "STATICIMAGE":
                            replacement = DefaultMessageHandler.StaticImageScript(scriptData);
                            break;

                        case "DYNAMICIMAGE":
                            replacement = DefaultMessageHandler.DynamicPathConverter(scriptData, scriptArgs);
                            break;

                        case "LOADDYNAMIC":
                            replacement = DefaultMessageHandler.LoadAllTextFromFile(
                                DefaultMessageHandler.DynamicPathConverter(scriptData));
                            break;

                        case "GETCURRENTDIR":
                            replacement = quotePaths ? QuoteIfNeeded(localItemsFolder, source, m.Index) : localItemsFolder;
                            break;

                        case "LOCALCONDITION":
                            // The chosen branch can hold scripts of its own, such as $GETCURRENTDIR().
                            replacement = ExecuteScriptUnsafe(
                                LocalCondition(scriptData, jparams), scriptArgs, callItemId, jparams, quotePaths);
                            break;

                        case "GETSRDIR":
                            replacement = quotePaths
                                ? QuoteIfNeeded(Directories.StoreRepoCacheDirectory, source, m.Index)
                                : Directories.StoreRepoCacheDirectory;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(LScriptCore), $"Error executing script {m.Value}: {ex}");
                    replacement = m.Value;
                }

                Logger.Instance.CreateDebugLog(nameof(LScriptCore),
                    $"Script {m.Value} execute result is {replacement}, {scriptData}");
                return replacement ?? "";
            }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(LScriptCore), $"ExecuteScriptUnsafe general error: {ex}");
        }

        return result;
    }



    /// <summary>
    /// A folder with a space (a user profile such as "C:\Users\Ivan Petrov") split the argument
    /// it was put into (issue #2). It is quoted unless it already stands inside quotes; a quote in
    /// the middle of an argument is valid for the command-line parser of the components.
    /// </summary>
    private static string QuoteIfNeeded(string path, string text, int position)
    {
        if (!path.Any(char.IsWhiteSpace))
            return path;
        int quotesBefore = text.AsSpan(0, position).Count('"');
        return quotesBefore % 2 == 0 ? $"\"{path}\"" : path;
    }

    /// <summary>
    /// The ':' of "condition ? a : b", skipping the colon of a drive letter such as "C:\".
    /// </summary>
    private static int FindTernaryColon(string condition, int questionMark)
    {
        for (int i = questionMark + 1; i < condition.Length; i++)
        {
            if (condition[i] != ':')
                continue;
            bool isDriveColon = i > 0 && char.IsLetter(condition[i - 1]) &&
                (i == 1 || !char.IsLetterOrDigit(condition[i - 2])) &&
                i + 1 < condition.Length && (condition[i + 1] == '\\' || condition[i + 1] == '/');
            if (!isDriveColon)
                return i;
        }
        return -1;
    }

    public static string LocalCondition(string condition, Dictionary<string, bool>? jparams)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return string.Empty;

        var qPos = condition.IndexOf('?');
        int cPos;
        if (condition.Contains("$SEPARATOR"))
        {
            cPos = condition.IndexOf("$SEPARATOR");
            condition = condition.Replace("$SEPARATOR", "$");
        }
        else
            cPos = FindTernaryColon(condition, qPos);
        if (qPos < 0 || cPos < 0 || cPos < qPos)
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x0 Not correct condition");
            return string.Empty;
        }

        var condExpr = condition.Substring(0, qPos).Trim();
        var trueExpr = condition.Substring(qPos + 1, cPos - qPos - 1).Trim();
        var falseExpr = condition.Substring(cPos + 1).Trim();

        bool condValue;
        var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x1 Not correct condition");
            return string.Empty;
        }

        var varName = parts[0].Trim();
        var literal = parts[1].Trim();

        if (jparams == null || !jparams.TryGetValue(varName, out var varBool))
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"Param {varName} not exist");
            return string.Empty;
        }

        var literalBool = bool.Parse(literal);
        condValue = (varBool == literalBool);

        var exprToEval = condValue ? trueExpr.Replace("$EMPTY", "") : falseExpr.Replace("$EMPTY", "");

        var resultObj = exprToEval;
        return resultObj.ToString();
    }

    public static Tuple<string, string, string, string>? GetNameOnOffValuesFromConditionString(string conditionString)
    {
        conditionString = Regex.Replace(conditionString, Pattern, (Match m) =>
        {
            string command = m.Groups[1].Value.ToUpperInvariant();
            string rawArg = m.Groups[2].Success ? m.Groups[2].Value : "";
            string scriptData = rawArg;

            return scriptData;
        });

        if (string.IsNullOrWhiteSpace(conditionString))
            return null;

        var qPos = conditionString.IndexOf('?');
        int cPos;
        if (conditionString.Contains("$SEPARATOR"))
        {
            cPos = conditionString.IndexOf("$SEPARATOR");
            conditionString = conditionString.Replace("$SEPARATOR", "$");
        }
        else
            cPos = FindTernaryColon(conditionString, qPos);

        if (qPos < 0 || cPos < 0 || cPos < qPos)
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x0 Not correct condition");
            return null;
        }

        var condExpr = conditionString.Substring(0, qPos).Trim();
        var trueExpr = conditionString.Substring(qPos + 1, cPos - qPos - 1).Trim();
        var falseExpr = conditionString.Substring(cPos + 1).Trim();

        var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x1 Not correct condition");
            return null;
        }

        string varName = parts[0].Trim();
        string conditionVarName = "";

        Match match = Regex.Match(varName, @"^%(.*?)%=");
        if (match.Success)
        {
            conditionVarName = match.Groups[1].Value;
        }
        varName = Regex.Replace(varName, @"^%.*?%=", "");


        return Tuple.Create(varName, conditionVarName, trueExpr, falseExpr);
    }

    public static async Task<string?> RunScript(
        string? scriptString,
        Dictionary<string, string>? extraArgs = null,
        CancellationToken? cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scriptString))
            return null;

        if (cancellationToken is null)
            cancellationToken = default;

        string result = "";

        foreach (string script in scriptString.Split(";"))
        {
            if (cancellationToken?.IsCancellationRequested ?? false)
                return null;

            Match match = Regex.Match(script, ScriptGetArgsRegex);
            string scriptData = "";

            if (match.Success)
            {
                scriptData = match.Groups[1].Value;
            }

            string[] parts = scriptData.Split(", ", StringSplitOptions.RemoveEmptyEntries);
            // An empty script, for example after a trailing ';'.
            if (parts.Length == 0)
                continue;
            string scriptName = parts[0];
            string[] scriptArgs = parts.Skip(1).ToArray();

            switch (scriptName)
            {
                case "finish_component_setup":
                    await FinishComponentSetup(scriptArgs);
                    break;
                case "apply_config":
                    // Written by older preset pack builders. The preset lists are reloaded
                    // after every installation, so there is nothing left to do.
                    break;
                case "download_easy_designer_annotation_file":
                    if (scriptArgs.Length < 1)
                        break;
                    // An optional second argument is the SHA-256 of the file.
                    result += scriptArgs.Length > 1 && !string.IsNullOrWhiteSpace(scriptArgs[1])
                        ? $"DOWNLOAD={DownloadEasyDesignerAnnotationFile(scriptArgs[0])}$SEPARATORedannotationfile$SEPARATOR{scriptArgs[1].Trim()};"
                        : $"DOWNLOAD={DownloadEasyDesignerAnnotationFile(scriptArgs[0])}$SEPARATORedannotationfile;";
                    break;
                case "install_msi":
                    if (scriptArgs.Length < 2)
                        break;
                    // Awaited, so a failed installation fails the item installation instead of the process.
                    await LScriptMsiHandler.InstallMsi(scriptArgs, extraArgs, cancellationToken ?? default);
                    break;
                default:
                    Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"Unknown script command: {scriptName}");
                    break;
            }
        }
        return result;
    }

    public static string CreateCondition(string varName, string onValue, string offValue)
    {
        return string.Format(ConditionPattern, varName, onValue, offValue);
    }

    private static async Task FinishComponentSetup(string[] args)
    {
        if (args.Length < 1)
            return;
        string componentName = args[0];

        await ComponentTasksManager.Instance.UpdateTaskList();

        if (componentName == "byedpi" || componentName == "spoofdpi" || componentName == "nodpi")
        {
            if (SettingsManager.Instance.GetValue<string>("PROXY", "proxyType") == "None")
            {
                _ = PipeHelper.SendNotificationPacket(
                    Shared.Pipe.Models.NotificationsMessageIds.ProxySetupRequired,
                    new()
                    {
                        { "componentName", NormalizeComponentName(componentName) }
                    });
            }
        }
    }

    private static string DownloadEasyDesignerAnnotationFile(string url)
    {
        return url;
    }

    private static string NormalizeComponentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        return name.Replace("dpi", "DPI", StringComparison.OrdinalIgnoreCase).FirstCharToUpper();
    }
}
