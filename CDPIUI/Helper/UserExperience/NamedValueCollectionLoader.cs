using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Loads assignments and named rows with per-item recovery for the named rows.</summary>
internal static class NamedValueCollectionLoader
{
    public static void Load<T>(ICollection<T> destination, Func<IEnumerable<string>> readAssignments,
        Func<string, bool> skipAssignment, Func<string, string, T> createAssignment,
        Func<IEnumerable<KeyValuePair<string, string>>> readNamedValues,
        Func<KeyValuePair<string, string>, T> createNamedValue, Action<Exception> reportNamedValueError)
    {
        destination.Clear();
        if (readAssignments() != null)
        {
            foreach (string assignment in readAssignments())
            {
                if (skipAssignment(assignment))
                    continue;
                int separator = assignment.IndexOf('=');
                if (separator < 0)
                    continue;
                destination.Add(createAssignment(assignment[..separator], assignment[(separator + 1)..]));
            }
        }

        if (readNamedValues() != null)
        {
            foreach (var pair in readNamedValues())
            {
                try
                {
                    destination.Add(createNamedValue(pair));
                }
                catch (Exception error)
                {
                    reportNamedValueError(error);
                }
            }
        }
    }
}
