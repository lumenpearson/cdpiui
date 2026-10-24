using System.Runtime.InteropServices;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// The system hot keys of the hot key triggers and the tasks each one runs. Not thread-safe:
/// <see cref="ConditionalLaunchEngine"/> calls it under its lock.
/// </summary>
internal sealed class ConditionalHotKeyRegistry
{
    public const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotKeyId = 0x4300;

    private readonly Dictionary<int, List<ConditionalTask>> _hotKeyTasks = [];
    private int _nextHotKeyId = FirstHotKeyId;

    /// <summary>
    /// The tasks of the hot key <paramref name="id"/>, as a copy, or null if it is not registered.
    /// </summary>
    public List<ConditionalTask>? GetTasks(int id)
    {
        return _hotKeyTasks.TryGetValue(id, out var registered)
            ? registered.ToList()
            : null;
    }

    public void Register(IntPtr windowHandle, IEnumerable<ConditionalTask> tasks)
    {
        // Hot keys need the message window of Start; registered before it, they had no
        // window to send WM_HOTKEY to.
        if (windowHandle == IntPtr.Zero)
            return;

        var groups = tasks
            .SelectMany(task => task.Triggers
                .Where(trigger => trigger.Type == ConditionalTriggerType.HotKey)
                .Select(trigger => new TriggerBinding(task, trigger, string.Empty)))
            .GroupBy(binding => ConditionalHotKeyParser.GetHotKeyIdentity(binding.Trigger), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var first = group.First();
            if (!ConditionalHotKeyParser.TryGetHotKey(first.Trigger, out var modifiers, out var virtualKey))
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Invalid hot key in task '{first.Task.Name}'.");
                continue;
            }

            var id = _nextHotKeyId++;
            if (!RegisterHotKey(windowHandle, id, modifiers | ModNoRepeat, virtualKey))
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Cannot register hot key for task '{first.Task.Name}'. Win32: {Marshal.GetLastWin32Error()}.");
                continue;
            }

            _hotKeyTasks[id] = group
                .Select(binding => binding.Task)
                .DistinctBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Registered hot key '{ConditionalHotKeyParser.GetHotKeyIdentity(first.Trigger)}' for {_hotKeyTasks[id].Count} conditional task(s).");
        }
    }

    public void UnregisterAll(IntPtr windowHandle)
    {
        foreach (var id in _hotKeyTasks.Keys)
            UnregisterHotKey(windowHandle, id);
        _hotKeyTasks.Clear();
        // Ids above 0xBFFF are rejected; every reload used new ones.
        _nextHotKeyId = FirstHotKeyId;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
