using System;
using CDPIUI.Default;
using Microsoft.UI.Xaml;

namespace CDPIUI.Helper.WindowHelper;

// WinUI effects remain at the application boundary; the coordinator only orders them.
internal sealed class AppWindowOperations(Action<Window, bool> register) : IAppWindowOperations<Window>
{
    public bool HasDispatcher(Window window) => window.DispatcherQueue != null;

    public bool TryGetTemplateId(Window window, out string id)
    {
        id = (window as TemplateWindow)?.Id;
        return window is TemplateWindow;
    }

    public Window Create(Type windowType) => (Window)Activator.CreateInstance(windowType);
    public void SetTemplateId(Window window, string id) => ((TemplateWindow)window).Id = id;
    public void SetPosition(Window window) => WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(window);
    public void Register(Window window, bool isUnsafe) => register(window, isUnsafe);
    public void Activate(Window window) => App.ActivateWindow(window);
    public void Close(Window window) => window.Close();
}
