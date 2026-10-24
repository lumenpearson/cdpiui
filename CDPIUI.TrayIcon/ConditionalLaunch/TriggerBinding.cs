using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// A trigger of a task, with the key of its state in the engine.
/// </summary>
internal sealed record TriggerBinding(
    ConditionalTask Task,
    ConditionalTrigger Trigger,
    string StateKey);
