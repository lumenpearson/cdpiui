using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Proxy;
using CDPIUI.Core.Store.Data;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// The state of the visual editor that builds a config from GoodCheck results: the found
/// strategies, the strategies the user picked and the startup string made of them.
/// </summary>
public sealed partial class GoodCheckConfigVisualEditorViewModel : ObservableObject
{
    private bool _isWarningVisible;

    public GoodCheckConfigVisualEditorViewModel()
    {
        RightItems.CollectionChanged += RightItems_CollectionChanged;
        ChangeModeCommand = new RelayCommand(p => ChangeModeInModel((Tuple<string, List<ChooseGroupModeContentDialog.ByeDPIGroupModes>>)p));
    }

    /// <summary>The strategies GoodCheck found.</summary>
    public ObservableCollection<DragItem> LeftItems { get; } = [];

    /// <summary>The strategies the config is made of.</summary>
    public ObservableCollection<DragItem> RightItems { get; } = [];

    public string ComponentId { get; private set; } = string.Empty;

    public ICommand ChangeModeCommand { get; }

    /// <summary>The component cannot group strategies: the config takes only one.</summary>
    public bool IsWarningVisible
    {
        get => _isWarningVisible;
        private set => SetProperty(ref _isWarningVisible, value);
    }

    public bool CanGoForward => RightItems.Count > 0;

    /// <summary>Another strategy can be added to the config.</summary>
    public bool CanAddToRight => !(IsGoodbyeDPI && RightItems.Count >= 1);

    private bool IsGoodbyeDPI => ComponentId == HardcodedItemIds.ComponentIds[Components.GoodbyeDPI];

    private bool IsByeDPI => HardcodedItemIds.ComponentIds[Components.ByeDPI] == ComponentId;

    public void SetComponent(string componentId)
    {
        ComponentId = componentId;

        if (IsGoodbyeDPI)
        {
            IsWarningVisible = true;
        }
    }

    /// <summary>Adds the strategies from the serialized list of the navigation parameter.</summary>
    public void LoadStrategies(string dragList)
    {
        List<DragItem> items = JSONConvertor.DeserializeObject<List<DragItem>>(dragList ?? string.Empty);

        foreach (var item in items)
        {
            LeftItems.Add(item);
        }
    }

    /// <summary>Copies the found strategy with the id to the config.</summary>
    public void AddToRight(Guid id)
    {
        var sourceLeft = LeftItems.FirstOrDefault(x => x.Id == id);
        if (sourceLeft != null && RightItems.FirstOrDefault(x => x.Id == id) == null)
        {
            DragItem item = new(sourceLeft.Args,
                sourceLeft.SiteListName,
                sourceLeft.Directory,
                IsByeDPI,
                [ChooseGroupModeContentDialog.ByeDPIGroupModes.None],
                id);
            RightItems.Add(item);
            CheckRightItemsSort();
        }
    }

    /// <summary>Removes the strategy with the id from the config.</summary>
    public void RemoveFromRight(Guid id)
    {
        var toRemove = RightItems.FirstOrDefault(x => x.Id == id);
        if (toRemove != null)
        {
            RightItems.Remove(toRemove);
        }
    }

    /// <summary>Only the strategies after the first one choose a ByeDPI group mode.</summary>
    public void CheckRightItemsSort()
    {
        if (!IsByeDPI)
            return;
        if (RightItems.Count > 0)
        {
            var firstItem = RightItems.First();
            firstItem.ShowGroupModeChooser = false;
            foreach (var item in RightItems)
            {
                if (item != firstItem && item.ShowGroupModeChooser == false)
                    item.ShowGroupModeChooser = true;
            }
        }
    }

    /// <summary>The navigation parameter that opens the config editor with the built startup string.</summary>
    public NameValueCollection CreateEditorParameter()
    {
        string startupString = BuildStartupString();
        Debug.WriteLine(startupString);

        return new NameValueCollection()
        {
            { "type", "CFGSTRING" },
            { "startupString", startupString },
            { "componentId", ComponentId }
        };
    }

    private string BuildStartupString()
    {
        string startupString = string.Empty;

        if (HardcodedItemIds.ComponentIds[Components.Zapret] == ComponentId)
        {
            foreach (var item in RightItems)
            {
                string part = "";
                if (startupString.Length != 0)
                    part = "--new ";

                part += $"--hostlist=\"{Path.Combine(item.Directory, item.SiteListName)}\" {item.Args} ";
                startupString += part;
            }
        }
        else if (IsByeDPI)
        {
            foreach (var item in RightItems)
            {
                string part = "";
                if (startupString.Length != 0)
                {
                    part = $"-A{ConvertModesToString(item.Modes)} ";
                }

                part += $"-H=\"{Path.Combine(item.Directory, item.SiteListName)}\" {ProxyHelper.ReplaceIp(item.Args)} ";
                startupString += part;
            }
        }
        else
        {
            foreach (var item in RightItems)
            {
                startupString += $"--blacklist=\"{Path.Combine(item.Directory, item.SiteListName)}\" {ProxyHelper.ReplaceIp(item.Args)} ";
            }
        }

        return startupString;
    }

    private static string ConvertModesToString(List<ChooseGroupModeContentDialog.ByeDPIGroupModes> modes)
    {
        string finalString = string.Empty;
        foreach (var mode in modes)
        {
            switch (mode)
            {
                case ChooseGroupModeContentDialog.ByeDPIGroupModes.Torst:
                    finalString += "t";
                    break;
                case ChooseGroupModeContentDialog.ByeDPIGroupModes.Redirect:
                    finalString += "r";
                    break;
                case ChooseGroupModeContentDialog.ByeDPIGroupModes.SslErr:
                    finalString += "s";
                    break;
                case ChooseGroupModeContentDialog.ByeDPIGroupModes.None:
                    finalString += "n";
                    break;
            }
            finalString += ",";
        }
        Debug.WriteLine(finalString.Length);
        return finalString.Length > 0 ? finalString.Remove(finalString.Length - 1) : finalString;
    }

    private void ChangeModeInModel(Tuple<string, List<ChooseGroupModeContentDialog.ByeDPIGroupModes>> tuple)
    {
        var targetItem = RightItems.FirstOrDefault(x => x.Id.ToString() == tuple.Item1);
        if (targetItem != null)
        {
            targetItem.Modes = tuple.Item2;
        }
    }

    private void RightItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanGoForward));
    }
}
