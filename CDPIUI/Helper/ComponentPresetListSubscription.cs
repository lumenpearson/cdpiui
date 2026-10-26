using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Helper.Items;
using Microsoft.UI.Dispatching;

namespace CDPIUI.Helper;

internal sealed class ComponentPresetListSubscription : IDisposable
{
    private readonly string _componentId;
    private readonly DispatcherQueue _dispatcher;
    private readonly Action<IReadOnlyList<ConfigSelectorItem>> _apply;
    private ComponentHelper _helper;
    private bool _disposed;
    private int _version;

    public ComponentPresetListSubscription(string componentId, DispatcherQueue dispatcher,
        Action<IReadOnlyList<ConfigSelectorItem>> apply)
    {
        _componentId = componentId;
        _dispatcher = dispatcher;
        _apply = apply;
        ComponentItemsLoaderHelper.Instance.InitRequested += Rebind;
        Rebind();
    }

    private void Rebind() => _dispatcher.TryEnqueue(() =>
    {
        if (_disposed)
            return;
        var current = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_componentId);
        if (!ReferenceEquals(current, _helper))
        {
            if (_helper != null)
                _helper.ConfigListUpdated -= Refresh;
            _helper = current;
            if (_helper != null)
                _helper.ConfigListUpdated += Refresh;
        }
        Refresh();
    });

    public void Refresh() => _dispatcher.TryEnqueue(async () =>
    {
        if (_disposed)
            return;
        int request = ++_version;
        var source = _helper;
        try
        {
            PackNameLookup packNames = new();
            var items = await Task.Run(() => source?.GetConfigHelper().GetConfigItems()
                .Where(item => !item.MarkAsRemoved)
                .Select(item => new ConfigSelectorItem
                {
                    FileName = item.file_name,
                    PackId = item.packId,
                    DisplayName = item.name,
                    PackDisplayName = packNames.GetDisplayName(item.packId),
                    IsLegacyConfig = item.IsLegacy
                }).ToArray() ?? Array.Empty<ConfigSelectorItem>());
            if (!_disposed && request == _version && ReferenceEquals(source, _helper))
                _apply(items);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(ComponentPresetListSubscription), exception.ToString());
        }
    });

    public void Dispose()
    {
        _disposed = true;
        ++_version;
        ComponentItemsLoaderHelper.Instance.InitRequested -= Rebind;
        if (_helper != null)
            _helper.ConfigListUpdated -= Refresh;
        _helper = null;
    }
}
