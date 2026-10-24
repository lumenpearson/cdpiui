using System.Diagnostics;
using System.Xml.Linq;
using CDPIUI.Shared.Basic;

namespace CDPIUI.TrayIcon.Helper;

public class SettingsManager : XMLSettingsService
{
    private static SettingsManager? _instance;
    private static readonly object _lock = new object();

    public static SettingsManager Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new SettingsManager();
                return _instance;
            }
        }
    }
    private SettingsManager() : base(Utils.GetSettingsFile()) { }
}
