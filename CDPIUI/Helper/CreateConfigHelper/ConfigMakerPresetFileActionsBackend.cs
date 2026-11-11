#nullable enable

using System.Collections.Generic;
using System.IO;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.System;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Construction only stores the existing facade; settings and shell are read when invoked.
internal sealed class ConfigMakerPresetFileActionsBackend : IConfigMakerPresetFileActionsBackend
{
    private readonly ConfigMakerPresetResources _resources;

    public ConfigMakerPresetFileActionsBackend(ConfigMakerPresetResources resources) => _resources = resources;

    public int AddFiles(IEnumerable<string> paths) => _resources.AddFiles(paths);
    public string ResolvePath(string path) => _resources.ResolvePath(path);
    public string? TryResolvePath(string path) => _resources.TryResolvePath(path);
    public bool FileExists(string path) => File.Exists(path);
    public ConfigMakerPresetResource GetOrCreate(string path, ConfigMakerPresetFileKind kind, out bool added) => _resources.GetOrCreate(path, kind, out added);
    public ConfigMakerPresetResource? FindAttached(string path) => _resources.FindAttached(path);
    public bool IsReferenced(ConfigMakerPresetResource resource) => _resources.IsReferenced(resource);
    public bool ShouldAskForTextApplication() => TextFileOpenPreferences.ShouldAskForApplication();
    public void RememberTextFileDialogShown() => TextFileOpenPreferences.RememberDialogShown();
    public void OpenTextFile(string path) => ShellHelper.OpenFile(path);
    public void OpenFileInDefaultApp(string path) => ShellHelper.OpenFileInDefaultApp(path);
    public void ShowFileInFolder(string path) => ShellHelper.LookupFileInDirectory(path);
}
