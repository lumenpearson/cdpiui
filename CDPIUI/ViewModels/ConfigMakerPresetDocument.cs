#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.ViewModels;

public sealed class ConfigMakerPresetDocument : INotifyPropertyChanged
{
    private static readonly Regex VariableNameRegex = new(
        "^[A-Za-z][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private string _componentId = string.Empty;
    private string _name = string.Empty;
    private string _commandText = string.Empty;
    private readonly HashSet<ConfigMakerVariableDefinition> _subscribedVariables = [];
    private readonly HashSet<ConfigMakerPresetResource> _subscribedResources = [];

    public ConfigMakerPresetDocument()
    {
        Variables.CollectionChanged += Variables_CollectionChanged;
        Resources.CollectionChanged += Resources_CollectionChanged;
    }

    public string? PackId { get; set; }
    public string? FileName { get; set; }
    public string? Meta { get; set; }
    public string? TargetVersion { get; set; }
    public string ComponentId { get => _componentId; set => SetField(ref _componentId, value ?? string.Empty); }
    public string Name { get => _name; set => SetField(ref _name, value ?? string.Empty); }
    public string CommandText { get => _commandText; set => SetField(ref _commandText, value ?? string.Empty); }
    public ObservableCollection<ConfigMakerVariableDefinition> Variables { get; } = [];
    public ObservableCollection<ConfigMakerPresetResource> Resources { get; } = [];
    public bool HasVariables => Variables.Count > 0;
    public bool HasResources => Resources.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ContentChanged;

    public static bool IsValidVariableName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && VariableNameRegex.IsMatch(value);

    public bool ContainsVariable(string name, string? exceptId = null) => Variables.Any(variable =>
        !string.Equals(variable.Id, exceptId, StringComparison.Ordinal) &&
        string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));

    public ConfigItem ToConfigItem(string packId, string presetName) =>
        ConfigMakerPresetDocumentConverter.ToConfigItem(this, packId, presetName);

    public static ConfigMakerPresetDocument FromConfigItem(ConfigItem item) =>
        ConfigMakerPresetDocumentConverter.FromConfigItem(item);

    public ConfigMakerPresetDocument Clone()
    {
        ConfigMakerPresetDocument result = new()
        {
            PackId = PackId,
            FileName = FileName,
            Meta = Meta,
            TargetVersion = TargetVersion,
            ComponentId = ComponentId,
            Name = Name,
            CommandText = CommandText,
        };
        foreach (ConfigMakerVariableDefinition variable in Variables)
        {
            result.Variables.Add(variable.Clone());
        }
        foreach (ConfigMakerPresetResource resource in Resources)
        {
            result.Resources.Add(resource.Clone());
        }
        return result;
    }

    public string ReplaceVariableReference(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return CommandText;
        }
        CommandText = Regex.Replace(
            CommandText,
            $"%{Regex.Escape(oldName)}%",
            $"%{newName}%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return CommandText;
    }

    private void Variables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateItemSubscriptions(
            e,
            Variable_PropertyChanged,
            _subscribedVariables,
            Variables);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasVariables)));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Resources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateItemSubscriptions(
            e,
            Resource_PropertyChanged,
            _subscribedResources,
            Resources);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasResources)));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void UpdateItemSubscriptions<T>(
        NotifyCollectionChangedEventArgs args,
        PropertyChangedEventHandler handler,
        ISet<T> subscribedItems,
        IEnumerable<T> currentItems)
        where T : INotifyPropertyChanged
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (T item in subscribedItems)
            {
                item.PropertyChanged -= handler;
            }
            subscribedItems.Clear();
            foreach (T item in currentItems)
            {
                item.PropertyChanged += handler;
                subscribedItems.Add(item);
            }
            return;
        }
        foreach (T item in args.OldItems?.OfType<T>() ?? [])
        {
            item.PropertyChanged -= handler;
            subscribedItems.Remove(item);
        }
        foreach (T item in args.NewItems?.OfType<T>() ?? [])
        {
            item.PropertyChanged += handler;
            subscribedItems.Add(item);
        }
    }

    private void Variable_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ContentChanged?.Invoke(this, EventArgs.Empty);

    private void Resource_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ContentChanged?.Invoke(this, EventArgs.Empty);

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }
}
