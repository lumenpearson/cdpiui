#nullable enable

using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.Basic;

// Preserves the window-close cleanup and the original post-cleanup dispatch policy.
internal sealed class AppWindowCloseCoordinator<TWindow> where TWindow : class
{
    private readonly List<TWindow> _windows;
    private readonly IAppWindowCloseOperations<TWindow> _operations;

    public AppWindowCloseCoordinator(List<TWindow> windows, IAppWindowCloseOperations<TWindow> operations)
    {
        _windows = windows;
        _operations = operations;
    }

    public void HandleClosed(TWindow window)
    {
        Func<bool> enqueueExit = _operations.CaptureExitEnqueue(window);
        bool migrationWindowClosed = _operations.IsMigrationWindow(window);
        try
        {
            _operations.SavePosition(window);
        }
        catch (Exception exception)
        {
            _operations.Warn(exception);
        }
        finally
        {
            _operations.Detach(window);
            try { _operations.ReleaseModal(window); }
            catch (Exception exception) { _operations.Warn(exception); }
            try { _windows.Remove(window); }
            catch (Exception exception) { _operations.Warn(exception); }
            if (migrationWindowClosed)
                _operations.CompleteMigration();
            _operations.ForgetOwner(window);
        }

        if (migrationWindowClosed)
        {
            if (!enqueueExit())
                _operations.ExitIfIdle();
            return;
        }

        if (_windows.Count == 0 && _operations.IsStoreWorking())
        {
            _ = _operations.CreatePrepareAsync();
        }
        else
        {
            if (!enqueueExit())
                _operations.ExitIfIdle();
        }
    }
}
