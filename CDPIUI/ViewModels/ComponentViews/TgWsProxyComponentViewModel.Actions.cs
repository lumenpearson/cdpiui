using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;

namespace CDPIUI.ViewModels.ComponentViews;

public sealed partial class TgWsProxyComponentViewModel
{
    private void RemoveDataCenter(string guid)
    {
        DataCenters.Remove(DataCenters.FirstOrDefault(x => x.Guid == guid));
    }

    private async Task EditDataCenterAsync(string guid)
    {
        try
        {
            var el = DataCenters.FirstOrDefault(x => x.Guid == guid);
            TgWsDataCenterEdit result = await _view.EditDataCenterAsync(
                _localizer.GetLocalizedString("EditDataCenter"),
                new TgWsDataCenterEdit(el?.Number, el?.Ip));

            if (result != null)
            {
                el.Number = result.Number;
                el.Ip = result.Ip;
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task AddDataCenterAsync()
    {
        try
        {
            TgWsDataCenterEdit result = await _view.EditDataCenterAsync(_localizer.GetLocalizedString("AddDataCenter"), null);
            if (result != null)
            {
                DataCenters.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Number = result.Number,
                    Ip = result.Ip,
                });
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private void HandleCreateFromTemplate(AvailableConfigCreationActios action)
    {
        if (action == AvailableConfigCreationActios.CreateFromTemplate)
            _ = CreateTemplateConfigAsync(); // CreateTemplateConfigAsync catches and logs its own errors.
    }

    private async Task CreateTemplateConfigAsync()
    {
        try
        {
            var item = CreateConfigPageHelper.CreateConfigItem(
                SharedConstants.LocalUserItemsId, _localizer.GetLocalizedString("DefaultConfigPlaceholder"), ComponentId, [], [], [], [], _defaultTgWsProxyStartString
                );
            string fileName = $"{Guid.NewGuid()}.json";
            string errorCode = await ConfigurationService.SaveConfigItem(fileName, SharedConstants.LocalUserItemsId, item);

            if (!string.IsNullOrEmpty(errorCode))
            {
                await _view.ShowErrorAsync(string.Format(_localizer.GetLocalizedString("SaveConfigException"), errorCode), _localizer.GetLocalizedString("SomethingWentWrong"));
                return;
            }

            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", fileName);
            SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", SharedConstants.LocalUserItemsId);

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
            componentHelper.ReInitConfigs();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task CreateConfigAsync()
    {
        try
        {
            ConfigCreationChoice choice = await _view.AskConfigCreationAsync();

            if (choice == ConfigCreationChoice.FromTemplate)
            {
                await CreateTemplateConfigAsync();
            }
            else if (choice == ConfigCreationChoice.Manual)
            {
                await _view.CreateConfigInEditorAsync(ComponentId);
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task LinkedSettingClickedAsync(object param)
    {
        try
        {
            if (param is LinkedActions action)
            {
                switch (action)
                {
                    case LinkedActions.EditCurrentConfig:
                        ComponentHelper componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(ComponentId);
                        var item = _selectedConfig;

                        if (componentHelper != null && item != null)
                            await _view.EditConfigAsync(componentHelper.GetConfigHelper().GetConfigItems().FirstOrDefault(x => x.packId == item.PackId && x.file_name == item.FileName));
                        break;
                    case LinkedActions.CreateNewConfigForComponent:
                        await CreateConfigAsync();
                        break;
                    case LinkedActions.OpenProxyInTelegram:
                        await _view.ShowConnectTelegramProxyAsync();
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
