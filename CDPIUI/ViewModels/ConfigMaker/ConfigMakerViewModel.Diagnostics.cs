using System.Collections.ObjectModel;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Dispatching;

namespace CDPIUI.ViewModels.ConfigMaker;

// The diagnostics of the edited command and their counters in the status bar.
public sealed partial class ConfigMakerViewModel
{
    private readonly DispatcherQueueTimer _diagnosticsTimer;
    private readonly ConfigMakerDiagnostics<ConfigMakerDiagnosticViewModel> _diagnostics;

    public ObservableCollection<ConfigMakerDiagnosticViewModel> Diagnostics { get; } = [];

    public string ErrorCountText
    {
        get => _diagnostics.ErrorCountText;
        private set => _diagnostics.ErrorCountText = value;
    }

    public string WarningCountText
    {
        get => _diagnostics.WarningCountText;
        private set => _diagnostics.WarningCountText = value;
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

    private void RebuildDiagnostics() => _diagnostics.Rebuild();

    private ConfigMakerDiagnosticViewModel CreateDiagnosticViewModel(ComponentCommandDiagnostic diagnostic) => new()
    {
        Source = diagnostic,
        SeverityText = _diagnostics.GetSeverityText(diagnostic),
        Description = _diagnostics.GetDescription(diagnostic),
    };
}
