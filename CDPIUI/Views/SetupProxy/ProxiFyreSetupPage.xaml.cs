using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.ProxySetupUtil;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Proxy;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage.Pickers;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;
using SelectedApplicationInfo = CDPIUI.Controls.Dialogs.ProxySetupUtil.SelectedApplicationInfo;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.SetupProxy;



public class ProxiFyreProxyGroup
{
    public List<string> appNames { get; set; }
    public string socks5ProxyEndpoint { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string username { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string password { get; set; }
    public List<string> supportedProtocols { get; set; }

    // Keys this page does not know are written back unchanged.
    [JsonExtensionData]
    public IDictionary<string, JToken> extensionData { get; set; }
}

public class ProxiFyreSettings
{
    public string logLevel { get; set; } = "Error";
    public List<ProxiFyreProxyGroup> proxies { get; set; } = [];
    public List<string> excludes { get; set; } = [];

    // Keys this page does not know are written back unchanged.
    [JsonExtensionData]
    public IDictionary<string, JToken> extensionData { get; set; }
}

public sealed partial class ProxiFyreSetupPage : TemplatePage
{
    private List<SelectedApplicationInfo> _whiteList = [];
    private List<SelectedApplicationInfo> _blackList = [];

    private ILocalizer _localizer = Localizer.Get();

    private const string AddOnId = "ASPEWK002";

    private enum ProxiFyreModes
    {
        TCP,
        UDP,
        Both
    }
    private class ProxiFyreModeElement
    {
        public string DisplayName { get; set; }
        public ProxiFyreModes Mode { get; set; }
    }

    private ObservableCollection<ProxiFyreModeElement> _proxiFyreModes = [];

    public ProxiFyreSetupPage()
    {
        InitializeComponent();
        this.DataContext = this;

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;

        _proxiFyreModes.Add(new ProxiFyreModeElement()
        {
            DisplayName = "TCP",
            Mode = ProxiFyreModes.TCP,
        });
        _proxiFyreModes.Add(new ProxiFyreModeElement()
        {
            DisplayName = "UDP",
            Mode = ProxiFyreModes.UDP,
        });
        _proxiFyreModes.Add(new ProxiFyreModeElement()
        {
            DisplayName = "TCP+UDP",
            Mode = ProxiFyreModes.Both
        });
        ProxiFyreModeComboBox.ItemsSource = _proxiFyreModes;
        ProxiFyreModeComboBox.SelectionChanged += ProxiFyreModeComboBox_SelectionChanged;
        SetDescription();
        GetSettings();
    }

    private void ProxiFyreModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        PrepareToConnectedBackwardAnimate(ActionButtonsGrid, new DirectConnectedAnimationConfiguration());
        if (Frame.CanGoBack)
            Frame.GoBack();
    }

    private void Navigate<T>() where T : Page
    {
        PrepareToConnectedForwardAnimate(ActionButtonsGrid, new DirectConnectedAnimationConfiguration());

        Frame.Navigate(typeof(T), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open help link
    }

    private async void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await global::CDPIUI.Helper.Basic.ProxyAddressWarning.ConfirmAsync(XamlRoot, IPValue.Text))
            return;
        if (SaveSettings())
            Navigate<ProxySetupCompletePage>();
    }



    private string GetProxyIpString(string ip, string port)
    {
        if (!ProxyHelper.IsValidPort(port))
            return string.Empty;
        if (IPAddress.TryParse(ip, out var address))
        {
            return address.AddressFamily switch
            {
                System.Net.Sockets.AddressFamily.InterNetwork => $"{ip}:{port}",
                System.Net.Sockets.AddressFamily.InterNetworkV6 => $"[{ip}]:{port}",
                _ => "",
            };
        }
        return string.Empty;
    }

