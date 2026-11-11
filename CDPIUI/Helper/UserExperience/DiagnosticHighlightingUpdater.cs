using System;
using System.Collections.Generic;
using System.Linq;

namespace CDPIUI.Helper.UserExperience;

// Retain the last requested highlighting state, including a failed apply.
internal sealed class DiagnosticHighlightingUpdater
{
    private string _signature = string.Empty;

    public void Reset() => _signature = string.Empty;

    public void Update<T>(string background, bool isLight, Func<IEnumerable<T>> diagnostics,
        Func<T, bool> isError, Func<T, bool> isWarning, Func<T, string> token,
        Action<bool, string[], string[]> apply)
    {
        string[] errorTokens = diagnostics().Where(isError).Select(token).ToArray();
        string[] warningTokens = diagnostics().Where(isWarning).Select(token).ToArray();
        string newSignature = $"{background}|{isLight}|{string.Join(',', errorTokens)}|{string.Join(',', warningTokens)}";
        if (string.Equals(_signature, newSignature, StringComparison.Ordinal))
        {
            return;
        }

        _signature = newSignature;
        apply(isLight, errorTokens, warningTokens);
    }
}
