using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Core.Store;

namespace CDPIUI.Helper.Basic;

public class ApplicationTaskMonitor
{
    public Action<bool> StoreStateChanged;

    private static ApplicationTaskMonitor _instance;
    private static readonly object SyncRoot = new object();

    public static ApplicationTaskMonitor Instance
    {
        get
        {
            lock (SyncRoot)
            {
                if (_instance == null)
                    _instance = new ApplicationTaskMonitor();
                return _instance;
            }
        }
    }

    private ApplicationTaskMonitor()
    {
        StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
        StoreHelper.Instance.NowProcessItemActions += StoreHelper_NowProcessItemActions;
    }

    private void StoreHelper_NowProcessItemActions(string obj)
    {
        if (!IsStoreWorking())
        {
            StoreStateChanged?.Invoke(false);
        }
        else
        {
            StoreStateChanged?.Invoke(true);
        }
    }

    private void StoreHelper_ItemActionsStopped(string obj)
    {
        if (!IsStoreWorking() || StoreHelper.Instance.GetQueue().Count == 0)
        {
            StoreStateChanged?.Invoke(false);
        }
        else
        {
            StoreStateChanged?.Invoke(true);
        }
    }
    /*
    public static bool IsComponentProcessRunning()
    {
        return ProcessManager.Instance.IsProcessRunning;
    }
    */

    public static bool IsGoodCheckRunning()
    {
        return GoodCheckProcessService.Instance.IsRunning();
    }

    public static bool IsStoreWorking()
    {
        return StoreHelper.Instance.IsNowUpdatesChecked || !string.IsNullOrEmpty(StoreHelper.Instance.GetCurrentQueueOperationId());
    }
}
