using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.AddOns.GoodCheck;

public enum GoodCheckProcessMode
{
    AllAsOne, // All site lists -> One strategy list
    AnyAsAny, // One site list -> One strategy list
}
public class GoodCheckSiteListModel
{
    public string? SiteListPath { get; set; }
    public string? StrategyListPath { get; set; }
}

public class GoodCheckOperationModel
{
    public int OperationId { get; set; }
    public string? SiteListName { get; set; }
    public string? SiteListPath { get; set; }
    // Appended line by line during a long run: string concatenation copied the whole log
    // on every line.
    private readonly System.Text.StringBuilder _output = new();
    public string Output
    {
        get { lock (_output) return _output.ToString(); }
        set { lock (_output) _output.Clear().Append(value); }
    }
    public void AppendOutputLine(string line)
    {
        lock (_output)
            _output.Append(line).Append('\n');
    }
    public GoodCheckOperationType OperationType { get; set; } = GoodCheckOperationType.Wait;
    public string ErrorCode { get; set; } = "";
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public CancellationToken CancellationToken { get; set; }
    /// <summary>The tray reported that the GoodCheck process ended.</summary>
    public bool ProcessExited { get; set; }
}
public enum GoodCheckOperationType
{
    Wait,
    ErrorHappens,
    SuccessFinish,
    UserInterrupt,
    WorkInProgress,
}
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

    private static readonly string AddonId = HardcodedItemIds.AddOnsIds[Core.Store.Data.AddOns.GoodCheck];
    private const string CheckLists = "CheckLists";

    private string _exeFileName = "";
    private string _dirName = "";
    private string _componentName = "";
    private string ComponentId = "";


    private const string GetProgressRegex = @"Launching '[a-zA-Z]{1,}', strategy (\d{1,})/(\d{1,}): \[(.*?)\]";
    private const string GetStrategyCountRegex = @"worst result for this strategy: (\d{1,})/(\d{1,})\n{1,}Terminating program\.\.\.";
    private const string AnalyzeRegex = @"Launching '[a-zA-Z]{1,}', strategy (\d{1,})/(\d{1,}): \[(.*?)\](?:.*?)worst result for this strategy: (\d{1,})/(\d{1,})(\n||This strategy has no successes){1,}Terminating program\.\.\.";

    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    private GoodCheckProcessMode _mode;
    private List<GoodCheckSiteListModel> _siteList = [];

    private static GoodCheckProcessService? _instance;
    private static readonly object SyncRoot = new object();

    public int CurrentOperationId { get; private set; } = 0;
    private List<GoodCheckOperationModel> _operations = new List<GoodCheckOperationModel>();

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
    public static string GetAddOnDirectory()
    {
        if (DatabaseHelper.Instance.IsItemInstalled(AddonId))
        {
            string? directory = DatabaseHelper.Instance.GetItemById(AddonId)?.Directory;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }
        return Path.Combine(Directories.StoreItemsDirectory, AddonId);
    }

    public static bool IsAddOnInstalled() => DatabaseHelper.Instance.IsItemInstalled(AddonId);

    private bool IsGoodCheckInstalled()
    {
        bool isInstalled = DatabaseHelper.Instance.IsItemInstalled(AddonId);

        if (!isInstalled)
            return false;

        DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(AddonId);

        _dirName = GetAddOnDirectory();

        _exeFileName = Path.Combine(
            _dirName,
            item.Executable + ".exe");

        return File.Exists(_exeFileName);
    }

    public static void MergeUniquePreserveOrder(
        List<GoodCheckSiteListModel> siteLists,
        string outputFile,
        Encoding? encoding = null,
        bool trimLines = true,
        bool ignoreEmpty = true,
        StringComparer? comparer = null)
    {
        // Without BOM: GoodCheck took it for a part of the first domain.
        encoding ??= new UTF8Encoding(false);
        comparer ??= StringComparer.Ordinal;

        var seen = new HashSet<string>(comparer);

        using var writer = new StreamWriter(outputFile, false, encoding);
        foreach (var siteList in siteLists)
        {
            if (!File.Exists(siteList.SiteListPath))
                continue;
            using var reader = new StreamReader(siteList.SiteListPath, encoding);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (trimLines)
                    line = line.Trim();
                if (ignoreEmpty && string.IsNullOrEmpty(line))
                    continue;

                if (seen.Add(line))
                {
                    writer.WriteLine(line);
                }
            }
        }
    }

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

        ComponentId = componentId;
        _mode = checkMode;

        string siteListFolder = Path.Combine(GetAddOnDirectory(), CheckLists);

        RemoveOldMergedSiteLists(siteListFolder);

        if (_mode == GoodCheckProcessMode.AllAsOne)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            string siteList = Path.Combine(siteListFolder, $"{secondsSinceEpoch}_sitelist.txt");

            MergeUniquePreserveOrder(siteListsToCheck, siteList);

            GoodCheckSiteListModel model = new()
            {
                SiteListPath = Path.GetFileName(siteList),
                StrategyListPath = Path.GetFileName(siteListsToCheck[0].StrategyListPath),
            };
            _siteList.Add(model);
            CancellationTokenSource cancellationTokenSource = new();
            _operations.Add(new()
            {
                OperationId = 0,
                SiteListName = Path.GetFileName(siteList),
                SiteListPath = siteList,
                CancellationTokenSource = cancellationTokenSource,
                CancellationToken = cancellationTokenSource.Token
            });
        }
        else
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < siteListsToCheck.Count; i++)
            {
                var model = siteListsToCheck[i];
                // The same exception that File.Copy threw for a list without a path.
                string path = model.SiteListPath
                    ?? throw new ArgumentNullException(nameof(siteListsToCheck), "A site list has no path.");
                // Lists with the same file name from different packs overwrote each other.
                string name = Path.GetFileName(path);
                if (!usedNames.Add(name))
                {
                    name = $"{i}_{name}";
                    usedNames.Add(name);
                }
                File.Copy(path, Path.Combine(siteListFolder, name), true);

                model.SiteListPath = name;
                model.StrategyListPath = Path.GetFileName(model.StrategyListPath);

                CancellationTokenSource cancellationTokenSource = new();

                _operations.Add(new()
                {
                    OperationId = i,
                    SiteListName = model.SiteListPath,
                    SiteListPath = path,
                    CancellationTokenSource = cancellationTokenSource,
                    CancellationToken = cancellationTokenSource.Token
                });
            }
            _siteList = siteListsToCheck;
        }

        OperationsListChanged?.Invoke();

    }

    // The merged lists of earlier runs ({epoch}_sitelist.txt) were never removed.
    private static void RemoveOldMergedSiteLists(string siteListFolder)
    {
        try
        {
            if (!Directory.Exists(siteListFolder))
                return;
            foreach (string file in Directory.EnumerateFiles(siteListFolder, "*_sitelist.txt"))
            {
                string prefix = Path.GetFileName(file).Split('_')[0];
                if (prefix.All(char.IsDigit) && File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1))
                    File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessService), $"Cannot remove old site lists: {ex.Message}");
        }
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
        string resolver = SettingsManager.Instance.GetValue<bool>(["ADDONS", AddonId], "useCurl") ? "curl" : "native";
        // A number from a text box: anything else would add arguments of its own.
        string passes = SettingsManager.Instance.GetValue<string>(["ADDONS", AddonId], "passesValue") ?? string.Empty;
        if (!int.TryParse(passes.Trim(), out int passCount) || passCount < 1)
            passCount = 1;
        string args =
            $"-q " +
            $"-p {passCount} " +
            $"-f {_componentName} " +
            $"-m {resolver} " +
            $"-c \"{siteList.SiteListPath}\" " +
            $"-s \"{siteList.StrategyListPath}\" ";

        _ = PipeHelper.SendGoodCheckPacket(
            Shared.Pipe.Models.GoodCheckMessageIds.Start,
            CurrentOperationId.ToString(),
            _exeFileName,
            args);

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
        finally
        {

        }

    }

    string? _goodStrategyCountString;

    private void AddOutput(string output)
    {
        Match progressMatch = Regex.Match(output, GetProgressRegex);
        if (progressMatch.Success)
        {
            string current = progressMatch.Groups[1].Value;
            string all = progressMatch.Groups[2].Value;
            string strategy = progressMatch.Groups[3].Value;

            ProgressChanged?.Invoke(Tuple.Create(current, all, strategy));
        }
        if (output.Contains("This strategy has no successes"))
        {
            IncorrectCount++;
            IncorrectCountChanged?.Invoke(IncorrectCount);
        }

        if (output.Contains("worst result for this strategy:"))
        {
            _goodStrategyCountString = output;
        }

        if (!string.IsNullOrEmpty(_goodStrategyCountString))
        {
            _goodStrategyCountString += $"\n{output}";
            Match resultMatch = Regex.Match(_goodStrategyCountString, GetStrategyCountRegex);
            if (resultMatch.Success)
            {
                string current = resultMatch.Groups[1].Value;
                string all = resultMatch.Groups[2].Value;

                int currentInt = 0;
                int allInt = 1;

                int.TryParse(current, out currentInt);
                int.TryParse(all, out allInt);

                StrategyResultAdded?.Invoke(Tuple.Create(currentInt, allInt));

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
                _goodStrategyCountString = null;
            }
        }

        OperationOutputAdded?.Invoke(Tuple.Create(CurrentOperationId, $"{output}"));
        _operations[CurrentOperationId].AppendOutputLine(output);
    }

    private async Task<bool> MonitorProcessAndLogsAsync(string logsDirectory, CancellationToken cancellationToken)
    {
        const string logPattern = "logfile_GoodCheckGoGo_*.log";
        const string errorFlag = "Exiting with an error...";
        const string successFlag = "All Done";

        string currentLogFile = null;
        long currentPosition = 0;
        StreamReader reader = null;
        FileStream fs = null;

        GoodCheckOperationModel model = GetOperationById(CurrentOperationId);
        // Only the log of this run: an older log could end with "All Done".
        DateTime runStartUtc = DateTime.UtcNow.AddSeconds(-2);

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
                string newest = GetNewestLogFile(logsDirectory, logPattern, runStartUtc);

                if (string.IsNullOrEmpty(newest))
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (reader == null || !string.Equals(currentLogFile, newest, StringComparison.OrdinalIgnoreCase))
                {
                    reader?.Dispose();
                    fs?.Dispose();

                    // A log of this run is read from its start: reading from the end lost
                    // everything written before the file was found.
                    currentLogFile = newest;
                    fs = new FileStream(currentLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    reader = new StreamReader(fs);
                }

                string newText = await reader.ReadToEndAsync().ConfigureAwait(false);

                if (!string.IsNullOrEmpty(newText))
                {
                    currentPosition = fs.Position;
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
                string rest = reader != null ? await reader.ReadToEndAsync().ConfigureAwait(false) : string.Empty;
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
        finally
        {
            reader?.Dispose();
            fs?.Dispose();
        }

        return false;
    }

    private string? GetNewestLogFile(string directory, string pattern, DateTime notBeforeUtc)
    {
        try
        {
            if (!Directory.Exists(directory))
                return null;
            var files = Directory.GetFiles(directory, pattern)
                .Where(f => File.GetCreationTimeUtc(f) >= notBeforeUtc)
                .ToArray();
            if (files.Length == 0)
                return null;

            return files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
        }
        catch
        {
            return null;
        }
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
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        string filePath = Path.Combine(
            GetAddOnDirectory(),
            "Reports",
            $"GoodCheckReport_{secondsSinceEpoch}.xml");
        if (!Path.Exists(Path.GetDirectoryName(filePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
        var root = new XElement("Report");
        doc.Add(root);

        int opIndex = 0;
        foreach (var operation in _operations)
        {
            opIndex++;
            // Both are set by InitGoodCheck; XAttribute does not take null.
            string operationName = operation.SiteListName ?? string.Empty;

            var group = new XElement("Group",
                new XAttribute("Name", operationName),
                new XAttribute("FullPath", operation.SiteListPath ?? string.Empty),
                new XAttribute("ComponentId", ComponentId));

            var matches = Regex.Matches(operation.Output, AnalyzeRegex, RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                string strategy = match.Groups[3].Value;
                string success = match.Groups[4].Value;
                string all = match.Groups[5].Value;

                var matchElem = new XElement("StrategyResult",
                    new XElement("Flag", false),
                    new XElement("Strategy", strategy),
                    new XElement("Success", success),
                    new XElement("All", all)
                );

                group.Add(matchElem);
            }
            root.Add(group);
        }
        doc.Save(filePath);
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

