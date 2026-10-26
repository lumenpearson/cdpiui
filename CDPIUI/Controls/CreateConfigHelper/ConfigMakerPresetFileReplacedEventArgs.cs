using System;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed class ConfigMakerPresetFileReplacedEventArgs(
    string commandText,
    string originalPath,
    string replacementPath) : EventArgs
{
    public string CommandText { get; } = commandText;
    public string OriginalPath { get; } = originalPath;
    public string ReplacementPath { get; } = replacementPath;
}
