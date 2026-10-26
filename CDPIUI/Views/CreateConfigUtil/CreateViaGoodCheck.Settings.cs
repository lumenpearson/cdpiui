using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic.Settings;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extensions;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Unidecode.NET;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.Views.CreateConfigUtil;

public sealed partial class CreateViaGoodCheck
{
    private void LoadGoodCheckSettings()
    {
        string targetFolder = Path.Combine(GoodCheckProcessService.GetAddOnDirectory(), "config.ini");

        if (!GoodCheckProcessService.IsAddOnInstalled())
        {
            _ = ShowErrorDialog(); // ShowErrorDialog catches and logs its own errors.
            return;
        }
        if (!File.Exists(targetFolder))
        {
            // Installed, but the files are incomplete: reinstalling fixes it, unlike installing.
            // ShowErrorDialog catches and logs its own errors.
            _ = ShowErrorDialog(string.Format(_localizer.GetLocalizedString("AddOnFilesMissing"),
                HardcodedItemIds.AddOnsIds.GetKeyByValue(AddOnId), targetFolder));
            return;
        }

        if (IniSettingsHelper is null)
            IniSettingsHelper = new(targetFolder, System.Text.Encoding.UTF8);

        if (ComponentId == HardcodedItemIds.ComponentIds[Core.Store.Data.Components.ByeDPI] && _resolverItems.FirstOrDefault(x => x.Id != "curl") != null)
            _resolverItems.Remove(_resolverItems.FirstOrDefault(x => x.Id != "curl"));

        PValue.Text = SettingsManager.Instance.GetValue<string>(["ADDONS", AddOnId], "passesValue");
        ResolverComboBox.SelectedIndex = ComponentId == HardcodedItemIds.ComponentIds[Core.Store.Data.Components.ByeDPI] ? 0 :
            SettingsManager.Instance.GetValue<bool>(["ADDONS", AddOnId], "useCurl") ? 1 : 0;

        ConnectionTimeout.Text = IniSettingsHelper.GetValue<string>("General", "ConnectionTimeout");

        ResolverNativeTimeout.Text = IniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeTimeout");
        ResolverNativeRetries.Text = IniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeRetries");
        InternalTimeoutMs.Text = IniSettingsHelper.GetValue<string>("Advanced", "InternalTimeoutMs");
        AutomaticConnectivityTest.IsOn = IniSettingsHelper.GetValue<bool>("Advanced", "AutomaticConnectivityTest");
        ConnectivityTestURL.Text = IniSettingsHelper.GetValue<string>("Advanced", "ConnectivityTestURL");
        SkipCertVerify.IsOn = IniSettingsHelper.GetValue<bool>("Advanced", "SkipCertVerify");

        AutomaticGoogleCacheTest.IsOn = IniSettingsHelper.GetValue<bool>("General", "AutomaticGoogleCacheTest");
        GoogleCacheMappingURLs.Text = IniSettingsHelper.GetValue<string>("Advanced", "GoogleCacheMappingURLs");

        UseDoH.IsOn = IniSettingsHelper.GetValue<bool>("Resolvers", "UseDoH");
        DoHResolvers.Text = IniSettingsHelper.GetValue<string>("Resolvers", "DoHResolvers");

        FakeSNI.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeSNI");
        FakeHexStreamTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamTCP");
        FakeHexStreamUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamUDP");
        FakeHexBytesTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesTCP");
        FakeHexBytesUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesUDP");
        PayloadTCP.Text = IniSettingsHelper.GetValue<string>("Fakes", "PayloadTCP");
        PayloadUDP.Text = IniSettingsHelper.GetValue<string>("Fakes", "PayloadUDP");
    }

