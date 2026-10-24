using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.Basic.Settings;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Shared.Extensions;
using CDPIUI.Views.CreateConfigUtil;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.ViewModels.CreateConfig;

// The additional settings step: the GoodCheck config.ini and the add-on settings.
public sealed partial class CreateViaGoodCheckViewModel
{
    private IniSettingsHelper _iniSettingsHelper;
    private int _resolverIndex;

    private string _passesValue;
    private string _connectionTimeout;
    private string _resolverNativeTimeout;
    private string _resolverNativeRetries;
    private string _internalTimeoutMs;
    private bool _automaticConnectivityTest;
    private string _connectivityTestUrl;
    private bool _skipCertVerify;
    private bool _automaticGoogleCacheTest;
    private string _googleCacheMappingUrls;
    private bool _useDoH;
    private string _doHResolvers;
    private string _fakeSni;
    private string _fakeHexStreamTcp;
    private string _fakeHexStreamUdp;
    private string _fakeHexBytesTcp;
    private string _fakeHexBytesUdp;
    private string _payloadTcp;
    private string _payloadUdp;

    public ObservableCollection<ComboBoxModel> ResolverItems { get; } = [];

    public int ResolverIndex
    {
        get => _resolverIndex;
        set => SetProperty(ref _resolverIndex, value);
    }

    public string PassesValue { get => _passesValue; set => SetProperty(ref _passesValue, value); }
    public string ConnectionTimeout { get => _connectionTimeout; set => SetProperty(ref _connectionTimeout, value); }
    public string ResolverNativeTimeout { get => _resolverNativeTimeout; set => SetProperty(ref _resolverNativeTimeout, value); }
    public string ResolverNativeRetries { get => _resolverNativeRetries; set => SetProperty(ref _resolverNativeRetries, value); }
    public string InternalTimeoutMs { get => _internalTimeoutMs; set => SetProperty(ref _internalTimeoutMs, value); }
    public bool AutomaticConnectivityTest { get => _automaticConnectivityTest; set => SetProperty(ref _automaticConnectivityTest, value); }
    public string ConnectivityTestUrl { get => _connectivityTestUrl; set => SetProperty(ref _connectivityTestUrl, value); }
    public bool SkipCertVerify { get => _skipCertVerify; set => SetProperty(ref _skipCertVerify, value); }
    public bool AutomaticGoogleCacheTest { get => _automaticGoogleCacheTest; set => SetProperty(ref _automaticGoogleCacheTest, value); }
    public string GoogleCacheMappingUrls { get => _googleCacheMappingUrls; set => SetProperty(ref _googleCacheMappingUrls, value); }
    public bool UseDoH { get => _useDoH; set => SetProperty(ref _useDoH, value); }
    public string DoHResolvers { get => _doHResolvers; set => SetProperty(ref _doHResolvers, value); }
    public string FakeSni { get => _fakeSni; set => SetProperty(ref _fakeSni, value); }
    public string FakeHexStreamTcp { get => _fakeHexStreamTcp; set => SetProperty(ref _fakeHexStreamTcp, value); }
    public string FakeHexStreamUdp { get => _fakeHexStreamUdp; set => SetProperty(ref _fakeHexStreamUdp, value); }
    public string FakeHexBytesTcp { get => _fakeHexBytesTcp; set => SetProperty(ref _fakeHexBytesTcp, value); }
    public string FakeHexBytesUdp { get => _fakeHexBytesUdp; set => SetProperty(ref _fakeHexBytesUdp, value); }
    public string PayloadTcp { get => _payloadTcp; set => SetProperty(ref _payloadTcp, value); }
    public string PayloadUdp { get => _payloadUdp; set => SetProperty(ref _payloadUdp, value); }

    private void InitResolverItems()
    {
        ResolverItems.Clear();
        ResolverItems.Add(new ComboBoxModel() { DisplayName = _localizer.GetLocalizedString("/GoodCheckAdditionalSettings/NativeResolver"), Id = "native" });
        ResolverItems.Add(new ComboBoxModel() { DisplayName = _localizer.GetLocalizedString("/GoodCheckAdditionalSettings/Curl"), Id = "curl" });
    }

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

        _iniSettingsHelper ??= new(targetFolder, System.Text.Encoding.UTF8);

        bool isByeDPI = ComponentId == HardcodedItemIds.ComponentIds[Components.ByeDPI];
        if (isByeDPI && ResolverItems.FirstOrDefault(x => x.Id != "curl") != null)
            ResolverItems.Remove(ResolverItems.FirstOrDefault(x => x.Id != "curl"));

        PassesValue = SettingsManager.Instance.GetValue<string>(["ADDONS", AddOnId], "passesValue");

        // Removing the selected resolver resets the combo box: raise the index even when it is unchanged.
        _resolverIndex = isByeDPI ? 0 :
            SettingsManager.Instance.GetValue<bool>(["ADDONS", AddOnId], "useCurl") ? 1 : 0;
        OnPropertyChanged(nameof(ResolverIndex));

        ConnectionTimeout = _iniSettingsHelper.GetValue<string>("General", "ConnectionTimeout");

        ResolverNativeTimeout = _iniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeTimeout");
        ResolverNativeRetries = _iniSettingsHelper.GetValue<string>("Advanced", "ResolverNativeRetries");
        InternalTimeoutMs = _iniSettingsHelper.GetValue<string>("Advanced", "InternalTimeoutMs");
        AutomaticConnectivityTest = _iniSettingsHelper.GetValue<bool>("Advanced", "AutomaticConnectivityTest");
        ConnectivityTestUrl = _iniSettingsHelper.GetValue<string>("Advanced", "ConnectivityTestURL");
        SkipCertVerify = _iniSettingsHelper.GetValue<bool>("Advanced", "SkipCertVerify");

