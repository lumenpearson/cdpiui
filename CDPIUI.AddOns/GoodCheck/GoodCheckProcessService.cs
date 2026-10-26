using System.Text;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.Store.Data;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.AddOns.GoodCheck;

public class GoodCheckProcessService
{
    public event Action<string>? AllComplete;
    public event Action<string>? ErrorHappens;
    public event Action<GoodCheckSiteListModel>? ProcessCompleted;
    public event Action<string>? CurrentSiteListChanged;
    public event Action<Tuple<string, string, string>>? ProgressChanged;
    public event Action<Tuple<int, int>>? StrategyResultAdded;
    public event Action<int>? CorrectCountChanged;
    public event Action<int>? IncorrectCountChanged;

    public event Action<Tuple<int, string>>? OperationOutputAdded;
    public event Action<Tuple<int, GoodCheckOperationType>>? OperationTypeChanged;
    public event Action? OperationsListChanged;

    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    // Null until the first site list starts.
    public string? CurrentSiteList { get; private set; }
    public Tuple<int, int>? CurrentSiteListIndex { get; private set; }

    private const string CheckLists = "CheckLists";

    private string _exeFileName = "";
    private string _dirName = "";
    private string _componentName = "";
    private string _componentId = "";

    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    private GoodCheckProcessMode _mode;
    private List<GoodCheckSiteListModel> _siteList = [];

    private static GoodCheckProcessService? _instance;
    private static readonly object SyncRoot = new object();

    public int CurrentOperationId { get; private set; } = 0;
    private List<GoodCheckOperationModel> _operations = new List<GoodCheckOperationModel>();

    private readonly GoodCheckOutputParser _outputParser = new();

