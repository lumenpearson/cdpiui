using System.ComponentModel;
using CDPIUI.ConditionalLaunch;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.ViewModels.ConditionalLaunch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.Dialogs.ConditionalLaunch;

/// <summary>
/// Adds or edits a conditional action. The parameter editors depend on the action type, so the
/// dialog creates their controls from <see cref="ConditionalActionEditorViewModel.Parameters"/>
/// and writes the entered values back to the parameter view models.
/// </summary>
public sealed partial class ConditionalActionContentDialog : ContentDialog
{
    private ComboBox? _presetComboBox;
    private bool _updatingPresetComboBox;

    public ConditionalActionEditorViewModel ViewModel { get; }

    public ConditionalAction? ResultAction => ViewModel.ResultAction;

    public ConditionalActionContentDialog(ConditionalAction? action)
    {
        ViewModel = new ConditionalActionEditorViewModel(action);
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ActionTypeComboBox.ItemsSource = ViewModel.Definitions;

        Title = ViewModel.Title;
        PrimaryButtonText = ViewModel.PrimaryButtonText;
        CloseButtonText = ViewModel.CloseButtonText;
        DefaultButton = ContentDialogButton.Primary;

        if (ViewModel.InitialDefinition != null)
            ActionTypeComboBox.SelectedItem = ViewModel.InitialDefinition;
    }

    private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectDefinition(ActionTypeComboBox.SelectedItem);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConditionalActionEditorViewModel.Parameters))
            BuildParameterControls();
        else if (e.PropertyName == nameof(ConditionalActionEditorViewModel.PresetChoices))
            UpdatePresetComboBox();
    }

    private void BuildParameterControls()
    {
        ActionParametersPanel.Children.Clear();
        _presetComboBox = null;

        if (ViewModel.IsPresetEditor)
        {
            BuildPresetControls();
            return;
        }

        foreach (ConditionalActionParameterViewModel parameter in ViewModel.Parameters)
            ActionParametersPanel.Children.Add(CreateParameterControl(parameter));
    }

    private static FrameworkElement CreateParameterControl(ConditionalActionParameterViewModel parameter)
    {
        FrameworkElement control;
        switch (parameter.Kind)
        {
            case ConditionalActionParameterKind.Choice:
                ComboBox comboBox = new()
                {
                    Header = parameter.Label,
                    ItemsSource = parameter.Choices,
                    DisplayMemberPath = nameof(ConditionalParameterChoice.Name),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = parameter.Name
                };
                comboBox.SelectedItem = parameter.SelectedChoice;
                comboBox.SelectionChanged += (_, _) =>
                    parameter.SelectedChoice = comboBox.SelectedItem as ConditionalParameterChoice;
                control = comboBox;
                break;
            case ConditionalActionParameterKind.Number:
                NumberBox numberBox = new()
                {
                    Header = parameter.Label,
                    Minimum = parameter.Minimum,
                    Maximum = parameter.Maximum,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                    Tag = parameter.Name
                };
                numberBox.Value = parameter.Number;
                // The number box clamps the value to its range.
                parameter.Number = numberBox.Value;
                numberBox.ValueChanged += (_, _) => parameter.Number = numberBox.Value;
                control = numberBox;
                break;
            default:
                TextBox textBox = new()
                {
                    Header = parameter.Label,
                    Text = parameter.Text,
                    Tag = parameter.Name
                };
                textBox.TextChanged += (_, _) => parameter.Text = textBox.Text;
                control = textBox;
                break;
        }

        control.Visibility = parameter.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        parameter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConditionalActionParameterViewModel.IsVisible))
                control.Visibility = parameter.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        };
        return control;
    }

    private void BuildPresetControls()
    {
        ComboBox componentComboBox = new()
        {
            Header = ViewModel.PresetComponentHeader,
            ItemsSource = ViewModel.PresetComponentChoices,
            DisplayMemberPath = nameof(ConditionalParameterChoice.Name),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = "componentId"
        };
        componentComboBox.SelectedItem = ViewModel.SelectedPresetComponent;

        _presetComboBox = new()
        {
            Header = ViewModel.PresetHeader,
            ItemTemplate = (DataTemplate)Resources["ConditionalPresetChoiceTemplate"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = "preset",
            Height = 80
        };
        _presetComboBox.SelectionChanged += PresetComboBox_SelectionChanged;

        ActionParametersPanel.Children.Add(componentComboBox);
        ActionParametersPanel.Children.Add(_presetComboBox);

        componentComboBox.SelectionChanged += (_, _) =>
            ViewModel.SelectedPresetComponent = componentComboBox.SelectedItem as ConditionalParameterChoice;
        UpdatePresetComboBox();
    }

    private void UpdatePresetComboBox()
    {
        if (_presetComboBox == null)
            return;
        _updatingPresetComboBox = true;
        _presetComboBox.ItemsSource = ViewModel.PresetChoices;
        _presetComboBox.SelectedItem = ViewModel.SelectedPreset;
        _updatingPresetComboBox = false;
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingPresetComboBox)
            ViewModel.SelectedPreset = _presetComboBox?.SelectedItem as ConditionalPresetChoice;
    }

    private void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        if (!ViewModel.TryCreateAction())
            args.Cancel = true;
    }
}
