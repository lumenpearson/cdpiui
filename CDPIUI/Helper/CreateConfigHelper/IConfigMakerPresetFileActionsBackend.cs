#nullable enable

using System.Collections.Generic;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerPresetFileActionsBackend
{
    int AddFiles(IEnumerable<string> paths);
    string ResolvePath(string path);
    string? TryResolvePath(string path);
    bool FileExists(string path);
    ConfigMakerPresetResource GetOrCreate(string fullPath, ConfigMakerPresetFileKind kind, out bool added);
    ConfigMakerPresetResource? FindAttached(string path);
    bool IsReferenced(ConfigMakerPresetResource resource);
    bool ShouldAskForTextApplication();
    void RememberTextFileDialogShown();
    void OpenTextFile(string path);
    void OpenFileInDefaultApp(string path);
    void ShowFileInFolder(string path);
}
