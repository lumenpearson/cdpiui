using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Default;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace CDPIUI;

public partial class App
{
    public async Task NavigateToUpdatesPage()
    {
        MainWindow mainWindow = await SafeCreateNewWindow<MainWindow>();
        mainWindow.NavView_Navigate(typeof(AboutPage), "START_CHECK", new DrillInNavigationTransitionInfo());
    }

    public bool CheckWindow<TWindow>() where TWindow : Window
    {
        return (OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null) != null);
    }

    public void ShowWindow<TWindow>() where TWindow : Window
    {
        var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
        if (activeFindWindow != null)
        {
            activeFindWindow.Activate();
        }
    }

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        foreach (var viewWindow in OpenWindows.OfType<TWindow>().ToList())
        {
            viewWindow.Close();
            OpenWindows.Remove(viewWindow);
        }
    }

    public void CloseWindow<TWindow>(string id) where TWindow : TemplateWindow
    {
        OpenWindows
            .OfType<TWindow>()
            .FirstOrDefault(window => string.Equals(
                window.Id,
                id,
                StringComparison.OrdinalIgnoreCase))
            ?.Close();
    }

    public static void ActivateWindow(Window window)
    {
        window.Activate();
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(window);
            if (windowHandle == IntPtr.Zero)
                return;

            Win32WindowApi.BringToFront(windowHandle);
        }
        catch
        {
            // Bringing the window to the front is cosmetic; it is already activated.
        }
    }

    // Every overload waits for the localizer: windows opened by a link or a file during the
    // start were built before their texts were available.
    public async Task<Window> UnsafeCreateNewWindow(Type windowType, bool activate = true, string id = "")
    {
        await _localizerReady;

        var findWindows = OpenWindows.Where(w => windowType.IsInstanceOfType(w)).ToList();

        // Only template windows have an id; a cast of every window threw for other types.
        foreach (TemplateWindow _win in findWindows.OfType<TemplateWindow>())
        {
            if (!string.IsNullOrEmpty(_win.Id) && string.Equals(
                _win.Id,
                id,
                StringComparison.OrdinalIgnoreCase))
            {
                if (activate)
                    ActivateWindow(_win);
                return _win;
            }
        }

        var newWindow = (TemplateWindow)Activator.CreateInstance(windowType);
        newWindow.Id = id;
        WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newWindow);
        RegisterWindow(newWindow, isUnsafe: true);
        if (activate)
            ActivateWindow(newWindow);

        return newWindow;
    }

    public async Task<TWindow> UnsafeCreateNewWindow<TWindow>(bool activate = true, string id = "") where TWindow : TemplateWindow, new() =>
        (TWindow)await UnsafeCreateNewWindow(typeof(TWindow), activate, id);

    public async Task<Window> SafeCreateNewWindow(Type windowType, bool activate = true)
    {
        await _localizerReady;

        var findWindows = OpenWindows.Where(w => windowType.IsInstanceOfType(w)).ToList();
        var activeFindWindow = findWindows.FirstOrDefault(w => w.DispatcherQueue != null);

        // One live window is reused; several or a dead one are closed and replaced by a new window.
        if (activeFindWindow != null && findWindows.Count == 1)
        {
            if (activate)
                ActivateWindow(activeFindWindow);
            return activeFindWindow;
        }
        else
        {
            foreach (var viewWindow in findWindows)
            {
                viewWindow.Close();
                OpenWindows.Remove(viewWindow);
            }

            var newWindow = (Window)Activator.CreateInstance(windowType);

            WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(newWindow);
            RegisterWindow(newWindow);

            if (activate)
                ActivateWindow(newWindow);

            return newWindow;
        }
    }

    public async Task<TWindow> SafeCreateNewWindow<TWindow>(bool activate = true) where TWindow : Window, new() =>
        (TWindow)await SafeCreateNewWindow(typeof(TWindow), activate);

    private void RegisterWindow(Window window, bool isUnsafe = false)
    {
        if (window == null)
            return;

        UpdateThemeForWindow(window, CurrentTheme);

        if (!OpenWindows.Contains(window))
            OpenWindows.Add(window);


        window.Closed -= Window_ClosedHandler;
        window.Closed += Window_ClosedHandler;


        // Looks pretty bad for weak PC
        // window.SizeChanged -= Window_SizeChanged;
        // window.SizeChanged += Window_SizeChanged;
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        if (sender is not Window window)
            return;
        if (args.Handled)
            return;

        try
        {
            WindowsPositionHelper.SaveWindowSizeAndPostionsettings(window);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(App), exception.ToString());
        }
    }

    private void Window_ClosedHandler(object sender, WindowEventArgs e)
    {
        if (sender is not Window window)
            return;
        if (e.Handled)
            return;
        var dispatcherQueue = window.DispatcherQueue;
        bool migrationWindowClosed = window is WelcomeWindow welcomeWindow &&
            string.Equals(
                welcomeWindow.Id,
                WelcomeWindow.MigrationWindowId,
                StringComparison.OrdinalIgnoreCase);

        try
        {
            WindowsPositionHelper.SaveWindowSizeAndPostionsettings(window);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(App), exception.ToString());
        }
        finally
        {
            window.Closed -= Window_ClosedHandler;
            window.SizeChanged -= Window_SizeChanged;

            // Leave visual-tree teardown to WinUI, including diagnostics notifications.
            try
            { _modalWindows.ReleaseModal(window, isClosing: true); }
            catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(App), exception.ToString()); }
            // List.Remove does not throw for a missing item; the guard only keeps the cleanup going.
            try
            { OpenWindows.Remove(window); }
            catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(App), exception.ToString()); }
            if (migrationWindowClosed)
                Program.CompleteMigrationActivation();
            _modalWindows.ForgetOwner(window);
        }

        if (migrationWindowClosed)
        {
            if (!dispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                ExitIfIdle))
            {
                ExitIfIdle();
            }
            return;
        }

        if (OpenWindows.Count == 0 && ApplicationTaskMonitor.IsStoreWorking())
        {
            _ = SafeCreateNewWindow<PrepareWindow>(activate: false);
        }
        else
        {
            if (!dispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                ExitIfIdle))
            {
                ExitIfIdle();
            }
        }

    }

    public TWindow GetCurrentWindowFromType<TWindow>() where TWindow : Window
    {
        return OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
    }

    public FrameworkElement GetRootFrame()
    {
        foreach (var window in OpenWindows)
        {
            try
            {
                if (window.Content is FrameworkElement rootElement)
                {
                    return rootElement;
                }
            }
            catch (Exception ex)
            {
                // The window can be closing and refuse access to its content; try the next one.
                Logger.Instance.CreateWarningLog(nameof(App), $"Cannot read the content of a window: {ex.Message}");
            }
        }
        throw new InvalidOperationException("No open window has content to use as the root frame.");
    }
}
