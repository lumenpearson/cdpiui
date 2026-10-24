using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace CDPIUI;

public partial class App
{
    public Task ShowWindowModalAsync(Window modalWindow) => ShowWindowModalAsync(modalWindow, null);

    public Task ShowWindowModalAsync(Window modalWindow, Window ownerWindow)
    {
        ArgumentNullException.ThrowIfNull(modalWindow);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async void Begin()
        {
            try
            { await _modalWindows.BeginModal(modalWindow, ownerWindow); completion.TrySetResult(true); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }
        if (_uiDispatcher.HasThreadAccess)
            Begin();
        else if (!_uiDispatcher.TryEnqueue(Begin))
            completion.TrySetCanceled();
        return completion.Task;
    }

    public Task MakeWindowNormal(Window modalWindow)
    {
        ArgumentNullException.ThrowIfNull(modalWindow);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Restore()
        {
            try
            { _modalWindows.ReleaseModal(modalWindow, isClosing: false); completion.TrySetResult(true); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }
        if (_uiDispatcher.HasThreadAccess)
            Restore();
        else if (!_uiDispatcher.TryEnqueue(Restore))
            completion.TrySetCanceled();
        return completion.Task;
    }
}
