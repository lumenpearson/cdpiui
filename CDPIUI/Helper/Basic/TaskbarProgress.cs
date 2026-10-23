#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CDPIUI.Helper.Basic
{
    public enum TaskbarProgressBarState
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8,
    }

    /// <summary>
    /// Shows the progress of a long operation on the taskbar button of the application.
    /// </summary>
    public static class TaskbarProgress
    {
        private static ITaskbarList3? _taskbar;

        public static void SetState(TaskbarProgressBarState state) =>
            Invoke((taskbar, window) => taskbar.SetProgressState(window, state));

        public static void SetValue(int current, int maximum) =>
            Invoke((taskbar, window) => taskbar.SetProgressValue(window, (ulong)Math.Max(current, 0), (ulong)Math.Max(maximum, 1)));

        private static void Invoke(Action<ITaskbarList3, IntPtr> action)
        {
            try
            {
                IntPtr window = Process.GetCurrentProcess().MainWindowHandle;
                if (window == IntPtr.Zero)
                    return;
                if (_taskbar == null)
                {
                    var taskbar = (ITaskbarList3)new TaskbarList();
                    taskbar.HrInit();
                    _taskbar = taskbar;
                }
                action(_taskbar, window);
            }
            catch (COMException)
            {
                // The taskbar progress is only a hint; Explorer may not run.
            }
        }

        [ComImport]
        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarList
        {
        }

        // ITaskbarList, ITaskbarList2 and ITaskbarList3 methods in vtable order.
        [ComImport]
        [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, TaskbarProgressBarState state);
        }
    }
}
