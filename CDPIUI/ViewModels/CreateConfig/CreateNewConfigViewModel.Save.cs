using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// Building the config from the page and saving it.
public sealed partial class CreateNewConfigViewModel
{
    private async Task SaveAsync()
    {
        ConvertDesignerLikeSettingsToString();
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        ConfigItem configItem = CreateConfig(secondsSinceEpoch);

        if (configItem == null)
        {
            return;
        }

        if (PageOpenMode == PageOpenModes.EditConfigNoSave)
        {
            _view.ReturnEditedConfig(configItem);
            return;
        }

        string src;
        if (_configItem == null || _configItem.packId != SharedConstants.LocalUserItemsId)
            src = DisplayName + $"_{secondsSinceEpoch}.json";
        else
            src = _configItem.file_name;

        string transl = ConfigMakerPresetStorageNaming.ToAsciiSlug(src);

        string errorCode = await ConfigurationService.SaveConfigItem(transl, SharedConstants.LocalUserItemsId, configItem);
        if (!string.IsNullOrEmpty(errorCode))
        {
            await _view.ShowErrorAsync(
                string.Format(_localizer.GetLocalizedString("CannotSaveConfigMessage"), errorCode),
                _localizer.GetLocalizedString("SomethingWentWrong")
                );
            return;
        }

        ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(SelectedComponent.Id);
        componentHelper?.ReInitConfigs();

        _view.SetExitAskAvailable(false);
        await _view.ShowSaveCompletedAsync();
    }

    private ConfigItem CreateConfig(int secondsSinceEpoch, bool saveLocalization = true)
    {
        string componentId = SelectedComponent.Id;
        var (jparams, vars) = CreateVariables(secondsSinceEpoch, saveLocalization);

        if (jparams == null || vars == null)
            return null;

        var configItem = CreateConfigPageHelper.CreateConfigItem(
            SharedConstants.LocalUserItemsId,
            DisplayName,
            componentId,
            jparams,
            vars,
            // A preset can define the same variable twice (as X= and as a comma variable);
            // the last value wins instead of an exception on save.
            Variables.GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.Last().Value),
            Variables.Where(v => v.AvailableValues != null).Select(v => v.AvailableValues).ToList(),
            CreateConfigPageHelper.GetNormalText(_view.EditorText, _listDirectory, _binDirectory)
            );

        return configItem;
    }

    private Tuple<Dictionary<string, bool>, List<string>> CreateVariables(int secondsSinceEpoch, bool saveLocalization)
    {
        try
        {
            return CreateConfigConditionBuilder.Build(
                Conditions,
                secondsSinceEpoch,
                EditItemId,
                saveLocalization);
        }
        catch (Exception ex)
        {
            // ShowErrorAsync catches and logs its own errors.
            _ = _view.ShowErrorAsync(
                string.Format(_localizer.GetLocalizedString("CannotSaveConfigMessage"), ErrorsHelper.Convertor.GetPrettyErrorCode("CREATE_CONFIG", ex)),
                _localizer.GetLocalizedString("SomethingWentWrong")
                );
            Logger.Instance.CreateWarningLog(LogSource, $"{ex}");
            return new(null, null);
        }
    }
}
