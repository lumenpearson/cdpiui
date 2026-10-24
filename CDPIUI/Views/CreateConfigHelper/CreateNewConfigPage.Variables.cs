using System;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.LScript;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Views.CreateConfigHelper;

// The conditions flyout and the variables list.
public sealed partial class CreateNewConfigPage
{
    private bool IsVarAddAvailable()
    {
        bool result = CreateConfigPageHelper.IsNameCorrect(VarNameTextBox.Text.Replace(" ", "")) &&
            !string.IsNullOrEmpty(OnValueTextBox.Text) &&
            !string.IsNullOrEmpty(OffValueTextBox.Text);
        return result;
    }

    private void AuditSaveVarAvailable()
    {
        bool state = IsVarAddAvailable();
        SaveConditionButton.IsEnabled = state;
    }

    private void CreatePreview()
    {
        if (!IsVarAddAvailable())
        {
            ConditionPreviewTextBlock.Text = _localizer.GetLocalizedString("NothingToPreview");
            return;
        }
        ConditionPreviewTextBlock.Text =
            LScriptLangHelper.CreateCondition(VarNameTextBox.Text.Replace(" ", ""), OnValueTextBox.Text, OffValueTextBox.Text);
    }

    private void SaveConditionButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        ConditionVariableModel model = new()
        {
            Name = VarNameTextBox.Text,
            Description = DescriptionTextBox.Text,
            OnValue = OnValueTextBox.Text,
            OffValue = OffValueTextBox.Text,
        };
        Conditions.Add(model);
        ConditionFlyout.Hide();
    }

    private void ConditionFlyout_Opened(object sender, object e)
    {
        VarNameTextBox.Text = "";
        DescriptionTextBox.Text = "";
        OnValueTextBox.Text = "";
        OffValueTextBox.Text = "";
        ConditionPreviewTextBlock.Text = _localizer.GetLocalizedString("NothingToPreview");
    }

    private void VarNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

    private void OnValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

    private void OffValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CreatePreview();
        AuditSaveVarAvailable();
    }

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
        CreateConfigHelperWindow.Instanse.IsOperationExitAskAvailable = true;
        Logger.Instance.CreateDebugLog(nameof(CreateNewConfigPage), $"Variable changed: {parameter.Item1} to {parameter.Item2}");
        foreach (var var in Variables)
        {
            if (var.Name == parameter.Item1)
            {
                var.Value = parameter.Item2;
                break;
            }
        }
    }
}
