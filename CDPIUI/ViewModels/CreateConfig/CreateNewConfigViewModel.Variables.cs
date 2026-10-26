using System;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.LScript;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// The conditions flyout and the variables list.
public sealed partial class CreateNewConfigViewModel
{
    private string _conditionName = string.Empty;
    private string _conditionDescription = string.Empty;
    private string _conditionOnValue = string.Empty;
    private string _conditionOffValue = string.Empty;

    public string ConditionName
    {
        get => _conditionName;
        set
        {
            if (SetProperty(ref _conditionName, value ?? string.Empty))
                OnConditionDraftChanged();
        }
    }

    public string ConditionDescription
    {
        get => _conditionDescription;
        set => SetProperty(ref _conditionDescription, value ?? string.Empty);
    }

    public string ConditionOnValue
    {
        get => _conditionOnValue;
        set
        {
            if (SetProperty(ref _conditionOnValue, value ?? string.Empty))
                OnConditionDraftChanged();
        }
    }

    public string ConditionOffValue
    {
        get => _conditionOffValue;
        set
        {
            if (SetProperty(ref _conditionOffValue, value ?? string.Empty))
                OnConditionDraftChanged();
        }
    }

    public string ConditionPreview => IsConditionSaveAvailable
        ? LScriptLangHelper.CreateCondition(ConditionName.Replace(" ", ""), ConditionOnValue, ConditionOffValue)
        : _localizer.GetLocalizedString("NothingToPreview");

    public bool IsConditionSaveAvailable =>
        CreateConfigPageHelper.IsNameCorrect(ConditionName.Replace(" ", "")) &&
        !string.IsNullOrEmpty(ConditionOnValue) &&
        !string.IsNullOrEmpty(ConditionOffValue);

    /// <summary>Clears the fields of the condition flyout.</summary>
    public void ResetConditionDraft()
    {
        ConditionName = "";
        ConditionDescription = "";
        ConditionOnValue = "";
        ConditionOffValue = "";
        OnPropertyChanged(nameof(ConditionPreview));
    }

    /// <summary>Adds the condition from the flyout fields.</summary>
    public void AddCondition()
    {
        _view.SetExitAskAvailable(true);
        ConditionVariableModel model = new()
        {
            Name = ConditionName,
            Description = ConditionDescription,
            OnValue = ConditionOnValue,
            OffValue = ConditionOffValue,
        };
        Conditions.Add(model);
    }

    private void OnConditionDraftChanged() =>
        OnPropertiesChanged(nameof(ConditionPreview), nameof(IsConditionSaveAvailable));

    private void RemoveCondition(object parameter)
    {
        if (parameter is ConditionVariableModel vm && Conditions.Contains(vm))
        {
            Conditions.Remove(vm);
        }
    }

    private void RemoveVariable(object parameter)
    {
        if (parameter is VariableModel vm && Variables.Contains(vm))
        {
            Variables.Remove(vm);
        }
    }

    private void VariableChangedValue(Tuple<string, string> parameter)
    {
        _view.SetExitAskAvailable(true);
        Logger.Instance.CreateDebugLog(LogSource, $"Variable changed: {parameter.Item1} to {parameter.Item2}");
        foreach (var variable in Variables)
        {
            if (variable.Name == parameter.Item1)
            {
                variable.Value = parameter.Item2;
                break;
            }
        }
    }
}
