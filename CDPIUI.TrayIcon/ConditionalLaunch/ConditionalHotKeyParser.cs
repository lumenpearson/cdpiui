using System.Windows.Forms;
using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.TrayIcon.ConditionalLaunch;

/// <summary>
/// Reads the hot key of a hot key trigger.
/// </summary>
internal static class ConditionalHotKeyParser
{
    public static bool TryGetHotKey(
        ConditionalTrigger trigger,
        out uint modifiers,
        out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (!Enum.TryParse<ConditionalHotKeyModifiers>(
                trigger.GetParameter("modifiers"),
                ignoreCase: true,
                out var parsedModifiers) ||
            !Enum.TryParse<Keys>(
                trigger.GetParameter("key"),
                ignoreCase: true,
                out var key))
        {
            return false;
        }

        modifiers = (uint)parsedModifiers;
        virtualKey = (uint)key;
        return key != Keys.None;
    }

    // By value: "Control, Alt" and "Alt, Control" are the same hot key and were registered
    // twice, the second time failing.
    public static string GetHotKeyIdentity(ConditionalTrigger trigger)
    {
        return TryGetHotKey(trigger, out var modifiers, out var virtualKey)
            ? $"{modifiers}|{virtualKey}"
            : $"{trigger.GetParameter("modifiers")}|{trigger.GetParameter("key")}";
    }
}