    private void SaveGoodCheckSettings()
    {
        string targetFolder = Path.Combine(GoodCheckProcessService.GetAddOnDirectory(), "config.ini");

        if (IniSettingsHelper is null)
            IniSettingsHelper = new(targetFolder, System.Text.Encoding.UTF8);

        SettingsManager.Instance.SetValue<string>(["ADDONS", AddOnId], "passesValue", PValue.Text);
        SettingsManager.Instance.SetValue<bool>(["ADDONS", AddOnId], "useCurl", ((ComboBoxModel)ResolverComboBox.SelectedItem).Id == "curl" ? true : false);

        IniSettingsHelper.SetValue<string>("General", "ConnectionTimeout", ConnectionTimeout.Text);

        IniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeTimeout", ResolverNativeTimeout.Text);
        IniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeRetries", ResolverNativeRetries.Text);
        IniSettingsHelper.SetValue<string>("Advanced", "InternalTimeoutMs", InternalTimeoutMs.Text);
        IniSettingsHelper.SetValue<bool>("Advanced", "AutomaticConnectivityTest", AutomaticConnectivityTest.IsOn);
        IniSettingsHelper.SetValue<string>("Advanced", "ConnectivityTestURL", ConnectivityTestURL.Text);
        IniSettingsHelper.SetValue<bool>("Advanced", "SkipCertVerify", SkipCertVerify.IsOn);

        IniSettingsHelper.SetValue<bool>("General", "AutomaticGoogleCacheTest", AutomaticGoogleCacheTest.IsOn);
        IniSettingsHelper.SetValue<string>("Advanced", "GoogleCacheMappingURLs", GoogleCacheMappingURLs.Text);

        IniSettingsHelper.SetValue<bool>("Resolvers", "UseDoH", UseDoH.IsOn);
        IniSettingsHelper.SetValue<string>("Resolvers", "DoHResolvers", DoHResolvers.Text);

        IniSettingsHelper.SetValue<string>("Fakes", "FakeSNI", FakeSNI.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamTCP", FakeHexStreamTCP.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamUDP", FakeHexStreamUDP.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesTCP", FakeHexBytesTCP.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesUDP", FakeHexBytesUDP.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "PayloadTCP", PayloadTCP.Text);
        IniSettingsHelper.SetValue<string>("Fakes", "PayloadUDP", PayloadUDP.Text);

        string itemsFolder = Directories.StoreItemsDirectory;

        try
        {
            GoodCheckComponentPathsWriter.Write(IniSettingsHelper, itemsFolder);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessService), $"Cannot set settings. Exception is {ex}");
            _ = ShowErrorDialog(_localizer.GetLocalizedString("GoodCheckComponentDirsSetException")); // ShowErrorDialog catches and logs its own errors.
        }
        IniSettingsHelper.Save();
    }

    private void StartGoodCheck()
    {
        List<GoodCheckSiteListModel> listModels = new();

        if (!ChangeModeComboBox.IsChecked == true)
        {
            foreach (
                GoodCheckSitelistButton button in
                SelectedSitelistStackPanel.Children.OfType<GoodCheckSitelistButton>())
            {
                var strategiesList = StrategiesListCombobox.Items[StrategiesListCombobox.SelectedIndex] as GoodCheckStrategiesList;
                GoodCheckSiteListModel model = new()
                {
                    SiteListPath = button.FilePath,
                    StrategyListPath = strategiesList.FilePath,
                };
                listModels.Add(model);
            }
        }
        else
        {
            foreach (
                OneSiteListOneStrategyModeElement element in _oneSiteListOneStrategyModeElements)
            {
                GoodCheckSiteListModel model = new()
                {
                    SiteListPath = element.SiteListFilePath,
                    StrategyListPath = element.StrategiesListFilePath,
                };
                listModels.Add(model);
            }
        }

        if (!GoodCheckProcessService.IsComponentSupported(ComponentId))
        {
            LogGoodCheckError($"ERR_GOODCHECK_UNSUPPORTED_COMPONENT ({ComponentId})");
            return;
        }

        GoodCheckProcessService.Instance.InitGoodCheck(
            ComponentId,
            (bool)ChangeModeComboBox.IsChecked ? GoodCheckProcessMode.AnyAsAny : GoodCheckProcessMode.AllAsOne,
            listModels
            );

        GoodCheckProcessService.Instance.ErrorHappens -= LogGoodCheckError;
        GoodCheckProcessService.Instance.ErrorHappens += LogGoodCheckError;

        GoodCheckProcessService.Instance.Start();
    }

    private static void LogGoodCheckError(string error)
    {
        Logger.Instance.CreateErrorLog(nameof(CreateViaGoodCheck), error);
    }
}
