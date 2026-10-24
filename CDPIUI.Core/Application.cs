using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core;

public class ApplicationInfo
{
    private static ApplicationInfo? _instance;
    private static readonly object _lock = new();
    public static ApplicationInfo Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new ApplicationInfo();
                return _instance;
            }
        }
    }

    public string Localization { get; private set; } = "en-US";
    public void SetLocalization(string key)
    {
        Localization = key;
    }



    // Read once: it was read from disk for every HTTP request.
    private static readonly Lazy<string> _version = new(GetVersion);

    public static string Version => _version.Value;

    private static string GetVersion()
    {
        string location = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(location))
            return "0.0.0.0";

        return FileVersionInfo.GetVersionInfo(location).FileVersion ?? "0.0.0.0";
    }


}
