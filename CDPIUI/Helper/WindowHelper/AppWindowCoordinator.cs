#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CDPIUI.Helper.WindowHelper;

// Owns lookup/reuse rules over the same list exposed by App. Use on the UI thread.
internal sealed class AppWindowCoordinator<TWindow> where TWindow : class
{
    private readonly List<TWindow> _windows;
    private readonly Func<Task> _readiness;
    private readonly IAppWindowOperations<TWindow> _operations;

    public AppWindowCoordinator(List<TWindow> windows, Func<Task> readiness,
        IAppWindowOperations<TWindow> operations)
    {
        _windows = windows;
        _readiness = readiness;
        _operations = operations;
    }

    public TWindow? GetCurrent(Type windowType) =>
        _windows.Where(windowType.IsInstanceOfType).FirstOrDefault(_operations.HasDispatcher);

    public void CloseAll(Type windowType)
    {
        foreach (TWindow window in _windows.Where(windowType.IsInstanceOfType).ToList())
        {
            _operations.Close(window);
            _windows.Remove(window);
        }
    }

    public void CloseTemplate(Type windowType, string id)
    {
        TWindow? window = _windows.Where(windowType.IsInstanceOfType).FirstOrDefault(window =>
            _operations.TryGetTemplateId(window, out string? templateId) &&
            string.Equals(templateId, id, StringComparison.OrdinalIgnoreCase));
        if (window != null)
        {
            _operations.Close(window);
        }
    }

    public async Task<TWindow> CreateUnsafeAsync(Type windowType, bool activate, string id)
    {
        await _readiness();
        List<TWindow> findWindows = _windows.Where(window => windowType.IsInstanceOfType(window)).ToList();
        foreach (TWindow window in findWindows)
        {
            if (_operations.TryGetTemplateId(window, out string? templateId) &&
                !string.IsNullOrEmpty(templateId) &&
                string.Equals(templateId, id, StringComparison.OrdinalIgnoreCase))
            {
                if (activate)
                    _operations.Activate(window);
                return window;
            }
        }

        TWindow newWindow = _operations.Create(windowType);
        _operations.SetTemplateId(newWindow, id);
        _operations.SetPosition(newWindow);
        _operations.Register(newWindow, isUnsafe: true);
        if (activate)
            _operations.Activate(newWindow);
        return newWindow;
    }

    public async Task<TWindow> CreateSafeAsync(Type windowType, bool activate)
    {
        await _readiness();
        List<TWindow> findWindows = _windows.Where(window => windowType.IsInstanceOfType(window)).ToList();
        TWindow? activeFindWindow = findWindows.FirstOrDefault(_operations.HasDispatcher);
        if (activeFindWindow != null && findWindows.Count == 1)
        {
            if (activate)
                _operations.Activate(activeFindWindow);
            return activeFindWindow;
        }

        foreach (TWindow window in findWindows)
        {
            _operations.Close(window);
            _windows.Remove(window);
        }
        TWindow newWindow = _operations.Create(windowType);
        _operations.SetPosition(newWindow);
        _operations.Register(newWindow, isUnsafe: false);
        if (activate)
            _operations.Activate(newWindow);
        return newWindow;
    }
}
