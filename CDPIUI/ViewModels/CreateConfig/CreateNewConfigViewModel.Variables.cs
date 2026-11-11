using System;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.LScript;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// The conditions flyout and the variables list.
public sealed partial class CreateNewConfigViewModel
{
    public string ConditionName
    {
        get => _conditionDraft.ConditionName;
        set => _conditionDraft.ConditionName = value;
    }

    public string ConditionDescription
    {
        get => _conditionDraft.ConditionDescription;
        set => _conditionDraft.ConditionDescription = value;
    }

    public string ConditionOnValue
    {
        get => _conditionDraft.ConditionOnValue;
        set => _conditionDraft.ConditionOnValue = value;
    }

    public string ConditionOffValue
    {
        get => _conditionDraft.ConditionOffValue;
        set => _conditionDraft.ConditionOffValue = value;
    }

    public string ConditionPreview => _conditionDraft.ConditionPreview;
    public bool IsConditionSaveAvailable => _conditionDraft.IsConditionSaveAvailable;

    /// <summary>Clears the fields of the condition flyout.</summary>
    public void ResetConditionDraft() => _conditionDraft.ResetConditionDraft();

    /// <summary>Adds the condition from the flyout fields.</summary>
    public void AddCondition() => _conditionDraft.AddCondition();

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
