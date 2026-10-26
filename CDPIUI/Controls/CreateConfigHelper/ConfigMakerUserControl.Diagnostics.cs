using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CDPIUI.Controls.CreateConfigHelper;

// The diagnostics list and the bottom panel tabs.
public sealed partial class ConfigMakerUserControl
{
    public void ShowOutputTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(0);
    }

    public void ShowDiagnosticsTab()
    {
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(1);
    }

    public void ShowPresetGroupsTab()
    {
        if (!HasPresetGroups)
        {
            return;
        }
        SetBottomPanelVisible(true);
        BottomSelector.SelectIndex(BottomSelector.Items.IndexOf(PresetGroupsSelectorItem));
    }

    private void ScheduleDiagnostics()
    {
        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Start();
    }

    private void DiagnosticsTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        RebuildDiagnostics();
    }

    private void RebuildDiagnostics()
    {
        List<ComponentCommandDiagnostic> results = ComponentCommandValidationService
            .Validate(CommandEditor.Text, _helpDocument.Options)
            .ToList();
        ConfigMakerPresetReferenceValidator.AddDiagnostics(
            CommandEditor.Text,
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
        ErrorCountTextBlock.Text = string.Format(
            _localizer.GetLocalizedString("ConfigMakerDiagnosticErrorCount"),
            errorCount);
        WarningCountTextBlock.Text = string.Format(
            _localizer.GetLocalizedString("ConfigMakerDiagnosticWarningCount"),
            warningCount);
        ApplyEditorTheme();
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

    private void DiagnosticsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void DiagnosticsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DiagnosticsListView.SelectedItem is ConfigMakerDiagnosticViewModel diagnostic)
        {
            GoToDiagnostic(diagnostic);
        }
    }

    private void GoToDiagnostic(ConfigMakerDiagnosticViewModel diagnostic)
    {
        CommandEditor.SetCursorPosition(
            Math.Max(0, diagnostic.Source.Line - 1),
            Math.Max(0, diagnostic.Source.Column - 1));
        CommandEditor.Focus(FocusState.Programmatic);
    }

    private void DiagnosticsStatusButton_Click(object sender, RoutedEventArgs e) =>
        ShowDiagnosticsTab();
}
