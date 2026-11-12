#nullable enable

using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

// Reads live application state at the original Store/idle decision points.
internal sealed class AppIdleExitController
{
    private readonly Func<int> _windowCount;
    private readonly Func<bool> _migrationPending;
    private readonly Func<bool> _storeWorking;
    private readonly Func<bool> _goodCheckRunning;
    private readonly Func<Task> _createPrepare;
    private readonly Action _closePrepare;
    private readonly Action _exit;
    private readonly Action<Exception> _warn;

    public AppIdleExitController(Func<int> windowCount, Func<bool> migrationPending,
        Func<bool> storeWorking, Func<bool> goodCheckRunning, Func<Task> createPrepare,
        Action closePrepare, Action exit, Action<Exception> warn)
    {
        _windowCount = windowCount;
        _migrationPending = migrationPending;
        _storeWorking = storeWorking;
        _goodCheckRunning = goodCheckRunning;
        _createPrepare = createPrepare;
        _closePrepare = closePrepare;
        _exit = exit;
        _warn = warn;
    }

    public void HandleStoreStateChanged(bool isWorking)
    {
        try
        {
            if (isWorking && _windowCount() == 0 && !_migrationPending())
            {
                _ = _createPrepare();
            }
            if (!isWorking)
            {
                _closePrepare();
                ExitIfIdle();
            }
        }
        catch (Exception exception)
        {
            _warn(exception);
            ExitIfIdle();
        }
    }

    public void ExitIfIdle()
    {
        if (_migrationPending() && _windowCount() == 0)
        {
            return;
        }
        if (_windowCount() != 0 || _storeWorking() || _goodCheckRunning())
        {
            return;
        }

        _exit();
    }
}
