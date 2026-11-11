using System;
using System.Threading.Tasks;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The test run of the edited command in the selected component.
public sealed partial class ConfigMakerViewModel
{
    private readonly ConfigMakerTestSession _testSession;

    public event EventHandler TestStateChanged;

    public bool IsTesting => _testSession.IsTesting;

    public async Task StartTestAsync()
    {
        if (string.IsNullOrWhiteSpace(ComponentId))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerSelectComponentMessage"),
                InfoBarSeverity.Warning);
            return;
        }

        if (!TryResolveTestArguments(out string arguments))
        {
            return;
        }

        await _testSession.StartAsync(arguments);
    }

    public Task StopTestAsync(bool showCompletionMessage = false) => _testSession.StopAsync(showCompletionMessage);

    private bool TryResolveTestArguments(out string arguments)
    {
        try
        {
            SyncDocumentFromEditor();
            arguments = ComponentCommandLineFormatter.ToSingleLine(
                ConfigMakerPresetStorageService.CreateResolvedCommandForTest(_presetDocument));
        }
        catch (Exception exception)
        {
            arguments = string.Empty;
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerResolvePresetFailedMessage"),
                    exception.Message),
                InfoBarSeverity.Error);
            return false;
        }
        if (string.IsNullOrWhiteSpace(arguments))
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerEmptyArgumentsMessage"),
                InfoBarSeverity.Warning);
            return false;
        }
        return true;
    }

    private Task<bool> ConfirmReplaceRunningComponentAsync() => _view.ConfirmAsync(
        _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentTitle"),
        _localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentMessage"),
        _localizer.GetLocalizedString("Continue"));

    private void ShowTestNotice(ConfigMakerTestNotice notice, string? details)
    {
        (string message, InfoBarSeverity severity) = notice switch
        {
            ConfigMakerTestNotice.Started => (_localizer.GetLocalizedString("ConfigMakerTestStartedMessage"), InfoBarSeverity.Informational),
            ConfigMakerTestNotice.Stopped => (_localizer.GetLocalizedString("ConfigMakerTestStoppedMessage"), InfoBarSeverity.Informational),
            ConfigMakerTestNotice.Exited => (_localizer.GetLocalizedString("ConfigMakerTestExitedMessage"), InfoBarSeverity.Warning),
            ConfigMakerTestNotice.NotInstalled => (_localizer.GetLocalizedString("TargetComponentNotInstalled"), InfoBarSeverity.Error),
            ConfigMakerTestNotice.StartFailed => (string.Format(_localizer.GetLocalizedString("ConfigMakerTestStartFailedMessage"), details), InfoBarSeverity.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(notice)),
        };
        ShowEditorMessage(message, severity);
    }

    private void RaiseTestStateChanged()
    {
        OnPropertyChanged(nameof(IsTesting));
        TestStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
