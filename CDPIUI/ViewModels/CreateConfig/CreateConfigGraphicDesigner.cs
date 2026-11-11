#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// Owns designer state and model edits; the host keeps view/tab/editor sequencing.
internal sealed class CreateConfigGraphicDesigner : ObservableObject
{
    private readonly ObservableCollection<GraphicDesignerSettingItemModel> _items;
    private readonly ObservableCollection<GraphicDesignerExclusiveSettingItemModel> _groups;
    private readonly ICreateConfigGraphicDesignerBackend _backend;
    private readonly Func<Components, string> _componentId;
    private string _loadedComponentId = string.Empty;
    private string _additionalSettings = string.Empty;

    public CreateConfigGraphicDesigner(
        ObservableCollection<GraphicDesignerSettingItemModel> items,
        ObservableCollection<GraphicDesignerExclusiveSettingItemModel> groups,
        ICreateConfigGraphicDesignerBackend backend,
        Func<Components, string> componentId)
    {
        _items = items;
        _groups = groups;
        _backend = backend;
        _componentId = componentId;
    }

    public string LoadedComponentId => _loadedComponentId;
    public string AdditionalSettings
    {
        get => _additionalSettings;
        set => SetProperty(ref _additionalSettings, value ?? string.Empty);
    }

    public void Load(string id)
    {
        if (_componentId(Components.GoodbyeDPI) == id && _loadedComponentId != _componentId(Components.GoodbyeDPI))
        {
            _backend.LoadGoodbye(_items, _groups);
            _loadedComponentId = _componentId(Components.GoodbyeDPI);
        }
        else if (_componentId(Components.SpoofDPI) == id && _loadedComponentId != _componentId(Components.SpoofDPI))
        {
            _backend.LoadSpoof(_items, _groups);
            _loadedComponentId = _componentId(Components.SpoofDPI);
        }
        else if (_componentId(Components.NoDPI) == id && _loadedComponentId != _componentId(Components.NoDPI))
        {
            if (_backend.TryLoadNoDpi(id, _items, _groups))
                _loadedComponentId = _componentId(Components.NoDPI);
        }
    }

    public void ChangeText(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string value = tuple.Item2;
        var item = _items.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.Value = value;
        }
        else
        {
            item = _groups.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            if (item != null)
                item.Value = value;
        }
    }

    public void ToggleBool(Tuple<string, bool> tuple)
    {
        string guid = tuple.Item1;
        bool value = tuple.Item2;
        var item = _items.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            item.IsChecked = value;
        }
        else
        {
            item = _groups.FirstOrDefault(x => x.Items.FirstOrDefault(y => y.Guid == guid) != null)?.Items.FirstOrDefault(y => y.Guid == guid);
            if (item != null)
                item.IsChecked = value;
        }
    }

    public void SelectGuid(Tuple<string, string> tuple)
    {
        string guid = tuple.Item1;
        string selectedGuid = tuple.Item2;
        var item = _groups.FirstOrDefault(x => x.Guid == guid);
        if (item != null)
        {
            Debug.WriteLine(selectedGuid);
            item.SelectedItemGuid = selectedGuid;
        }
    }

    public void Parse(string text) => AdditionalSettings = _backend.Parse(_items, _groups, text);
    public string Build() => _backend.Build(_items, _groups, AdditionalSettings);
}
