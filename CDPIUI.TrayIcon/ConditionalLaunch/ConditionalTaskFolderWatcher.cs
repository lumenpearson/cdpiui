using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// Watches the task files of a folder and reports every change and every watcher failure.
/// </summary>
internal sealed class ConditionalTaskFolderWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly FileSystemEventHandler _changed;
    private readonly RenamedEventHandler _renamed;
    private readonly ErrorEventHandler _error;

    public ConditionalTaskFolderWatcher(
        string directory,
        FileSystemEventHandler changed,
        ErrorEventHandler error)
    {
        _changed = changed;
        _renamed = new RenamedEventHandler(changed);
        _error = error;
        _watcher = new FileSystemWatcher(
            directory,
            $"*{ConditionalTaskFileService.FileExtension}")
        {
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += _changed;
        _watcher.Created += _changed;
        _watcher.Deleted += _changed;
        _watcher.Renamed += _renamed;
        _watcher.Error += _error;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= _changed;
        _watcher.Created -= _changed;
        _watcher.Deleted -= _changed;
        _watcher.Renamed -= _renamed;
        _watcher.Error -= _error;
        _watcher.Dispose();
    }
}
