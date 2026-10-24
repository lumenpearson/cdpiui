using System;
using System.Collections.Generic;
using CDPIUI.ConditionalLaunch;

namespace CDPIUI.ViewModels.ConditionalLaunch;

/// <summary>The value of one parameter in the conditional action editor.</summary>
public sealed class ConditionalActionParameterViewModel : ObservableObject
{
    private readonly Action<ConditionalActionParameterViewModel> _valueChanged;
    private ConditionalParameterChoice? _selectedChoice;
    private double _number;
    private string _text = string.Empty;
    private bool _isVisible = true;

    internal ConditionalActionParameterViewModel(
        ConditionalParameterDefinition definition,
        ConditionalActionParameterKind kind,
        IReadOnlyList<ConditionalParameterChoice> choices,
        Action<ConditionalActionParameterViewModel> valueChanged)
    {
        Definition = definition;
        Kind = kind;
        Choices = choices;
        _valueChanged = valueChanged;
    }

    internal ConditionalParameterDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Label => Definition.Label;

    public ConditionalActionParameterKind Kind { get; }

    public double Minimum => Definition.Minimum;

    public double Maximum => Definition.Maximum;

    internal IReadOnlyList<ConditionalParameterChoice> Choices { get; }

    internal ConditionalParameterChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (SetProperty(ref _selectedChoice, value))
                _valueChanged(this);
        }
    }

    public double Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The value as the action stores it; empty when nothing is entered.</summary>
    public string Value => Kind switch
    {
        ConditionalActionParameterKind.Text => Text.Trim(),
        ConditionalActionParameterKind.Number when !double.IsNaN(Number) => ((long)Number).ToString(),
        ConditionalActionParameterKind.Choice when SelectedChoice != null => SelectedChoice.Value,
        _ => string.Empty
    };
}