        AutomaticGoogleCacheTest = _iniSettingsHelper.GetValue<bool>("General", "AutomaticGoogleCacheTest");
        GoogleCacheMappingUrls = _iniSettingsHelper.GetValue<string>("Advanced", "GoogleCacheMappingURLs");

        UseDoH = _iniSettingsHelper.GetValue<bool>("Resolvers", "UseDoH");
        DoHResolvers = _iniSettingsHelper.GetValue<string>("Resolvers", "DoHResolvers");

        FakeSni = _iniSettingsHelper.GetValue<string>("Fakes", "FakeSNI");
        FakeHexStreamTcp = _iniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamTCP");
        FakeHexStreamUdp = _iniSettingsHelper.GetValue<string>("Fakes", "FakeHexStreamUDP");
        FakeHexBytesTcp = _iniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesTCP");
        FakeHexBytesUdp = _iniSettingsHelper.GetValue<string>("Fakes", "FakeHexBytesUDP");
        PayloadTcp = _iniSettingsHelper.GetValue<string>("Fakes", "PayloadTCP");
        PayloadUdp = _iniSettingsHelper.GetValue<string>("Fakes", "PayloadUDP");
    }

    private void SaveGoodCheckSettings()
    {
        string targetFolder = Path.Combine(GoodCheckProcessService.GetAddOnDirectory(), "config.ini");

        _iniSettingsHelper ??= new(targetFolder, System.Text.Encoding.UTF8);

        SettingsManager.Instance.SetValue<string>(["ADDONS", AddOnId], "passesValue", PassesValue);
        SettingsManager.Instance.SetValue<bool>(["ADDONS", AddOnId], "useCurl", ResolverItems[ResolverIndex].Id == "curl");

        _iniSettingsHelper.SetValue<string>("General", "ConnectionTimeout", ConnectionTimeout);

        _iniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeTimeout", ResolverNativeTimeout);
        _iniSettingsHelper.SetValue<string>("Advanced", "ResolverNativeRetries", ResolverNativeRetries);
        _iniSettingsHelper.SetValue<string>("Advanced", "InternalTimeoutMs", InternalTimeoutMs);
        _iniSettingsHelper.SetValue<bool>("Advanced", "AutomaticConnectivityTest", AutomaticConnectivityTest);
        _iniSettingsHelper.SetValue<string>("Advanced", "ConnectivityTestURL", ConnectivityTestUrl);
        _iniSettingsHelper.SetValue<bool>("Advanced", "SkipCertVerify", SkipCertVerify);

        _iniSettingsHelper.SetValue<bool>("General", "AutomaticGoogleCacheTest", AutomaticGoogleCacheTest);
        _iniSettingsHelper.SetValue<string>("Advanced", "GoogleCacheMappingURLs", GoogleCacheMappingUrls);

        _iniSettingsHelper.SetValue<bool>("Resolvers", "UseDoH", UseDoH);
        _iniSettingsHelper.SetValue<string>("Resolvers", "DoHResolvers", DoHResolvers);

        _iniSettingsHelper.SetValue<string>("Fakes", "FakeSNI", FakeSni);
        _iniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamTCP", FakeHexStreamTcp);
        _iniSettingsHelper.SetValue<string>("Fakes", "FakeHexStreamUDP", FakeHexStreamUdp);
        _iniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesTCP", FakeHexBytesTcp);
        _iniSettingsHelper.SetValue<string>("Fakes", "FakeHexBytesUDP", FakeHexBytesUdp);
        _iniSettingsHelper.SetValue<string>("Fakes", "PayloadTCP", PayloadTcp);
        _iniSettingsHelper.SetValue<string>("Fakes", "PayloadUDP", PayloadUdp);

        string itemsFolder = Directories.StoreItemsDirectory;

        try
        {
            GoodCheckComponentPathsWriter.Write(_iniSettingsHelper, itemsFolder);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessService), $"Cannot set settings. Exception is {ex}");
            _ = ShowErrorDialog(_localizer.GetLocalizedString("GoodCheckComponentDirsSetException")); // ShowErrorDialog catches and logs its own errors.
        }
        _iniSettingsHelper.Save();
    }

    private void StartGoodCheck()
    {
        List<GoodCheckSiteListModel> listModels = new();

        if (!IsSeparateMode)
        {
            foreach (SiteListElement siteList in SelectedSiteLists)
            {
                GoodCheckSiteListModel model = new()
                {
                    SiteListPath = siteList.Directory,
                    StrategyListPath = SelectedStrategiesList.FilePath,
                };
                listModels.Add(model);
            }
        }
        else
        {
            foreach (OneSiteListOneStrategyModeElement element in _oneSiteListOneStrategyModeElements)
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
            IsSeparateMode ? GoodCheckProcessMode.AnyAsAny : GoodCheckProcessMode.AllAsOne,
            listModels
            );

        GoodCheckProcessService.Instance.ErrorHappens -= LogGoodCheckError;
        GoodCheckProcessService.Instance.ErrorHappens += LogGoodCheckError;

        GoodCheckProcessService.Instance.Start();
    }

    private static void LogGoodCheckError(string error)
    {
        Logger.Instance.CreateErrorLog(LogSource, error);
    }
}
