using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using static CDPIUI.Helper.WindowHelper.Win32WindowApi;

namespace CDPIUI.Helper.WindowHelper;

/// <summary>
/// Makes a window modal: disables its owners and restores them when the modal window
/// closes or is made normal again. Must be used on the UI thread only.
/// </summary>
internal sealed class ModalWindowCoordinator
{
    private readonly List<Window> _openWindows;
    private readonly Dictionary<Window, ModalSession> _modalSessions = new();
    private readonly Dictionary<Window, DisabledOwner> _disabledOwners = new();

    public ModalWindowCoordinator(List<Window> openWindows)
    {
        _openWindows = openWindows;
    }

    private sealed class DisabledOwner
    {
        public IntPtr Handle;
        public bool WasEnabled;
        public int Count;
    }

    private sealed class ModalSession
    {
        public IntPtr Handle;
        public IntPtr PreviousOwner;
        public Window Owner;
        public readonly List<Window> Disabled = new();
        public readonly TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Windows.Foundation.TypedEventHandler<object, WindowEventArgs> ClosedHandler;
    }

    public Task BeginModal(Window modalWindow, Window ownerWindow)
    {
        if (_modalSessions.TryGetValue(modalWindow, out var existing))
            return existing.Completion.Task;
        if (!_openWindows.Contains(modalWindow))
            return Task.CompletedTask;
        var handle = WindowNative.GetWindowHandle(modalWindow);
        if (!IsWindow(handle))
            return Task.CompletedTask;
        var session = new ModalSession { Handle = handle, Owner = ownerWindow };
        _modalSessions.Add(modalWindow, session);
        try
        {
            if (ownerWindow != null && _openWindows.Contains(ownerWindow))
            {
                var ownerHandle = WindowNative.GetWindowHandle(ownerWindow);
                if (IsWindow(ownerHandle))
                {
                    session.PreviousOwner = GetWindowLongPtr(handle, GWLP_HWNDPARENT);
                    SetWindowLongPtr(handle, GWLP_HWNDPARENT, ownerHandle);
                }
            }
            var owners = ownerWindow == null ? _openWindows.ToArray() : new[] { ownerWindow };
            foreach (var owner in owners)
            {
                if (owner == modalWindow || !_openWindows.Contains(owner))
                    continue;
                var ownerHandle = WindowNative.GetWindowHandle(owner);
                if (!IsWindow(ownerHandle))
                    continue;
                if (!_disabledOwners.TryGetValue(owner, out var state))
                {
                    state = new DisabledOwner { Handle = ownerHandle, WasEnabled = IsWindowEnabled(ownerHandle) };
                    _disabledOwners.Add(owner, state);
                }
                state.Count++;
                session.Disabled.Add(owner);
                EnableWindow(ownerHandle, false);
            }
            session.ClosedHandler = (_, args) =>
            {
                if (!args.Handled)
                    ReleaseModal(modalWindow, isClosing: true);
            };
            modalWindow.Closed += session.ClosedHandler;
            return session.Completion.Task;
        }
        catch
        {
            ReleaseModal(modalWindow, isClosing: false);
            throw;
        }
    }

    public void ReleaseModal(Window modalWindow, bool isClosing)
    {
        if (!_modalSessions.Remove(modalWindow, out var session))
            return;
        if (session.ClosedHandler != null)
            modalWindow.Closed -= session.ClosedHandler;
        try
        {
            // Never show, reposition or query a modal window after Closed.
            if (!isClosing && session.Owner != null && IsWindow(session.Handle))
                SetWindowLongPtr(session.Handle, GWLP_HWNDPARENT,
                    IsWindow(session.PreviousOwner) ? session.PreviousOwner : IntPtr.Zero);
            foreach (var owner in session.Disabled)
            {
                if (!_disabledOwners.TryGetValue(owner, out var state))
                    continue;
                if (--state.Count > 0)
                    continue;
                _disabledOwners.Remove(owner);
                if (state.WasEnabled && _openWindows.Contains(owner) && IsWindow(state.Handle))
                    EnableWindow(state.Handle, true);
            }
            if (session.Owner != null && _openWindows.Contains(session.Owner))
            {
                var ownerHandle = WindowNative.GetWindowHandle(session.Owner);
                if (IsWindow(ownerHandle) && IsWindowEnabled(ownerHandle))
                    SetForegroundWindow(ownerHandle);
            }
        }
        finally { session.Completion.TrySetResult(true); }
    }

    /// <summary>
    /// Drops the disabled state that a closed window still holds as an owner.
    /// </summary>
    public void ForgetOwner(Window window)
    {
        _disabledOwners.Remove(window);
    }
}
