using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.AddOns.BlockCheck2;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>
/// State of the BlockCheck2 preset review page: the expert editing switch and notice, and
/// saving the reviewed preset to the application. The draft is in <see cref="Result"/>,
/// which the result window shares with the result page.
/// </summary>
public sealed partial class BlockCheck2PresetReviewViewModel : ObservableObject
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly BlockCheck2PresetStorageService _presetStorageService = new();
    private readonly IBlockCheck2PresetReviewPageView _view;
    private bool _updatingEditingToggle;
    private bool _savingPreset;
    private bool? _isExpertEditingChecked = false;
    private bool _isExpertNoticeOpen;
    private bool _isSaving;

    public BlockCheck2PresetReviewViewModel(IBlockCheck2PresetReviewPageView view)
    {
        _view = view;
    }

    public BlockCheck2ResultViewModel? Result { get; private set; }

    /// <summary>Two-way: clicking the check box changes it before the click is handled.</summary>
    public bool? IsExpertEditingChecked
    {
        get => _isExpertEditingChecked;
        set => SetProperty(ref _isExpertEditingChecked, value);
    }

    /// <summary>Two-way: the user can close the notice.</summary>
    public bool IsExpertNoticeOpen
    {
        get => _isExpertNoticeOpen;
        set => SetProperty(ref _isExpertNoticeOpen, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    public void Load(BlockCheck2ResultViewModel result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (Result != null)
        {
            Result.Draft.PropertyChanged -= Draft_PropertyChanged;
        }
        Result = result;
        OnPropertyChanged(nameof(Result));
        Result.Draft.PropertyChanged += Draft_PropertyChanged;
        _view.LoadDraft(Result.Draft);
        UpdateState();
    }

    public void OnNavigatedFrom()
    {
        if (Result != null)
        {
            Result.Draft.PropertyChanged -= Draft_PropertyChanged;
        }
    }

    private void Draft_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        UpdateState();

    private void UpdateState()
    {
        if (Result == null)
        {
            return;
        }
        BlockCheck2PresetDraft draft = Result.Draft;
        _updatingEditingToggle = true;
        IsExpertEditingChecked = draft.IsExpertEditingEnabled;
        _updatingEditingToggle = false;
        IsExpertNoticeOpen = draft.IsExpertEditingEnabled;
        _view.NotifyMenuStateChanged();
    }

    /// <summary>Runs after a click on the check box, which has already changed its state.</summary>
    public async Task ApplyExpertEditingChoiceAsync()
    {
        if (_updatingEditingToggle || Result == null)
        {
            return;
        }
        bool enable = IsExpertEditingChecked == true;
        if (enable && !await _view.ConfirmExpertEditingAsync())
        {
            _updatingEditingToggle = true;
            IsExpertEditingChecked = false;
            _updatingEditingToggle = false;
            return;
        }
        _view.SetExpertEditing(enable);
        UpdateState();
    }

    /// <summary>
    /// Discards the expert changes after asking when there are any. Returns <see langword="true"/>
    /// when the view goes back to the preset builder.
    /// </summary>
    public async Task<bool> DiscardExpertChangesAsync()
    {
        if (Result == null)
        {
            return false;
        }
        if (Result.Draft.HasExpertChanges && !await _view.ConfirmDiscardExpertChangesAsync())
        {
            return false;
        }
        Result.Draft.DiscardExpertChanges();
        return true;
    }

    public async Task SaveToApplicationAsync()
    {
        if (Result == null || !Result.Draft.CanUseConfig || _savingPreset)
        {
            return;
        }

        string? presetName = await _view.AskPresetNameAsync();
        if (presetName == null)
        {
            return;
        }

        _savingPreset = true;
        IsSaving = true;
        UpdateState();
        try
        {
            BlockCheck2PresetStorageResult result = await _presetStorageService.SaveAsync(
                presetName,
                Result.Draft);
            if (!result.Success)
            {
                _view.ShowStatus(
                    BlockCheck2NoticeSeverity.Error,
                    Text("BlockCheck2PresetSaveFailedTitle"),
                    PresetSaveErrorMessage(result));
                return;
            }

            _view.ShowStatus(
                BlockCheck2NoticeSeverity.Success,
                Text("BlockCheck2PresetSavedTitle"),
                string.Format(
                    Text("BlockCheck2PresetSavedMessageFormat"),
                    result.PresetName,
                    result.CopiedFileCount));

            // Not awaited: the configs are refreshed while the dialog is open. The view logs its own errors.
            _ = _view.ShowSaveCompletedAsync();

            try
            {
                ComponentItemsLoaderHelper.Instance
                    .GetComponentHelperFromId(HardcodedItemIds.ComponentIds[Components.Zapret2])
                    ?.ReInitConfigs();
            }
            catch (Exception exception)
            {
                _view.ShowStatus(
                    BlockCheck2NoticeSeverity.Warning,
                    Text("BlockCheck2PresetSavedRefreshFailedTitle"),
                    exception.Message);
            }
        }
        catch (Exception exception)
        {
            _view.ShowStatus(
                BlockCheck2NoticeSeverity.Error,
                Text("BlockCheck2PresetSaveFailedTitle"),
                exception.Message);
        }
        finally
        {
            _savingPreset = false;
            IsSaving = false;
            UpdateState();
        }
    }

    private string PresetSaveErrorMessage(BlockCheck2PresetStorageResult result) =>
        result.ErrorCode switch
        {
            "COMPONENT_UNAVAILABLE" => Text("BlockCheck2PresetSaveComponentUnavailableMessage"),
            "FILE_MISSING" => string.Format(
                Text("BlockCheck2PresetSaveFileMissingMessageFormat"),
                result.ErrorDetails),
            "SAVE_FAILED" => string.Format(
                Text("BlockCheck2PresetSaveStorageErrorMessageFormat"),
                result.ErrorDetails),
            "PRESET_EMPTY" => Text("BlockCheck2PresetSaveEmptyMessage"),
            _ => string.IsNullOrWhiteSpace(result.ErrorDetails)
                ? Text("BlockCheck2PresetSaveUnknownMessage")
                : result.ErrorDetails,
        };

    private string Text(string key) => _localizer.GetLocalizedString(key);
}
