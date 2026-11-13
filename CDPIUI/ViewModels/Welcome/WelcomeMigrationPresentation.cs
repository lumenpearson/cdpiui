#nullable enable

using System;
using System.Collections.Generic;

namespace CDPIUI.ViewModels.Welcome;

internal sealed class WelcomeMigrationPresentation<TSeverity> : ObservableObject
{
    private readonly TSeverity _errorSeverity;
    private readonly Func<string, string?> _text;
    private bool _isMigrationMode;
    private string? _migrationStatusTitle = string.Empty;
    private string? _migrationStatusMessage = string.Empty;
    private TSeverity _migrationStatusSeverity;
    private double _migrationProgress;
    private IReadOnlyList<MigrationComponentDisplayItem>? _migrationComponentItems;
    private bool _isMigrationComponentsListVisible = true;
    private string? _migrationSummaryText = string.Empty;
    private bool _isRetryMigrationVisible;

    public WelcomeMigrationPresentation(TSeverity initialSeverity, TSeverity errorSeverity,
        Func<string, string?> text)
    {
        _migrationStatusSeverity = initialSeverity;
        _errorSeverity = errorSeverity;
        _text = text;
    }

    public bool IsMigrationMode
    {
        get => _isMigrationMode;
        set
        {
            if (SetProperty(ref _isMigrationMode, value))
                OnPropertyChanged(nameof(IsReadyBundlesContentVisible));
        }
    }

    public bool IsReadyBundlesContentVisible => !_isMigrationMode;

    public string? MigrationStatusTitle
    {
        get => _migrationStatusTitle;
        set => SetProperty(ref _migrationStatusTitle, value);
    }

    public string? MigrationStatusMessage
    {
        get => _migrationStatusMessage;
        set => SetProperty(ref _migrationStatusMessage, value);
    }

    public TSeverity MigrationStatusSeverity
    {
        get => _migrationStatusSeverity;
        set => SetProperty(ref _migrationStatusSeverity, value);
    }

    public double MigrationProgress
    {
        get => _migrationProgress;
        set => SetProperty(ref _migrationProgress, value);
    }

    public IReadOnlyList<MigrationComponentDisplayItem>? MigrationComponentItems
    {
        get => _migrationComponentItems;
        set => SetProperty(ref _migrationComponentItems, value);
    }

    public bool IsMigrationComponentsListVisible
    {
        get => _isMigrationComponentsListVisible;
        set => SetProperty(ref _isMigrationComponentsListVisible, value);
    }

    public string? MigrationSummaryText
    {
        get => _migrationSummaryText;
        set => SetProperty(ref _migrationSummaryText, value);
    }

    public bool IsRetryMigrationVisible
    {
        get => _isRetryMigrationVisible;
        set => SetProperty(ref _isRetryMigrationVisible, value);
    }

    public void ShowStatus(string? titleKey, string? messageKey, TSeverity severity)
    {
        MigrationStatusTitle = _text($"/WelcomeWizard/{titleKey}");
        MigrationStatusMessage = _text($"/WelcomeWizard/{messageKey}");
        MigrationStatusSeverity = severity;
    }

    public void ShowError(string? message, string? errorCode)
    {
        // The migration session reports some failures with an English message; the code selects the localized text.
        string? localizedMessage = _text($"/WelcomeWizard/MigrationError_{errorCode}");
        if (!string.IsNullOrWhiteSpace(localizedMessage))
            message = localizedMessage;
        MigrationStatusTitle = _text("/WelcomeWizard/MigrationFailedTitle");
        MigrationStatusMessage = $"{message}\n\n{errorCode}";
        MigrationStatusSeverity = _errorSeverity;
        IsRetryMigrationVisible = true;
    }
}
