#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns the active help request and loading state. The host supplies the source and UI effects.
internal sealed class ConfigMakerCommandHelpLoader : ObservableObject
{
    private readonly ConfigMakerCommandHelpPanel _panel;
    private readonly Func<string> _currentComponent;
    private readonly Func<string, string> _localize;
    private readonly Func<string, CancellationToken, bool, Task<ComponentCommandHelpDocument>> _load;
    private readonly Action _rebuildStructure;
    private readonly Action _rebuildDiagnostics;
    private readonly Action<ComponentCommandHelpDocument> _showStatus;
    private CancellationTokenSource? _cancellation;
    private bool _isHelpLoading;
    private string _usageText = string.Empty;

    public ConfigMakerCommandHelpLoader(ConfigMakerCommandHelpPanel panel,
        Func<string> currentComponent, Func<string, string> localize,
        Func<string, CancellationToken, bool, Task<ComponentCommandHelpDocument>> load,
        Action rebuildStructure, Action rebuildDiagnostics, Action<ComponentCommandHelpDocument> showStatus)
    {
        _panel = panel;
        _currentComponent = currentComponent;
        _localize = localize;
        _load = load;
        _rebuildStructure = rebuildStructure;
        _rebuildDiagnostics = rebuildDiagnostics;
        _showStatus = showStatus;
    }

    public bool IsHelpLoading
    {
        get => _isHelpLoading;
        private set
        {
            if (SetProperty(ref _isHelpLoading, value))
                OnPropertyChanged(nameof(IsCommandListEnabled));
        }
    }

    public bool IsCommandListEnabled => !_isHelpLoading;
    public string UsageText
    {
        get => _usageText;
        private set => SetProperty(ref _usageText, value);
    }

    public void Cancel() => _cancellation?.Cancel();

    public async Task LoadAsync(string componentId, bool forceRefresh)
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _cancellation.Token;

        IsHelpLoading = true;
        UsageText = _localize("ConfigMakerLoadingHelpMessage");
        try
        {
            ComponentCommandHelpDocument document = await _load(componentId, cancellationToken, forceRefresh);
            if (cancellationToken.IsCancellationRequested ||
                !string.Equals(_currentComponent(), componentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _panel.Document = document;
            UsageText = !string.IsNullOrWhiteSpace(document.Usage)
                ? document.Usage
                : string.Format(_localize("ConfigMakerHelpOptionsLoadedMessage"), document.Options.Count);
            _panel.RebuildCommandModules();
            _panel.RebuildCommandOptions();
            _rebuildStructure();
            _rebuildDiagnostics();
            _showStatus(document);
        }
        catch (OperationCanceledException)
        {
            // A newer request or unloading canceled the load: nothing is left to show.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsHelpLoading = false;
        }
    }
}
