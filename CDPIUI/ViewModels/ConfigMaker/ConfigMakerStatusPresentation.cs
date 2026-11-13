#nullable enable

using System;

namespace CDPIUI.ViewModels.ConfigMaker;

internal sealed class ConfigMakerStatusPresentation<TSeverity> : ObservableObject
{
    private readonly Func<TSeverity, string?> _resolveTitle;
    private readonly Action<TSeverity, string?, string?> _raiseNotice;
    private readonly Func<bool> _getInline;
    private string? _statusTitle = string.Empty;
    private string? _statusMessage = string.Empty;
    private TSeverity _statusSeverity;
    private bool _isStatusOpen;

    public ConfigMakerStatusPresentation(
        TSeverity initialSeverity,
        Func<TSeverity, string?> resolveTitle,
        Action<TSeverity, string?, string?> raiseNotice,
        Func<bool> getInline)
    {
        _statusSeverity = initialSeverity;
        _resolveTitle = resolveTitle;
        _raiseNotice = raiseNotice;
        _getInline = getInline;
    }

    public string? StatusTitle
    {
        get => _statusTitle;
        set => SetProperty(ref _statusTitle, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public TSeverity StatusSeverity
    {
        get => _statusSeverity;
        set => SetProperty(ref _statusSeverity, value);
    }

    public bool IsStatusOpen
    {
        get => _isStatusOpen;
        set => SetProperty(ref _isStatusOpen, value);
    }

    public void Show(string? message, TSeverity severity)
    {
        string? title = _resolveTitle(severity);
        _raiseNotice(severity, title, message);
        if (!_getInline())
        {
            IsStatusOpen = false;
            return;
        }

        StatusTitle = title;
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }
}
