using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.System;
using CDPIUI.ViewModels.ComponentViews;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UserControl = Microsoft.UI.Xaml.Controls.UserControl;

namespace CDPIUI;

public sealed partial class ComponentSettingUserControl : UserControl, IComponentSettingView
{
    public static readonly DependencyProperty ToggledCommandProperty =
            DependencyProperty.Register(
                nameof(ToggledCommand),
                typeof(ICommand),
                typeof(ComponentSettingUserControl),
                new PropertyMetadata(null)
            );

    public static readonly DependencyProperty ToggledParameterProperty =
        DependencyProperty.Register(
            nameof(ToggledParameter),
            typeof(object),
            typeof(ComponentSettingUserControl),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty ValueChangedCommandProperty =
            DependencyProperty.Register(
                nameof(ValueChangedCommand),
                typeof(ICommand),
                typeof(ComponentSettingUserControl),
                new PropertyMetadata(null)
            );

    public static readonly DependencyProperty ValueChangedParameterProperty =
        DependencyProperty.Register(
            nameof(ValueChangedParameter),
            typeof(object),
            typeof(ComponentSettingUserControl),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty GuidProperty =
        DependencyProperty.Register(
            nameof(Guid), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(
            nameof(DisplayName), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty TextValueProperty =
        DependencyProperty.Register(
            nameof(TextValue), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(
            nameof(Type), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty AvailableEnumValuesProperty =
        DependencyProperty.Register(
            nameof(AvailableEnumValues), typeof(List<EnumModel>), typeof(ComponentSettingUserControl), new PropertyMetadata(new List<EnumModel>())
        );

    public static readonly DependencyProperty EnableTextInputProperty =
        DependencyProperty.Register(
            nameof(EnableTextInput), typeof(bool), typeof(ComponentSettingUserControl), new PropertyMetadata(false)
        );

    public static readonly DependencyProperty IsSettingCheckedProperty =
        DependencyProperty.Register(
            nameof(IsSettingChecked), typeof(bool), typeof(ComponentSettingUserControl), new PropertyMetadata(false)
        );

    // The check state reaches the controls through x:Bind when the control loads, after
    // the host has set the commands. The state set by the host is not a user toggle, so
    // the notifications start only after Loaded, as when the state was written to the
    // controls before the commands were set.
    private bool _isLoaded;

    public ComponentSettingUserControl()
    {
        ViewModel = new ComponentSettingViewModel(this);
        InitializeComponent();
        Loaded += (_, _) => _isLoaded = true;
    }

    public ComponentSettingViewModel ViewModel { get; }

    public ICommand ToggledCommand
    {
        get => (ICommand)GetValue(ToggledCommandProperty);
        set => SetValue(ToggledCommandProperty, value);
    }

    public object ToggledParameter
    {
        get => GetValue(ToggledParameterProperty);
        set => SetValue(ToggledParameterProperty, value);
    }

    public ICommand ValueChangedCommand
    {
        get => (ICommand)GetValue(ValueChangedCommandProperty);
        set => SetValue(ValueChangedCommandProperty, value);
    }

    public object ValueChangedParameter
    {
        get => GetValue(ValueChangedParameterProperty);
        set => SetValue(ValueChangedParameterProperty, value);
    }

    public string Guid
    {
        get { return (string)GetValue(GuidProperty); }
        set { SetValue(GuidProperty, value); }
    }

    public string DisplayName
    {
        get { return (string)GetValue(DisplayNameProperty); }
        set { SetValue(DisplayNameProperty, value); }
    }

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public string TextValue
    {
        get { return (string)GetValue(TextValueProperty); }
        set { ViewModel.SetTextValue(value); }
    }

    public string Type
    {
        get { return (string)GetValue(TypeProperty); }
        set
        {
            SetValue(TypeProperty, value);
            ViewModel.ApplyType();
        }
    }

    public List<EnumModel> AvailableEnumValues
    {
        get { return (List<EnumModel>)GetValue(AvailableEnumValuesProperty); }
        set
        {
            SetValue(AvailableEnumValuesProperty, value ?? new List<EnumModel>());
            ViewModel.ApplyEnumValues();
        }
    }

    public bool EnableTextInput
    {
        get { return (bool)GetValue(EnableTextInputProperty); }
        set { SetValue(EnableTextInputProperty, value); }
    }

    public bool IsSettingChecked
    {
        get { return (bool)GetValue(IsSettingCheckedProperty); }
        set
        {
            SetValue(IsSettingCheckedProperty, value);
            ViewModel.ShowChecked(value);
        }
    }

    void IComponentSettingView.StoreTextValue(string value) => SetValue(TextValueProperty, value);

    void IComponentSettingView.NotifyValueChanged() => NotifyValueChanged();

    private void NotifyValueChanged()
    {
        ValueChangedParameter ??= Tuple.Create(Guid, TextValue);
        if (ValueChangedCommand != null && ValueChangedCommand.CanExecute(ValueChangedParameter))
        {
            ValueChangedCommand.Execute(ValueChangedParameter);
        }
        ValueChangedParameter = null;
    }

    private void NotifyToggled(bool isChecked)
    {
        ToggledParameter ??= Tuple.Create(Guid, isChecked);
        if (ToggledCommand != null && ToggledCommand.CanExecute(ToggledParameter))
        {
            ToggledCommand.Execute(ToggledParameter);
        }
        ToggledParameter = null;
    }

    private void ToggleChecked()
    {
        if (!_isLoaded)
            return;

        bool isChecked = ViewModel.UsesCheckBox ? (bool)MainCheckBox.IsChecked : MainToggleSwitch.IsOn;
        NotifyToggled(isChecked);
    }

    private void MainCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void MainCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void MainToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TextValue = MainTextBox.Text;
        NotifyValueChanged();
    }

    private void EnumPropertyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectEnumValue((EnumModel)EnumPropertyComboBox.SelectedItem);
    }

    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath;
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Title = WinUI3Localizer.Localizer.Get().GetLocalizedString("FileDialogChooseTextFileTitle");
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            openFileDialog.Filter = $"{WinUI3Localizer.Localizer.Get().GetLocalizedString("TextFiles")} (*.txt)|*.txt";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
            }
            else
            {
                return;
            }
        }
        TextValue = $"\"{filePath}\"";
        NotifyValueChanged();
    }

    private async void ViewSelectedFileButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = ComponentSettingViewModel.UnquotePath(TextValue);
        if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
        {
            EditSitelistAskApplicationContentDialog dialog = new EditSitelistAskApplicationContentDialog()
            {
                XamlRoot = this.XamlRoot,
                FilePath = filePath,
            };
            await dialog.ShowQueuedAsync();
            if (dialog.IsSuccess)
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
        }
        else
        {
            ShellHelper.OpenFile(filePath);
        }
    }
}
