using System.Runtime.InteropServices;

namespace CDPIUI.TrayIcon.Forms;

/// <summary>
/// Win32 declarations for the notification area icon and the messages of its window.
/// </summary>
internal static class TrayIconNativeMethods
{
    public const Int32 WM_MYMESSAGE = 0x8000; //WM_APP
    public const Int32 NOTIFYICON_VERSION_4 = 0x4;

    //messages
    public const Int32 WM_CONTEXTMENU = 0x7B;
    public const Int32 NIN_BALLOONHIDE = 0x403;
    public const Int32 NIN_BALLOONSHOW = 0x402;
    public const Int32 NIN_BALLOONTIMEOUT = 0x404;
    public const Int32 NIN_BALLOONUSERCLICK = 0x405;
    // NIN_SELECT | NINF_KEY; it was 0x403, so the icon did not react to Enter or Space.
    public const Int32 NIN_KEYSELECT = 0x401;
    public const Int32 NIN_SELECT = 0x400;
    public const Int32 NIN_POPUPOPEN = 0x406;
    public const Int32 NIN_POPUPCLOSE = 0x407;
    public const Int32 WM_LBUTTONDOWN = 0x0201;
    public const Int32 WM_CREATE = 0x0001;
    public const Int32 TASKBAR_INIT_COMPLETE = 0xC0AA;

    public const Int32 NIIF_USER = 0x4;
    public const Int32 NIIF_NONE = 0x0;
    public const Int32 NIIF_INFO = 0x1;
    public const Int32 NIIF_WARNING = 0x2;
    public const Int32 NIIF_ERROR = 0x3;
    public const Int32 NIIF_LARGE_ICON = 0x20;

    public enum NotifyFlags
    {
        NIF_MESSAGE = 0x01,
        NIF_ICON = 0x02,
        NIF_TIP = 0x04,
        NIF_INFO = 0x10,
        NIF_STATE = 0x08,
        NIF_GUID = 0x20,
        NIF_SHOWTIP = 0x80
    }

    public enum NotifyCommand { NIM_ADD = 0x0, NIM_DELETE = 0x2, NIM_MODIFY = 0x1, NIM_SETVERSION = 0x4 }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public Int32 cbSize;
        public IntPtr hWnd;
        public Int32 uID;
        public NotifyFlags uFlags;
        public Int32 uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public String szTip;
        public Int32 dwState;
        public Int32 dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public String szInfo;
        public Int32 uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public String szInfoTitle;
        public Int32 dwInfoFlags;
        public Guid guidItem; //> IE 6
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern System.Int32 Shell_NotifyIcon(NotifyCommand cmd, ref NOTIFYICONDATA data);

    [DllImport("User32.dll", SetLastError = true)]
    public static extern uint RegisterWindowMessage(String lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr changeInfo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr icon);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public Int32 left;
        public Int32 top;
        public Int32 right;
        public Int32 bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER
    {
        public Int32 cbSize;
        public IntPtr hWnd;
        public Int32 uID;
        public Guid guidItem;
    }

    //Works with Shell32.dll (version 6.1 or later)
    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int Shell_NotifyIconGetRect([In] ref NOTIFYICONIDENTIFIER identifier, [Out] out RECT iconLocation);
}
