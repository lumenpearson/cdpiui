using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Dispatching;

namespace CDPIUI.ViewModels.ConfigMaker;

// The diagnostics of the edited command and their counters in the status bar.
public sealed partial class ConfigMakerViewModel
{
    private readonly DispatcherQueueTimer _diagnosticsTimer;
    private string _errorCountText = string.Empty;
    private string _warningCountText = string.Empty;

    public ObservableCollection<ConfigMakerDiagnosticViewModel> Diagnostics { get; } = [];

    public string ErrorCountText
    {
        get => _errorCountText;
        private set => SetProperty(ref _errorCountText, value);
    }

    public string WarningCountText
    {
        get => _warningCountText;
        private set => SetProperty(ref _warningCountText, value);
    }

    private void ScheduleDiagnostics()
    {
        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Start();
    }

    private void DiagnosticsTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        RebuildDiagnostics();
    }

    private void RebuildDiagnostics()
    {
        string editorText = _view.EditorText;
        List<ComponentCommandDiagnostic> results = ComponentCommandValidationService
            .Validate(editorText, _helpDocument.Options)
            .ToList();
        ConfigMakerPresetReferenceValidator.AddDiagnostics(
            editorText,
            _presetDocument.Variables,
            reference => _presetResources.FindAttached(reference) != null,
            results);

        Diagnostics.Clear();
        foreach (ComponentCommandDiagnostic diagnostic in results)
        {
            Diagnostics.Add(CreateDiagnosticViewModel(diagnostic));
        }

        int errorCount = results.Count(item => item.Severity == ComponentCommandDiagnosticSeverity.Error);
        int warningCount = results.Count - errorCount;
        ErrorCountText = string.Format(
            _localizer.GetLocalizedString("ConfigMakerDiagnosticErrorCount"),
            errorCount);
        WarningCountText = string.Format(
            _localizer.GetLocalizedString("ConfigMakerDiagnosticWarningCount"),
            warningCount);
        _view.RefreshEditorHighlighting();
    }

    private ConfigMakerDiagnosticViewModel CreateDiagnosticViewModel(ComponentCommandDiagnostic diagnostic) => new()
    {
        Source = diagnostic,
        SeverityText = _localizer.GetLocalizedString(
            diagnostic.Severity == ComponentCommandDiagnosticSeverity.Error
                ? "ConfigMakerDiagnosticErrorSeverity"
                : "ConfigMakerDiagnosticWarningSeverity"),
        Description = diagnostic.Kind switch
        {
            ComponentCommandDiagnosticKind.UnknownFlag => string.Format(
                _localizer.GetLocalizedString("ConfigMakerUnknownFlagDiagnostic"),
                diagnostic.Token),
            ComponentCommandDiagnosticKind.MissingRequiredArgument => string.Format(
                _localizer.GetLocalizedString("ConfigMakerMissingArgumentDiagnostic"),
                diagnostic.Token),
            ComponentCommandDiagnosticKind.UnresolvedVariable => string.Format(
                _localizer.GetLocalizedString("ConfigMakerUnresolvedVariableDiagnostic"),
                diagnostic.Token),
            ComponentCommandDiagnosticKind.UnresolvedResource => string.Format(
                _localizer.GetLocalizedString("ConfigMakerUnresolvedResourceDiagnostic"),
                diagnostic.Token),
            _ => _localizer.GetLocalizedString("ConfigMakerUnterminatedQuoteDiagnostic"),
        },
    };
}
