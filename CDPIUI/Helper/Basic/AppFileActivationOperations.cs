#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core.Basic;
using CDPIUI.Messages;

namespace CDPIUI.Helper.Basic;

// Preserves the existing WinUI/import calls. Construction does not open or inspect anything.
internal sealed class AppFileActivationOperations : IAppFileActivationOperations
{
    private readonly App _app;
    private readonly Func<string, Task> _openConditionalTask;

    public AppFileActivationOperations(App app, Func<string, Task> openConditionalTask)
    {
        _app = app;
        _openConditionalTask = openConditionalTask;
    }

    public void LogFile(string path) => Logger.Instance.CreateDebugLog(nameof(App), $"Working on file {path}");
    public bool PathExists(string path) => Path.Exists(path);
    public bool IsConfigShareSupported(string path) => ConfigShareService.IsSupported(path);

    public async Task OpenConfigShareAsync(string path)
    {
        var importDialog = await _app.UnsafeCreateNewWindow<ConfigShareImportDialog>(activate: false);
        App.ActivateWindow(importDialog);
        await importDialog.SetFileAsync(path);
    }

    public Task OpenConditionalTaskAsync(string path) => _openConditionalTask(path);

    public async Task OpenUpdateFileAsync(string path)
    {
        var updateDialog = await _app.SafeCreateNewWindow<ApplicationUpdateFileDialogWindow>(activate: false);
        updateDialog.SetUpdateFilePath(path);
        App.ActivateWindow(updateDialog);
    }

    public async Task OpenStorePackAsync(string path)
    {
        var window = await _app.SafeCreateNewWindow<StoreLocalItemInstallingDialog>();
        // Not awaited: the import runs while the dialog is open. SetPackFilePath logs its own errors.
        _ = window.SetPackFilePath(path);
    }
}
