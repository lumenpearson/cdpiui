#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels.ConfigMaker;

namespace CDPIUI.Helper.CreateConfigHelper;

internal enum ConfigMakerPresetFileNoticeSeverity { Success, Warning, Error }

internal interface IConfigMakerPresetFileReplacementHost
{
    Task RunReportingErrorsAsync(Func<Task> action);
    Task<ConfigMakerFileReplacementChoice> AskPresetFileReplacementAsync(string missingPath);
    string? ChooseReplacementFile(string missingPath);
    string Text(string key);
    void ShowPresetFileMessage(string message, ConfigMakerPresetFileNoticeSeverity severity);
    void RebuildPresetStructure();
    bool UsesExplicitPresetStructure { get; }
    ConfigMakerPresetFileInfo[] ExplicitPresetFiles { get; set; }
    void SetCommandText(string commandText);
    void NotifyPresetFileReplaced(string originalPath, string replacementPath);
}