    public static GoodCheckProcessService Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new GoodCheckProcessService();
                return _instance;
            }
        }
    }

    private GoodCheckProcessService()
    {

    }

    /// <summary>
    /// The folder GoodCheck is installed in, as recorded in the Store database.
    /// </summary>
    public static string GetAddOnDirectory() => GoodCheckInstallation.GetDirectory();

    public static bool IsAddOnInstalled() => GoodCheckInstallation.IsInstalled();

    private bool IsGoodCheckInstalled()
    {
        if (!GoodCheckInstallation.IsInstalled())
            return false;

        _dirName = GoodCheckInstallation.GetDirectory();
        _exeFileName = GoodCheckInstallation.GetExecutablePath(_dirName);

        return File.Exists(_exeFileName);
    }

    public static void MergeUniquePreserveOrder(
        List<GoodCheckSiteListModel> siteLists,
        string outputFile,
        Encoding? encoding = null,
        bool trimLines = true,
        bool ignoreEmpty = true,
        StringComparer? comparer = null) =>
        GoodCheckSiteListMerger.MergeUniquePreserveOrder(siteLists, outputFile, encoding, trimLines, ignoreEmpty, comparer);

    public void InitGoodCheck(
        string componentId,
        GoodCheckProcessMode checkMode,
        List<GoodCheckSiteListModel> siteListsToCheck
        )
    {
        _siteList.Clear();
        _operations.Clear();

        CurrentOperationId = 0;

        // GetKeyByValue returns default(Components), Zapret, for Zapret2 or NoDPI, and the
        // check then ran with the wrong component.
        if (!IsComponentSupported(componentId))
            throw new NotSupportedException($"GoodCheck does not support the component {componentId}.");
        _componentName = GoodCheckResultViewHelper.ComponentNormalNames[HardcodedItemIds.ComponentIds.GetKeyByValue(componentId)];

        _componentId = componentId;
        _mode = checkMode;

        string siteListFolder = Path.Combine(GetAddOnDirectory(), CheckLists);

        GoodCheckSiteListStager.RemoveOldMergedSiteLists(siteListFolder);

        if (_mode == GoodCheckProcessMode.AllAsOne)
        {
            GoodCheckSiteListStager.AddMergedSiteList(siteListFolder, siteListsToCheck, _siteList, _operations);
        }
        else
        {
            GoodCheckSiteListStager.AddSeparateSiteLists(siteListFolder, siteListsToCheck, _operations);
            _siteList = siteListsToCheck;
        }

        OperationsListChanged?.Invoke();

    }

    public void Start()
    {
        _cancellationTokenSource = new();
        _cancellationToken = _cancellationTokenSource.Token;
        _ = StartCheck(_cancellationToken);
    }

    public static bool IsComponentSupported(string componentId) =>
        GoodCheckResultViewHelper.ComponentNormalNames.Keys
            .Any(component => HardcodedItemIds.ComponentIds.TryGetValue(component, out string? id) && id == componentId);

    public void Stop()
    {
        // Stop before the first Start had no source to cancel.
        _cancellationTokenSource?.Cancel();
        _ = PipeHelper.SendGoodCheckPacket(Shared.Pipe.Models.GoodCheckMessageIds.Stop);
    }


    public async Task StartCheck(CancellationToken cancellationToken)
    {
        if (!IsGoodCheckInstalled())
        {
            ErrorHappens?.Invoke("ERR_ADDON_NOT_INSTALLED");
            return;
        }

        try
        {
            if (_siteList != null)
            {
                for (int i = 0; i < _siteList.Count; i++)
                {
                    CurrentOperationId = i;
                    _operations[CurrentOperationId].OperationType = GoodCheckOperationType.WorkInProgress;
                    OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.WorkInProgress));

                    var site = _siteList[i];
                    CurrentSiteList = $"[{i + 1}/{_siteList.Count}] {site.SiteListPath}";
                    CurrentSiteListChanged?.Invoke(CurrentSiteList);

                    CurrentSiteListIndex = Tuple.Create(i + 1, _siteList.Count);

                    if (cancellationToken.IsCancellationRequested || _cancellationToken.IsCancellationRequested)
                        break;

                    bool ok = await StartProcessAsync(site, cancellationToken).ConfigureAwait(false);
                    if (ok)
                    {
                        ProcessCompleted?.Invoke(site);
                        _operations[CurrentOperationId].OperationType = GoodCheckOperationType.SuccessFinish;
                        OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.SuccessFinish));
                    }

                    if (cancellationToken.IsCancellationRequested || _cancellationToken.IsCancellationRequested)
                        break;
                }

                CreateAndSaveReport();

            }
        }
        catch (OperationCanceledException)
        {
            // pass
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessService), $"ERR_INTERNAL happens: {ex}");
            ErrorHappens?.Invoke("ERR_INTERNAL");
            AllComplete?.Invoke(string.Empty);
        }
        finally
        {
            // A stopped or failed check left its operation shown as running.
            foreach (var operation in _operations.Where(o => o.OperationType == GoodCheckOperationType.WorkInProgress).ToList())
            {
                operation.OperationType = GoodCheckOperationType.UserInterrupt;
                OperationTypeChanged?.Invoke(Tuple.Create(operation.OperationId, GoodCheckOperationType.UserInterrupt));
            }
        }
    }

    private async Task<bool> StartProcessAsync(GoodCheckSiteListModel siteList, CancellationToken cancellationToken)
    {
        IncorrectCount = 0;
        CorrectCount = 0;

        if (string.IsNullOrEmpty(_exeFileName) || !File.Exists(_exeFileName))
        {
            HandleProcessException("ERR_EXE_NOT_FOUND", CurrentOperationId);
            return false;
        }

        _ = PipeHelper.SendGoodCheckPacket(
            Shared.Pipe.Models.GoodCheckMessageIds.Start,
            CurrentOperationId.ToString(),
            _exeFileName,
            GoodCheckCommandLine.Build(_componentName, siteList));

        string logsDirectory = Path.Combine(_dirName, "Logs");
        var monitorTask = MonitorProcessAndLogsAsync(logsDirectory, cancellationToken);

        try
        {
            await monitorTask.ConfigureAwait(false);

            bool result = await monitorTask.ConfigureAwait(false);

            GoodCheckOperationModel model = GetOperationById(CurrentOperationId);

            if (model.ProcessExited && !cancellationToken.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                // The process ended by itself: the result comes from its complete log.
                return result;
            }
            if (cancellationToken.IsCancellationRequested || model.CancellationToken.IsCancellationRequested || _cancellationToken.IsCancellationRequested)
            {
                _ = PipeHelper.SendGoodCheckPacket(Shared.Pipe.Models.GoodCheckMessageIds.Stop);
                return false;
            }

            return result;

        }
        catch
        {
            return false;
        }

    }

    private void AddOutput(string output)
    {
        GoodCheckOutputLine line = _outputParser.Parse(output);
        if (line.Progress != null)
        {
            ProgressChanged?.Invoke(line.Progress);
        }
        if (line.HasNoSuccesses)
        {
            IncorrectCount++;
            IncorrectCountChanged?.Invoke(IncorrectCount);
        }

        if (line.StrategyResult != null)
        {
            (int currentInt, int allInt) = line.StrategyResult;

            StrategyResultAdded?.Invoke(line.StrategyResult);

            if (allInt != 0 && currentInt * 100 / allInt >= 65)
            {
                CorrectCount++;
                CorrectCountChanged?.Invoke(CorrectCount);
            }
            else if (currentInt != 0)
            {
                IncorrectCount++;
                IncorrectCountChanged?.Invoke(IncorrectCount);
            }
        }

        OperationOutputAdded?.Invoke(Tuple.Create(CurrentOperationId, $"{output}"));
        _operations[CurrentOperationId].AppendOutputLine(output);
    }

    private async Task<bool> MonitorProcessAndLogsAsync(string logsDirectory, CancellationToken cancellationToken)
    {
        const string errorFlag = "Exiting with an error...";
        const string successFlag = "All Done";

        GoodCheckOperationModel model = GetOperationById(CurrentOperationId);
        using var log = new GoodCheckLogTail(logsDirectory, DateTime.UtcNow.AddSeconds(-2));

        // Returns true or false when the line decides the result, otherwise null.
        bool? CheckLines(string text)
        {
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                AddOutput(line);

                if (line.IndexOf(errorFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HandleProcessException("ERR_GOODCHECK_EXCEPTION", CurrentOperationId);
                    return false;
                }
                if (line.IndexOf(successFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return null;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested && !model.CancellationToken.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                if (!log.TryOpenNewest())
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                string newText = await log.ReadNewTextAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(newText))
                {
                    bool? decided = CheckLines(newText);
                    if (decided != null)
                        return decided.Value;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            if (model.ProcessExited && !cancellationToken.IsCancellationRequested && !_cancellationToken.IsCancellationRequested)
            {
                // The process ended: read what it wrote last, and a run without "All Done",
                // such as a failed connectivity check, is no success.
                string rest = await log.ReadNewTextAsync().ConfigureAwait(false);
                bool? decided = string.IsNullOrEmpty(rest) ? null : CheckLines(rest);
                if (decided != null)
                    return decided.Value;
                HandleProcessException("ERR_GOODCHECK_EXITED_EARLY", CurrentOperationId);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _operations[CurrentOperationId].OperationType = GoodCheckOperationType.UserInterrupt;
            OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.UserInterrupt));
            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessService), $"ERR_MONITOR_EXCEPTION happens: {ex}");
            HandleProcessException("ERR_MONITOR_EXCEPTION", CurrentOperationId);
            return false;
        }

        return false;
    }

    public void HandleProcessException(string errorCode, int operationId)
    {
        _operations[operationId].OperationType = GoodCheckOperationType.ErrorHappens;
        _operations[operationId].ErrorCode = errorCode;
        OperationTypeChanged?.Invoke(Tuple.Create(operationId, GoodCheckOperationType.ErrorHappens));
        ErrorHappens?.Invoke(errorCode);
    }

    /// <summary>
    /// The tray could not start GoodCheck or it ended with an error. The log monitor waited for
    /// a process that never ran until the user cancelled; it stops now.
    /// </summary>
    public void OperationWithIdFailed(int id, string errorCode)
    {
        HandleProcessException(string.IsNullOrEmpty(errorCode) ? "ERR_UNKNOWN" : errorCode, id);
        GoodCheckOperationModel model = GetOperationById(id);
        if (model == null)
            return;
        model.ProcessExited = true;
        model.CancellationTokenSource?.Cancel();
    }

    public void OperationWithIdDied(int id)
    {
        // Ended by itself: the log monitor decides whether it was a success. Marking it as one
        // here turned an early failure into a success with an empty report.
        GoodCheckOperationModel model = GetOperationById(id);
        model.ProcessExited = true;
        model.CancellationTokenSource?.Cancel();
    }

    public void RemoveFromQueueOrStopOperation(int id)
    {
        GoodCheckOperationModel model = GetOperationById(id);
        model.OperationType = GoodCheckOperationType.UserInterrupt;
        model.CancellationTokenSource.Cancel();
        // model.CancellationTokenSource.Dispose();

        OperationTypeChanged?.Invoke(Tuple.Create(id, GoodCheckOperationType.UserInterrupt));
    }

    private void CreateAndSaveReport()
    {
        string filePath = GoodCheckReportWriter.Save(GetAddOnDirectory(), _operations, _componentId);
        AllComplete?.Invoke(filePath);
    }

    public List<GoodCheckOperationModel> GetCurrentOperations()
    {
        return _operations;
    }
    public GoodCheckOperationModel? GetOperationById(int id)
    {
        try
        {
            return _operations[id];
        }
        catch { return null; }
    }

    public bool IsRunning()
    {
        return _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;
    }

    public void Kill()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Kill was already called; the check is stopped.
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessService), $"Cannot stop the check: {ex.Message}");
        }
    }

}
