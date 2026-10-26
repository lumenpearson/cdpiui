using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// The state of each process trigger between two polls: whether its process ran, and its
/// pending delayed run. Not thread-safe: <see cref="ConditionalLaunchEngine"/> calls it under
/// its lock.
/// </summary>
internal sealed class ProcessTriggerStates
{
    private readonly Dictionary<string, ProcessTriggerState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Forgets the states of all tasks except <paramref name="unchangedTaskIds"/> and cancels
    /// their pending runs.
    /// </summary>
    public void ResetExcept(IReadOnlySet<string> unchangedTaskIds)
    {
        foreach (var key in _states.Keys.ToList())
        {
            if (unchangedTaskIds.Contains(key.Split('|')[0]))
                continue;
            _states[key].CancelPending();
            _states.Remove(key);
        }
    }

    public void Clear()
    {
        foreach (var state in _states.Values)
            state.CancelPending();
        _states.Clear();
    }

    /// <summary>
    /// Records whether the process of each trigger runs now.
    /// </summary>
    /// <returns>The triggers whose condition became true since the last poll</returns>
    public List<TriggerBinding> Update(IEnumerable<TriggerBinding> bindings, HashSet<string> runningProcessNames)
    {
        List<TriggerBinding> newlyMatched = [];
        foreach (var binding in bindings)
        {
            var trigger = binding.Trigger;
            var rawProcessName = trigger.GetParameter("processName");
            if (string.IsNullOrWhiteSpace(rawProcessName))
                continue;

            var processName = Path.GetFileNameWithoutExtension(rawProcessName.Trim());
            var isRunning = runningProcessNames.Contains(processName);

            if (!_states.TryGetValue(binding.StateKey, out var state))
            {
                state = new ProcessTriggerState(isRunning);
                _states[binding.StateKey] = state;
                continue;
            }

            var matched = trigger.Type switch
            {
                ConditionalTriggerType.ProcessStarted => !state.LastIsRunning && isRunning,
                ConditionalTriggerType.ProcessStopped => state.LastIsRunning && !isRunning,
                _ => false
            };

            var conditionStillValid = trigger.Type switch
            {
                ConditionalTriggerType.ProcessStarted => isRunning,
                ConditionalTriggerType.ProcessStopped => !isRunning,
                _ => true
            };

            if (!conditionStillValid)
                state.CancelPending();

            state.LastIsRunning = isRunning;
            if (matched)
                newlyMatched.Add(binding);
        }
        return newlyMatched;
    }

    /// <summary>
    /// Cancels the pending run of the trigger and starts a new one.
    /// </summary>
    /// <returns>The cancellation of the new run, or null if the trigger has no state any more</returns>
    public CancellationTokenSource? BeginPending(string stateKey)
    {
        if (!_states.TryGetValue(stateKey, out var state))
            return null;

        state.CancelPending();
        state.Pending = new CancellationTokenSource();
        return state.Pending;
    }

    /// <summary>
    /// Ends the pending run of the trigger, unless a newer run replaced it.
    /// </summary>
    public void EndPending(string stateKey, CancellationTokenSource cancellation)
    {
        if (_states.TryGetValue(stateKey, out var state) &&
            ReferenceEquals(state.Pending, cancellation))
        {
            state.Pending = null;
        }
    }
}
