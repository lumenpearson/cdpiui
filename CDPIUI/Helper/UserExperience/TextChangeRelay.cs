using System;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Relays user input and programmatic replacements in their original callback order.</summary>
internal sealed class TextChangeRelay
{
    private readonly Func<bool> _isReadOnly;
    private readonly Func<string> _readText;
    private readonly Action<string> _writeText;
    private readonly Func<string, string> _replaceText;
    private readonly Action<string> _acceptText;
    private readonly Action _rebuild;
    private readonly Action _rebuildDiagnostics;
    private readonly Action _scheduleDiagnostics;
    private readonly Action<string> _notify;

    public TextChangeRelay(Func<bool> isReadOnly, Func<string> readText, Action<string> writeText,
        Func<string, string> replaceText, Action<string> acceptText, Action rebuild,
        Action rebuildDiagnostics, Action scheduleDiagnostics, Action<string> notify)
    {
        _isReadOnly = isReadOnly;
        _readText = readText;
        _writeText = writeText;
        _replaceText = replaceText;
        _acceptText = acceptText;
        _rebuild = rebuild;
        _rebuildDiagnostics = rebuildDiagnostics;
        _scheduleDiagnostics = scheduleDiagnostics;
        _notify = notify;
    }

    public bool IsUpdating { get; private set; }

    public void OnTextChanged(string text)
    {
        if (IsUpdating)
            return;
        if (_isReadOnly())
        {
            ReplaceText(_readText(), updateProperty: false);
            return;
        }

        IsUpdating = true;
        _writeText(text ?? string.Empty);
        IsUpdating = false;
        _acceptText(_readText());
        _rebuild();
        _notify(_readText());
        _scheduleDiagnostics();
    }

    public void ReplaceText(string text, bool updateProperty = true)
    {
        IsUpdating = true;
        string editorText = _replaceText(text ?? string.Empty);
        if (updateProperty)
            _writeText(editorText);
        _acceptText(editorText);
        IsUpdating = false;
        _rebuild();
        _rebuildDiagnostics();
        _notify(editorText);
    }
}
