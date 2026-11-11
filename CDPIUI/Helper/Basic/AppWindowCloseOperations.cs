#nullable enable

using System;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CDPIUI.Helper.Basic;

internal sealed class AppWindowCloseOperations : IAppWindowCloseOperations<Window>
{
    private readonly ModalWindowCoordinator _modal;
    private readonly Action<Window> _detach;
    private readonly Func<Task> _createPrepare;
    private readonly Action _exit;

    public AppWindowCloseOperations(ModalWindowCoordinator modal, Action<Window> detach,
        Func<Task> createPrepare, Action exit)
    {
        _modal = modal; _detach = detach; _createPrepare = createPrepare; _exit = exit;
    }

    public Func<bool> CaptureExitEnqueue(Window window)
    {
        var dispatcherQueue = window.DispatcherQueue;
        return () => dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _exit());
    }
    public bool IsMigrationWindow(Window window) => window is WelcomeWindow welcomeWindow &&
        string.Equals(welcomeWindow.Id, WelcomeWindow.MigrationWindowId, StringComparison.OrdinalIgnoreCase);
    public void SavePosition(Window window) => WindowsPositionHelper.SaveWindowSizeAndPostionsettings(window);
    public void Detach(Window window) => _detach(window);
    public void ReleaseModal(Window window) => _modal.ReleaseModal(window, isClosing: true);
    public void Warn(Exception error) => Logger.Instance.CreateWarningLog(nameof(App), error.ToString());
    public void CompleteMigration() => Program.CompleteMigrationActivation();
    public void ForgetOwner(Window window) => _modal.ForgetOwner(window);
    public bool IsStoreWorking() => ApplicationTaskMonitor.IsStoreWorking();
    public Task CreatePrepareAsync() => _createPrepare();
    public void ExitIfIdle() => _exit();
}
