using System.Diagnostics;
using System.Xml.Linq;
using CDPIUI.Shared.Basic;

namespace CDPIUI.TrayIcon.Helper;

public class SettingsManager : XMLSettingsService
{
    private static SettingsManager? _instance;
    private static readonly object SyncRoot = new object();

    public static SettingsManager Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new SettingsManager();
                return _instance;
            }
        }
    }
    private SettingsManager() : base(Utils.GetSettingsFile()) { }
}
