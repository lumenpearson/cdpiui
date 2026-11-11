using System;
using System.Threading.Tasks;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml;

namespace CDPIUI;

public partial class App
{
    public Task ShowWindowModalAsync(Window modalWindow) => ShowWindowModalAsync(modalWindow, null);

    public Task ShowWindowModalAsync(Window modalWindow, Window ownerWindow)
    {
        ArgumentNullException.ThrowIfNull(modalWindow);
        return DispatcherTaskBridge.Run(
            () => _modalWindows.BeginModal(modalWindow, ownerWindow),
            () => _uiDispatcher.HasThreadAccess,
            action => _uiDispatcher.TryEnqueue(() => action()));
    }

    public Task MakeWindowNormal(Window modalWindow)
    {
        ArgumentNullException.ThrowIfNull(modalWindow);
        return DispatcherTaskBridge.Run(
            () => _modalWindows.ReleaseModal(modalWindow, isClosing: false),
            () => _uiDispatcher.HasThreadAccess,
            action => _uiDispatcher.TryEnqueue(() => action()));
    }
}
