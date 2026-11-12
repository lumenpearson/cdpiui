#nullable enable

using System;

namespace CDPIUI.ViewModels.BlockCheck2;

// Preserves selected-row presentation and synchronous publication order.
internal sealed class BlockCheck2ResultSelection : ObservableObject
{
    private readonly Func<BlockCheck2StrategyEvidenceItem?> _selectedItem;
    private readonly Func<bool> _hasSession, _testRunning, _manualRunning;
    private readonly Func<BlockCheck2StrategyEvidenceItem, bool> _canAddForList;
    private readonly Func<string, string> _text;
    private readonly Action _menu;

    private bool _isDetailsHitTestVisible = true;
    private bool _isTestStrategyEnabled = true;
    private bool _isManualTestEnabled = true;
    private bool? _isManualTestChecked = false;
    private string _manualTestLabel = string.Empty;
    private string _manualTestGlyph = "\uE768";
    private bool _isAddForSiteEnabled = true;
    private bool _isAddForListEnabled = true;
    private string _selectedStrategyName = string.Empty;
    private string _selectedStrategyId = string.Empty;
    private string _selectedStrategyArguments = string.Empty;
    private string _selectedSite = string.Empty;
    private string _selectedConnection = string.Empty;
    private string _selectedEvidence = string.Empty;
    private string _selectedBaseline = string.Empty;

    public BlockCheck2ResultSelection(Func<BlockCheck2StrategyEvidenceItem?> selectedItem,
        Func<bool> hasSession, Func<bool> testRunning, Func<bool> manualRunning,
        Func<BlockCheck2StrategyEvidenceItem, bool> canAddForList, Func<string, string> text, Action menu)
    {
        _selectedItem = selectedItem;
        _hasSession = hasSession;
        _testRunning = testRunning;
        _manualRunning = manualRunning;
        _canAddForList = canAddForList;
        _text = text;
        _menu = menu;
    }

    public bool IsDetailsHitTestVisible
    {
        get => _isDetailsHitTestVisible;
        set => SetProperty(ref _isDetailsHitTestVisible, value);
    }

    public bool IsTestStrategyEnabled
    {
        get => _isTestStrategyEnabled;
        set => SetProperty(ref _isTestStrategyEnabled, value);
    }

    public bool IsManualTestEnabled
    {
        get => _isManualTestEnabled;
        set => SetProperty(ref _isManualTestEnabled, value);
    }

    public bool? IsManualTestChecked
    {
        get => _isManualTestChecked;
        set => SetProperty(ref _isManualTestChecked, value);
    }

    public string ManualTestLabel
    {
        get => _manualTestLabel;
        set => SetProperty(ref _manualTestLabel, value);
    }

    public string ManualTestGlyph
    {
        get => _manualTestGlyph;
        set => SetProperty(ref _manualTestGlyph, value);
    }

    public bool IsAddForSiteEnabled
    {
        get => _isAddForSiteEnabled;
        set => SetProperty(ref _isAddForSiteEnabled, value);
    }

    public bool IsAddForListEnabled
    {
        get => _isAddForListEnabled;
        set => SetProperty(ref _isAddForListEnabled, value);
    }

    public string SelectedStrategyName
    {
        get => _selectedStrategyName;
        set => SetProperty(ref _selectedStrategyName, value);
    }

    public string SelectedStrategyId
    {
        get => _selectedStrategyId;
        set => SetProperty(ref _selectedStrategyId, value);
    }

    public string SelectedStrategyArguments
    {
        get => _selectedStrategyArguments;
        set => SetProperty(ref _selectedStrategyArguments, value);
    }

    public string SelectedSite
    {
        get => _selectedSite;
        set => SetProperty(ref _selectedSite, value);
    }

    public string SelectedConnection
    {
        get => _selectedConnection;
        set => SetProperty(ref _selectedConnection, value);
    }

    public string SelectedEvidence
    {
        get => _selectedEvidence;
        set => SetProperty(ref _selectedEvidence, value);
    }

    public string SelectedBaseline
    {
        get => _selectedBaseline;
        set => SetProperty(ref _selectedBaseline, value);
    }

    public void Update()
    {
        BlockCheck2StrategyEvidenceItem? item = _selectedItem();
        bool selected = item != null && _hasSession();
        IsDetailsHitTestVisible = selected && !_testRunning();
        IsTestStrategyEnabled = selected && !_testRunning();
        IsManualTestEnabled = selected && (!_testRunning() || _manualRunning());
        IsManualTestChecked = selected && _manualRunning();

        ManualTestLabel = IsManualTestChecked == true
            ? _text("StopTest")
            : _text("PresetTestConfirmButton");
        ManualTestGlyph = IsManualTestChecked == true
            ? "\uE769"
            : "\uE768";

        IsAddForSiteEnabled = selected && !_testRunning();
        IsAddForListEnabled = selected &&
            _canAddForList(item!) &&
            !_testRunning();
        _menu();
        if (item == null)
        {
            SelectedStrategyName = _text("BlockCheck2SelectStrategyText");
            SelectedStrategyId = string.Empty;
            SelectedStrategyArguments = string.Empty;
            SelectedSite = string.Empty;
            SelectedConnection = string.Empty;
            SelectedEvidence = string.Empty;
            SelectedBaseline = string.Empty;
            return;
        }

        SelectedStrategyName = item.StrategyName;
        SelectedStrategyId = item.StrategyId;
        SelectedStrategyArguments = item.StrategyArguments;
        SelectedSite = item.SiteUrl;
        SelectedConnection = item.ConnectionDetails;
        SelectedEvidence = item.EvidenceDetails;
        SelectedBaseline = string.Format(
            _text("BlockCheck2BaselineEvidenceFormat"),
            item.BaselineText);
    }

}
