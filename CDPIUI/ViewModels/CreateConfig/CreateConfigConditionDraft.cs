#nullable enable

using System;
using System.Collections.ObjectModel;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

internal sealed class CreateConfigConditionDraft : ObservableObject
{
    private readonly ObservableCollection<ConditionVariableModel> _conditions;
    private readonly Func<string, bool> _isNameCorrect;
    private readonly Func<string, string, string, string> _format;
    private readonly Func<string, string> _text;
    private readonly Action _enableExitAsk;
    private string _conditionName = string.Empty;
    private string _conditionDescription = string.Empty;
    private string _conditionOnValue = string.Empty;
    private string _conditionOffValue = string.Empty;

    public CreateConfigConditionDraft(ObservableCollection<ConditionVariableModel> conditions,
        Func<string, bool> isNameCorrect, Func<string, string, string, string> format,
        Func<string, string> text, Action enableExitAsk)
    {
        _conditions = conditions;
        _isNameCorrect = isNameCorrect;
        _format = format;
        _text = text;
        _enableExitAsk = enableExitAsk;
    }

    public string ConditionName
    {
        get => _conditionName;
        set { if (SetProperty(ref _conditionName, value ?? string.Empty)) OnConditionDraftChanged(); }
    }
    public string ConditionDescription
    {
        get => _conditionDescription;
        set => SetProperty(ref _conditionDescription, value ?? string.Empty);
    }
    public string ConditionOnValue
    {
        get => _conditionOnValue;
        set { if (SetProperty(ref _conditionOnValue, value ?? string.Empty)) OnConditionDraftChanged(); }
    }
    public string ConditionOffValue
    {
        get => _conditionOffValue;
        set { if (SetProperty(ref _conditionOffValue, value ?? string.Empty)) OnConditionDraftChanged(); }
    }
    public string ConditionPreview => IsConditionSaveAvailable
        ? _format(ConditionName.Replace(" ", ""), ConditionOnValue, ConditionOffValue)
        : _text("NothingToPreview");
    public bool IsConditionSaveAvailable =>
        _isNameCorrect(ConditionName.Replace(" ", "")) &&
        !string.IsNullOrEmpty(ConditionOnValue) &&
        !string.IsNullOrEmpty(ConditionOffValue);

    public void ResetConditionDraft()
    {
        ConditionName = "";
        ConditionDescription = "";
        ConditionOnValue = "";
        ConditionOffValue = "";
        OnPropertyChanged(nameof(ConditionPreview));
    }

    public void AddCondition()
    {
        _enableExitAsk();
        ConditionVariableModel model = new()
        {
            Name = ConditionName,
            Description = ConditionDescription,
            OnValue = ConditionOnValue,
            OffValue = ConditionOffValue,
        };
        _conditions.Add(model);
    }

    private void OnConditionDraftChanged() =>
        OnPropertiesChanged(nameof(ConditionPreview), nameof(IsConditionSaveAvailable));
}