    private void GetSettings()
    {
        string dir = Path.Combine(Directories.StoreItemsDirectory, AddOnId);
        if (!Path.Exists(dir))
            return; // TODO: show error message
        string settingsFile = Path.Combine(dir, "app-config.json");

        if (!File.Exists(settingsFile))
        {
            ProxiFyreModeComboBox.SelectedItem = _proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.Both);

            IPValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            PortValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "port");
        }
        else
        {
            string readyToCompareIpAddr = GetProxyIpString(
                SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress"), SettingsManager.Instance.GetValue<string>("PROXY", "port"));

            ProxiFyreSettings? settings = TryLoadSettings(settingsFile);
            ProxiFyreProxyGroup? proxyGroup = settings?.proxies?.FirstOrDefault(x => x?.socks5ProxyEndpoint == readyToCompareIpAddr)
                ?? settings?.proxies?.FirstOrDefault(x => x != null);

            foreach (string app in proxyGroup?.appNames ?? [])
            {
                _whiteList.Add(new()
                {
                    DisplayName = Path.GetFileNameWithoutExtension(app),
                    FullPath = app,
                });
            }

            // An empty proxies list or null fields in app-config.json threw a NullReferenceException.
            var protocols = proxyGroup?.supportedProtocols;
            if (protocols == null || (protocols.Contains("UDP") && protocols.Contains("TCP")))
            {
                ProxiFyreModeComboBox.SelectedItem = _proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.Both);
            }
            else if (protocols.Contains("UDP"))
            {
                ProxiFyreModeComboBox.SelectedItem = _proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.UDP);
            }
            else
            {
                ProxiFyreModeComboBox.SelectedItem = _proxiFyreModes.FirstOrDefault(x => x.Mode == ProxiFyreModes.TCP);
            }

            foreach (string app in settings?.excludes ?? [])
            {
                _blackList.Add(new()
                {
                    DisplayName = Path.GetFileNameWithoutExtension(app),
                    FullPath = app,
                });
            }

            IPValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            PortValue.Text = SettingsManager.Instance.GetValue<string>("PROXY", "port");

        }
        SetDescription();
    }

    private static ProxiFyreSettings? TryLoadSettings(string settingsFile)
    {
        try
        {
            return JSONConvertor.LoadJson<ProxiFyreSettings>(settingsFile);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(ProxiFyreSetupPage), $"Cannot read \"{settingsFile}\": {ex.Message}");
            return null;
        }
    }

    private bool SaveSettings()
    {
        string dir = Path.Combine(Directories.StoreItemsDirectory, AddOnId);
        if (!Path.Exists(dir))
            return false; // TODO: show error message
        string settingsFile = Path.Combine(dir, "app-config.json");

        ProxiFyreSettings settings = new();


        if (!File.Exists(settingsFile))
        {

        }
        else
        {
            // A damaged app-config.json is replaced instead of failing the setup.
            settings = TryLoadSettings(settingsFile) ?? new();
        }
        settings.proxies ??= [];

        string readyToCompareIpAddr = GetProxyIpString(IPValue.Text, PortValue.Text);

        if (string.IsNullOrEmpty(readyToCompareIpAddr))
        {
            return false;
        }

        SettingsManager.Instance.SetValue("PROXY", "IPAddress", IPValue.Text);
        SettingsManager.Instance.SetValue("PROXY", "port", PortValue.Text);


        bool flag = false;
        if (settings.proxies != null)
        {
            settings.proxies.RemoveAll(group => group == null);
            foreach (var proxyGroup in settings.proxies)
            {
                if (proxyGroup.socks5ProxyEndpoint == readyToCompareIpAddr)
                {
                    proxyGroup.supportedProtocols = GetProtocolFromProxiFyreMode(((ProxiFyreModeElement)ProxiFyreModeComboBox.SelectedItem).Mode);
                    proxyGroup.appNames = GetAppNamesFromModel(_whiteList);
                    flag = true;
                }
            }
        }
        if (!flag)
        {
            settings.proxies.Add(new()
            {
                socks5ProxyEndpoint = readyToCompareIpAddr,
                appNames = GetAppNamesFromModel(_whiteList),
                supportedProtocols = GetProtocolFromProxiFyreMode(((ProxiFyreModeElement)ProxiFyreModeComboBox.SelectedItem).Mode)
            });
        }

        ProxiFyreSettings readyToSaveSettigs = new()
        {
            logLevel = settings.logLevel ?? "Error",
            proxies = settings.proxies,
            excludes = GetAppNamesFromModel(_blackList),
            extensionData = settings.extensionData
        };

        string jsonString = JsonConvert.SerializeObject(readyToSaveSettigs);
        Logger.Instance.CreateDebugLog(nameof(ProxiFyreSetupPage), jsonString);
        File.WriteAllText(settingsFile, jsonString);

        SettingsManager.Instance.SetValue("PROXY", "proxyType", ProxySetupTypes.ProxiFyre.ToString());


        return true;
    }

    private List<string> GetAppNamesFromModel(List<SelectedApplicationInfo> info)
    {
        List<string> apps = [];
        foreach (var app in info)
        {
            apps.Add(app.FullPath);
        }
        return apps;
    }

    private List<string> GetProtocolFromProxiFyreMode(ProxiFyreModes mode)
    {
        switch (mode)
        {
            case ProxiFyreModes.TCP:
                return ["TCP"];
            case ProxiFyreModes.UDP:
                return ["UDP"];
            default:
                return ["TCP", "UDP"];
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        IPValue.Text = "127.0.0.1";
        PortValue.Text = "1080";
    }

    private void SetDescription()
    {
        string whiteListApps = string.Empty;

        int i = 0;
        foreach (var app in _whiteList)
        {
            if (i < 3)
                if (_whiteList.Count <= 1 || i == 0)
                    whiteListApps += $"{app.DisplayName}";
                else
                    whiteListApps += $", {app.DisplayName}";
            else
            {
                whiteListApps += " " + string.Format(_localizer.GetLocalizedString("AndMore"), _whiteList.Count - 3);
                break;
            }
            i++;
        }

        string whiteListDescription = string.Format(_localizer.GetLocalizedString("SelectedAppsSettingsCardDescription"), whiteListApps);
        if (_whiteList.Count > 0)
            ApplicationWhiteListCard.Description = whiteListDescription;
        else
            ApplicationWhiteListCard.Description = _localizer.GetLocalizedString("ApplicationListIsEmpty");

        string blackListApps = string.Empty;

        int j = 0;
        foreach (var app in _blackList)
        {
            if (j < 3)
                if (_blackList.Count <= 1 || j == 0)
                    blackListApps += $"{app.DisplayName}";
                else
                    blackListApps += $", {app.DisplayName}";
            else
            {
                blackListApps += " " + string.Format(_localizer.GetLocalizedString("AndMore"), _blackList.Count - 3);
                break;
            }
            j++;
        }

        string blackListDescription = string.Format(_localizer.GetLocalizedString("SelectedAppsSettingsCardDescription"), blackListApps);
        if (_blackList.Count > 0)
            ApplicationBlackListCard.Description = blackListDescription;
        else
            ApplicationBlackListCard.Description = _localizer.GetLocalizedString("ApplicationListIsEmpty");
    }

    private async void ApplicationBlackListCard_Click(object sender, RoutedEventArgs e)
    {
        SelectAppContentDialog dialog = new SelectAppContentDialog(_blackList);
        dialog.XamlRoot = this.XamlRoot;
        dialog.Title = string.Format(_localizer.GetLocalizedString("EditApplicationListDialogTitle"), _localizer.GetLocalizedString("ApplicationBlackList"));
        await dialog.ShowQueuedAsync();

        _blackList = dialog.Applications.ToList();
        SetDescription();
    }

    private async void ApplicationWhiteListCard_Click(object sender, RoutedEventArgs e)
    {
        SelectAppContentDialog dialog = new SelectAppContentDialog(_whiteList);
        dialog.XamlRoot = this.XamlRoot;
        dialog.Title = string.Format(_localizer.GetLocalizedString("EditApplicationListDialogTitle"), _localizer.GetLocalizedString("ApplicationWhiteList"));
        await dialog.ShowQueuedAsync();

        _whiteList = dialog.Applications.ToList();
        SetDescription();
    }
}
