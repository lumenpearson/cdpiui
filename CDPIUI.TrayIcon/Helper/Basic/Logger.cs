using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Logger;

namespace CDPIUI.TrayIcon.Helper.Basic;

public class Logger : LoggerBase, ILogger
{
    private static Logger? _instance;
    private static readonly object SyncRoot = new();

    public static Logger Instance
    {
        get
        {
            lock (SyncRoot)
            {
                if (_instance == null)
                    _instance = new Logger();
                return _instance;
            }
        }
    }



    public Logger() : base(Utils.GetDataDirectory(), GetSelevirity()) { }

    private static LogSelevirity GetSelevirity()
    {
#if DEBUG
        return LogSelevirity.DEBG;
#else
        return SettingsManager.Instance.GetValue<string>("DEBUG", "logLevel").ToEnum<LogSelevirity>(LogSelevirity.WARN);
#endif
    }
}
