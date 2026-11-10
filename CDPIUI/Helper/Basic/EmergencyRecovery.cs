using System;

namespace CDPIUI.Helper.Basic;

/// <summary>Optional diagnostics must not prevent an exception from reaching the error UI.</summary>
internal static class EmergencyRecovery
{
    internal static void BestEffort(Action action)
    {
        try { action(); }
        catch (Exception exception) when (IsRecoverable(exception)) { }
    }

    internal static T Read<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (Exception exception) when (IsRecoverable(exception)) { return fallback; }
    }

    internal static string Describe(Exception exception)
    {
        string type = exception.GetType().FullName ?? exception.GetType().Name;
        string description = Read(exception.ToString, type);
        return string.IsNullOrWhiteSpace(description) ? type : description;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
