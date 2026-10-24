using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CDPIUI.Controls.WindowControls;
using CDPIUI.Core;
using CDPIUI.Helper;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using WinUIEx;
using Application = Microsoft.UI.Xaml.Application;
using Size = System.Windows.Size;

namespace CDPIUI.Default;

public enum TitleBarModes
{
    System = 0,
    Custom = 1,
}
public partial class TemplateWindow : WindowEx
{
    public new AppWindow AppWindow = null;
    public OverlappedPresenter OverlappedPresenter = null;

    public event Action NewIdSet;

    private string _windowTitleProperty = "CDPI UI";
    public string WindowTitle
    {
        get
        {
            return _windowTitleProperty;
        }
        set
        {
            _windowTitleProperty = value;
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (WindowTitle != null)
                {
                    Title = UIHelper.GetWindowName(WindowTitle);
                    if (CustomTitleBarUserControl != null)
                        CustomTitleBarUserControl.Title = WindowTitle;
                }
            });
        }
    }

    private Size _windowSizeProperty = new(484, 300);
    public Size WindowMinSize
    {
        get
        {
            return _windowSizeProperty;
        }
        set
        {
            if (_windowSizeProperty == value)
            { return; }
            _windowSizeProperty = value;
            UpdateWindowMinSize();
        }
    }

    private string _iconUriProperty = "";
    public string IconUri
    {
        get
        {
            return _iconUriProperty;
        }
        set
        {
            _iconUriProperty = value;
            UpdateWindowIcon();
        }
    }

    private TitleBarUserControl _customTitleBarUserControlProperty = null;
    public TitleBarUserControl CustomTitleBarUserControl
    {
        get
        {
            return _customTitleBarUserControlProperty;
        }
        set
        {
            _customTitleBarUserControlProperty = value;
            InitializeTitleBar();
        }
    }

    private bool _isAppShownInSwitchersProperty = false;
    public bool IsAppShownInSwitchers
    {
        get
        {
            return _isAppShownInSwitchersProperty;
        }
        set
        {
            _isAppShownInSwitchersProperty = value;
            UpdateAppShownInSwitcher();
        }
    }

    private string _idProperty = string.Empty;
    public string Id
    {
        get
        {
            return _idProperty;
        }
        set
        {
            _idProperty = value;
            NewIdSet?.Invoke();
        }
    }

    private TitleBarModes _titleBarMode = TitleBarModes.Custom;
    public TitleBarModes TitleBarMode
    {
        get
        {
            return _titleBarMode;
        }
        set
        {
            _titleBarMode = value;
            SetTitleBarMode(TitleBarMode);
        }
    }
    private object _navigateBackParameterProperty = null;
    public object NavigateBackParameter
    {
        get => _navigateBackParameterProperty;
        private set => _navigateBackParameterProperty = value;
    }

    private Frame _mainFrameProperty;
    public Frame MainFrame
    {
        get => _mainFrameProperty;
        set => _mainFrameProperty = value;
    }

    public TemplateWindow()
    {
        this.Title = "CDPI UI";

        GetAppWindowAndPresenter();

        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
        }

        WindowsPositionHelper.SetWindowBorderMargin(this);

        TitleBarMode = (TitleBarModes)SettingsManager.Instance.GetValue<int>("APPEARANCE", "titleBarMode");

        Activated += DefaultWindow_Activated;

        if (((App)Application.Current).CurrentTheme == ElementTheme.Dark)
        {
            ApplyDarkThemeToSystemMenu();
        }

        UpdateWindowMinSize();
    }

    public void NavigateToPageWithParameter(Type page, NameValueCollection @params = null)
    {
        MainFrame?.Navigate(page, @params);
    }

    private void SetTitleBarMode(TitleBarModes mode)
    {

        switch (mode)
        {
            case TitleBarModes.System:
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (CustomTitleBarUserControl != null)
                        CustomTitleBarUserControl.Visibility = Visibility.Collapsed;
                });
                ExtendsContentIntoTitleBar = false;
                break;
            case TitleBarModes.Custom:
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (CustomTitleBarUserControl != null)
                        CustomTitleBarUserControl.Visibility = Visibility.Visible;
                });
                ExtendsContentIntoTitleBar = true;
                break;
        }
    }

    private void InitializeTitleBar()
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (_customTitleBarUserControlProperty == null)
                return;

            SetTitleBar(CustomTitleBarUserControl.WindowMoveAera);
            AssignActionHandlerToWindowIcon();
            CustomTitleBarUserControl.Visibility = TitleBarMode == TitleBarModes.Custom ? Visibility.Visible : Visibility.Collapsed;
            CustomTitleBarUserControl.Title = WindowTitle;
            if (!string.IsNullOrEmpty(IconUri))
                CustomTitleBarUserControl.IconSource = new BitmapImage(new Uri($"ms-appx:///{IconUri}"));
        });
    }

    public void DisableResizeFeature(bool isMinimizable = false)
    {
        var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
        appWindowPresenter.IsResizable = false;
        appWindowPresenter.IsMaximizable = false;
        appWindowPresenter.IsMinimizable = isMinimizable;

        NativeWindowHelper.ForceDisableMaximize(this);
    }

    // The handlers belong to the window and its own controls, so they need no removal on
    // Closed. Removed in the base Closed, which runs before a derived window cancels closing
    // (args.Handled), they left a window that stayed open without them.

    private void DefaultWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        UpdateWindowMinSize();

        if (CustomTitleBarUserControl?.AppTitleTextBlock == null)
            return;
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            AssignStateToTextBlock(CustomTitleBarUserControl.AppTitleTextBlock, true);
        }
        else
        {
            AssignStateToTextBlock(CustomTitleBarUserControl.AppTitleTextBlock, false);
        }
    }

    private static void AssignStateToTextBlock(TextBlock textBlock, bool isEnabled)
    {
        Style captionStyle = (Style)((App)Application.Current).Resources["AppBarTipTextBlockStyle"];
        if (textBlock.Style != captionStyle)
        {
            textBlock.Style = (Style)(isEnabled ? ((App)Application.Current).Resources["DefaultTextBlockStyle"] : ((App)Application.Current).Resources["DisabledTextBlockStyle"]);
        }
    }

    public void GetAppWindowAndPresenter()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow = AppWindow.GetFromWindowId(myWndId);
        OverlappedPresenter = AppWindow.Presenter as OverlappedPresenter;
    }

    private void UpdateAppShownInSwitcher()
    {
        AppWindow.IsShownInSwitchers = IsAppShownInSwitchers;
    }

    private long _lastTimestamp = 0;
    private readonly int _doubleClickTimeMS = 250;

    private void AssignActionHandlerToWindowIcon()
    {
        if (CustomTitleBarUserControl?.ImageAera == null)
            return;

        CustomTitleBarUserControl.ImageAera.PointerPressed += TitleIcon_PointerPressed;
    }

    private void TitleIcon_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Monotonic: a change of the system clock does not count as a double-click.
        long milliseconds = Environment.TickCount64;

        if (milliseconds - _lastTimestamp < _doubleClickTimeMS && milliseconds != 0)
        {
            this.Close();
            return;
        }
        else
        {
            _lastTimestamp = milliseconds;
        }

        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        GetWindowRect(hWnd, out RECT pos);
        IntPtr hMenu = GetSystemMenu(hWnd, false);
        int cmd = TrackPopupMenu(hMenu, 0x100, pos.left + 10, pos.top + 40, 0, hWnd, IntPtr.Zero);
        if (cmd > 0)
            SendMessage(hWnd, 0x112, (IntPtr)cmd, IntPtr.Zero);
    }

    private void UpdateWindowIcon()
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (CustomTitleBarUserControl != null)
                CustomTitleBarUserControl.IconSource = new BitmapImage(new Uri($"ms-appx:///{IconUri}"));
        });
        WindowsPositionHelper.SetWindowIcon(this, IconUri);
    }

    private void UpdateWindowMinSize()
    {
        MinWidth = (int)WindowMinSize.Width;
        MinHeight = (int)WindowMinSize.Height;
    }

    public static bool RemoveAndGoBackTo(Type pageType, Frame rootFrame)
    {
        if (rootFrame == null)
            return false;

        var back = rootFrame.BackStack;
        int targetIndex = -1;
        for (int i = back.Count - 1; i >= 0; i--)
        {
            if (back[i].SourcePageType == pageType)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex == -1)
            return false;

        for (int i = back.Count - 1; i > targetIndex; i--)
            back.RemoveAt(i);

        if (rootFrame.CanGoBack)
        {
            rootFrame.GoBack();
            return true;
        }
        return false;
    }

    public void NavigateBackWithParameter(object parameter)
    {
        NavigateBackParameter = parameter;
        MainFrame?.GoBack();
    }

    public void ClearNavigateBackParameter()
    {
        NavigateBackParameter = null;
    }

    public static void ToggleLoadingState(TaskbarProgressBarState loadingState, int currentLoadingValue = 0, int maxLoadingValue = 100)
    {
        TaskbarProgress.SetState(loadingState);
        if (loadingState != TaskbarProgressBarState.Indeterminate)
            TaskbarProgress.SetValue(currentLoadingValue, maxLoadingValue);
    }

    #region WINAPI


    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private const int GWLP_WNDPROC = -4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")]
    static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll")]
    static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
       int nReserved, IntPtr hWnd, IntPtr prcRect);
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    struct RECT { public int left, top, right, bottom; }

    private void ApplyDarkThemeToSystemMenu()
    {
        // Undocumented uxtheme exports: #135 is SetPreferredAppMode since Windows 10 1903 and
        // AllowDarkModeForApp(BOOL) on 1809, where 2 also means "allow". Older systems lack them.
        try
        {
            SetPreferredAppMode(2);
            FlushMenuThemes();
        }
        // The exports are missing on older systems: the system menu keeps its light theme.
        catch (EntryPointNotFoundException) { }
        catch (DllNotFoundException) { }
    }

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SetPreferredAppMode(int preferredAppMode);

    [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern void FlushMenuThemes();

    #endregion
}
