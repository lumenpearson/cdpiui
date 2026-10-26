using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.ProxySetupUtil;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Proxy;
using Newtonsoft.Json;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.Proxy;

/// <summary>
/// State of the ProxiFyre setup page: the protocol mode, the application white and black
/// lists, the proxy address, and reading and writing the ProxiFyre app-config.json file.
/// </summary>
public sealed partial class ProxiFyreSetupViewModel : ObservableObject
{
    private const string LogSource = "ProxiFyreSetupPage";
    private const string AddOnId = "ASPEWK002";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IProxiFyreSetupView _view;

    private List<SelectedApplicationInfo> _whiteList = [];
    private List<SelectedApplicationInfo> _blackList = [];

    private ProxiFyreModeOption _selectedMode;
    private string _ipAddress = string.Empty;
    private string _port = string.Empty;
    private string _whiteListDescription;
    private string _blackListDescription;

    public ProxiFyreSetupViewModel(IProxiFyreSetupView view)
    {
        _view = view;

        Modes.Add(new ProxiFyreModeOption()
        {
            DisplayName = "TCP",
            Mode = ProxiFyreMode.TCP,
        });
        Modes.Add(new ProxiFyreModeOption()
        {
            DisplayName = "UDP",
            Mode = ProxiFyreMode.UDP,
        });
        Modes.Add(new ProxiFyreModeOption()
        {
            DisplayName = "TCP+UDP",
            Mode = ProxiFyreMode.Both
        });

        ResetAddressCommand = new RelayCommand(ResetAddress);
        // The edit methods catch and log their own errors.
        EditWhiteListCommand = new RelayCommand(() => _ = EditWhiteListAsync());
        EditBlackListCommand = new RelayCommand(() => _ = EditBlackListAsync());

        UpdateDescriptions();
        LoadSettings();
    }

    public ObservableCollection<ProxiFyreModeOption> Modes { get; } = [];

    public ICommand ResetAddressCommand { get; }

    public ICommand EditWhiteListCommand { get; }

    public ICommand EditBlackListCommand { get; }

    public ProxiFyreModeOption SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string WhiteListDescription
    {
        get => _whiteListDescription;
        private set => SetProperty(ref _whiteListDescription, value);
    }

    public string BlackListDescription
    {
        get => _blackListDescription;
        private set => SetProperty(ref _blackListDescription, value);
    }

    /// <summary>Confirms the proxy address and saves the settings; true when the page can go on.</summary>
    public async Task<bool> ConfirmAndSaveAsync()
    {
        if (!await _view.ConfirmProxyAddressAsync(IpAddress))
            return false;
        return SaveSettings();
    }

    private static string GetProxyIpString(string ip, string port)
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

    private ProxiFyreModeOption FindMode(ProxiFyreMode mode) => Modes.FirstOrDefault(x => x.Mode == mode);

