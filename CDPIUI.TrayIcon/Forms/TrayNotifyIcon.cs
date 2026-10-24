using System.Runtime.InteropServices;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using static CDPIUI.TrayIcon.Forms.TrayIconNativeMethods;

namespace CDPIUI.TrayIcon.Forms;

/// <summary>
/// The icon of the tray in the notification area: adds it, retries while the taskbar is not
/// ready, updates and removes it. Messages of the icon go to the window of <c>owner</c>.
/// </summary>
internal sealed class TrayNotifyIcon : IDisposable
{
    private readonly Control _owner;
    private readonly Guid _guid;
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 5000 };
    private bool _iconAdded;
    private int _iconAttempts;

    public TrayNotifyIcon(Control owner, Guid guid, Action retry)
    {
        _owner = owner;
        _guid = guid;
        _retryTimer.Tick += (_, _) => retry();
    }

    public bool IsAdded => _iconAdded;

    /// <summary>
    /// Forgets the icon, for example after the taskbar was created again without it.
    /// </summary>
    public void MarkRemoved() => _iconAdded = false;

    public void Add(bool notify, string iconName, string toolTip)
    {
        NOTIFYICONDATA data = new();

        data.cbSize = Marshal.SizeOf(data);
        data.hWnd = _owner.Handle;
        data.guidItem = _guid;
        data.uCallbackMessage = WM_MYMESSAGE;
        data.hIcon = LoadIcon(iconName);
        data.szTip = GetNormalToolTip(toolTip);

        data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                      NotifyFlags.NIF_SHOWTIP;

        try
        {
            _iconAttempts++;
            var result = Shell_NotifyIcon(NotifyCommand.NIM_ADD, ref data);
            int lastError = Marshal.GetLastWin32Error();
            // TaskbarCreated also occurs on DPI changes, when the icon may still exist.
            _iconAdded = result != 0 || Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data) != 0;
            if (!_iconAdded)
            {
                ReportPendingRegistration(notify, result, lastError);
                return;
            }

            _retryTimer.Stop();
            Logger.Instance.CreateDebugLog(nameof(EmptyForm), $"Tray icon registered after {_iconAttempts} attempt(s).");
            _iconAttempts = 0;
            data.uVersion = NOTIFYICON_VERSION_4;
            if (Shell_NotifyIcon(NotifyCommand.NIM_SETVERSION, ref data) == 0)
                Logger.Instance.CreateDebugLog(nameof(EmptyForm), "NIM_SETVERSION returned FALSE.");
        }
        finally
        {
            if (data.hIcon != IntPtr.Zero)
                DestroyIcon(data.hIcon);
        }
    }

    private void ReportPendingRegistration(bool notify, int result, int lastError)
    {
        _retryTimer.Start();
        if (_iconAttempts == 1 || _iconAttempts % 12 == 0)
            Logger.Instance.CreateDebugLog(nameof(EmptyForm),
                $"Tray registration pending: attempt={_iconAttempts}, NIM_ADD={result}, " +
                $"last-error snapshot=0x{lastError:X8} (Shell_NotifyIcon does not document GetLastError), " +
                $"taskbarPresent={FindWindow("Shell_TrayWnd", null) != IntPtr.Zero}, hwnd=0x{_owner.Handle:X}. Will retry.");

        if (notify && _iconAttempts == 1 && FindWindow("Shell_TrayWnd", null) != IntPtr.Zero)
        {
            try
            { NotifyHelper.Instance.ShowTrayErrorMessage($"0x{lastError:X8}"); }
            catch (Exception ex)
            {
                Logger.Instance.CreateDebugLog(nameof(EmptyForm), $"Tray error notification failed: {ex}");
            }
        }
    }

    public void Update(string iconName, string toolTip)
    {
        if (string.IsNullOrEmpty(iconName) || !_iconAdded)
            return;

        NOTIFYICONDATA data = new();

        data.cbSize = Marshal.SizeOf(data);
        data.hWnd = _owner.Handle;
        data.guidItem = _guid;
        data.uCallbackMessage = WM_MYMESSAGE;
        data.hIcon = LoadIcon(iconName);
        data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                      NotifyFlags.NIF_SHOWTIP;
        data.szTip = GetNormalToolTip(toolTip);

        try
        {
            if (Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data) == 0)
            {
                _iconAdded = false;
                _retryTimer.Start();
            }
        }
        finally
        {
            if (data.hIcon != IntPtr.Zero)
                DestroyIcon(data.hIcon);
        }
    }

    public void Delete()
    {
        NOTIFYICONDATA data = new NOTIFYICONDATA();
        data.cbSize = Marshal.SizeOf(data);
        data.uFlags = NotifyFlags.NIF_GUID;
        data.guidItem = _guid;

        Shell_NotifyIcon(NotifyCommand.NIM_DELETE, ref data);
    }

    public RECT GetRect()
    {
        NOTIFYICONIDENTIFIER notifyIcon = new NOTIFYICONIDENTIFIER();

        notifyIcon.cbSize = Marshal.SizeOf(notifyIcon);

        notifyIcon.guidItem = _guid;
        Shell_NotifyIconGetRect(ref notifyIcon, out RECT rect);

        return rect;
    }

    public void Dispose()
    {
        _retryTimer.Dispose();
    }

    private static nint LoadIcon(string name)
    {
        var icon = Utils.GetBitmapFromResourses($"CDPIUI.TrayIcon.Assets.{name}.ico");
        if (icon != null)
        {
            nint result = icon.GetHicon();
            icon.Dispose();

            return result;
        }
        return nint.Zero;
    }

    private static string GetNormalToolTip(string? toolTip)
    {
        return "CDPI UI" + (string.IsNullOrEmpty(toolTip) ? string.Empty : $"\n{toolTip}");
    }
}