    private void LoadSettings()
    {
        string dir = Path.Combine(Directories.StoreItemsDirectory, AddOnId);
        if (!Path.Exists(dir))
            return; // TODO: show error message
        string settingsFile = Path.Combine(dir, "app-config.json");

        if (!File.Exists(settingsFile))
        {
            SelectedMode = FindMode(ProxiFyreMode.Both);

            IpAddress = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            Port = SettingsManager.Instance.GetValue<string>("PROXY", "port");
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
                SelectedMode = FindMode(ProxiFyreMode.Both);
            }
            else if (protocols.Contains("UDP"))
            {
                SelectedMode = FindMode(ProxiFyreMode.UDP);
            }
            else
            {
                SelectedMode = FindMode(ProxiFyreMode.TCP);
            }

            foreach (string app in settings?.excludes ?? [])
            {
                _blackList.Add(new()
                {
                    DisplayName = Path.GetFileNameWithoutExtension(app),
                    FullPath = app,
                });
            }

            IpAddress = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            Port = SettingsManager.Instance.GetValue<string>("PROXY", "port");
        }
        UpdateDescriptions();
    }

    private static ProxiFyreSettings? TryLoadSettings(string settingsFile)
    {
        try
        {
            return JSONConvertor.LoadJson<ProxiFyreSettings>(settingsFile);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(LogSource, $"Cannot read \"{settingsFile}\": {ex.Message}");
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

        if (File.Exists(settingsFile))
        {
            // A damaged app-config.json is replaced instead of failing the setup.
            settings = TryLoadSettings(settingsFile) ?? new();
        }
        settings.proxies ??= [];

        string readyToCompareIpAddr = GetProxyIpString(IpAddress, Port);

        if (string.IsNullOrEmpty(readyToCompareIpAddr))
            return false;

        SettingsManager.Instance.SetValue("PROXY", "IPAddress", IpAddress);
        SettingsManager.Instance.SetValue("PROXY", "port", Port);

        bool flag = false;
        settings.proxies.RemoveAll(group => group == null);
        foreach (var proxyGroup in settings.proxies)
        {
            if (proxyGroup.socks5ProxyEndpoint == readyToCompareIpAddr)
            {
                proxyGroup.supportedProtocols = GetProtocols(SelectedMode.Mode);
                proxyGroup.appNames = GetAppNames(_whiteList);
                flag = true;
            }
        }
        if (!flag)
        {
            settings.proxies.Add(new()
            {
                socks5ProxyEndpoint = readyToCompareIpAddr,
                appNames = GetAppNames(_whiteList),
                supportedProtocols = GetProtocols(SelectedMode.Mode)
            });
        }

        ProxiFyreSettings readyToSaveSettings = new()
        {
            logLevel = settings.logLevel ?? "Error",
            proxies = settings.proxies,
            excludes = GetAppNames(_blackList),
            extensionData = settings.extensionData
        };

        string jsonString = JsonConvert.SerializeObject(readyToSaveSettings);
        Logger.Instance.CreateDebugLog(LogSource, jsonString);
        File.WriteAllText(settingsFile, jsonString);

        SettingsManager.Instance.SetValue("PROXY", "proxyType", ProxySetupTypes.ProxiFyre.ToString());

        return true;
    }

    private static List<string> GetAppNames(List<SelectedApplicationInfo> info) =>
        info.Select(app => app.FullPath).ToList();

    private static List<string> GetProtocols(ProxiFyreMode mode) => mode switch
    {
        ProxiFyreMode.TCP => ["TCP"],
        ProxiFyreMode.UDP => ["UDP"],
        _ => ["TCP", "UDP"],
    };

    private void ResetAddress()
    {
        IpAddress = "127.0.0.1";
        Port = "1080";
    }

    private void UpdateDescriptions()
    {
        WhiteListDescription = BuildDescription(_whiteList);
        BlackListDescription = BuildDescription(_blackList);
    }

    private string BuildDescription(List<SelectedApplicationInfo> applications)
    {
        if (applications.Count == 0)
            return _localizer.GetLocalizedString("ApplicationListIsEmpty");

        string apps = string.Empty;
        int i = 0;
        foreach (var app in applications)
        {
            if (i >= 3)
            {
                apps += " " + string.Format(_localizer.GetLocalizedString("AndMore"), applications.Count - 3);
                break;
            }

            apps += i == 0 ? app.DisplayName : $", {app.DisplayName}";
            i++;
        }

        return string.Format(_localizer.GetLocalizedString("SelectedAppsSettingsCardDescription"), apps);
    }

    private async Task EditWhiteListAsync()
    {
        try
        {
            string title = string.Format(_localizer.GetLocalizedString("EditApplicationListDialogTitle"), _localizer.GetLocalizedString("ApplicationWhiteList"));
            _whiteList = await _view.EditApplicationsAsync(_whiteList, title);
            UpdateDescriptions();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }

    private async Task EditBlackListAsync()
    {
        try
        {
            string title = string.Format(_localizer.GetLocalizedString("EditApplicationListDialogTitle"), _localizer.GetLocalizedString("ApplicationBlackList"));
            _blackList = await _view.EditApplicationsAsync(_blackList, title);
            UpdateDescriptions();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
        }
    }
}
