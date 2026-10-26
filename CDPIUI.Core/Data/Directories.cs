using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions;

namespace CDPIUI.Core.Data;

public static class Directories
{
    /// <summary>
    /// Application data directory (%LOCALAPPDATA% if current folder protected, otherwise CURRENT)
    /// </summary>
    public static string DataDirectory { get => DirectoriesHelper.Instance.DataDirectory; }

    /// <summary>
    /// Application current directory
    /// </summary>
    public static string CurrentDirectory { get => DirectoriesHelper.Instance.CurrentDirectory; }

    public static string SettingsDirectory { get => DataCombine("Settings"); }
    public static string SettingsFilePath { get => Path.Combine(SettingsDirectory, "Settings.xml"); }
    public static string TemplatesDirectory { get => CurrentDirCombine("Template"); }
    public static string TemplateSettingsDirectory { get => Path.Combine(TemplatesDirectory, "Settings"); }
    public static string TemplateSettingsFilePath { get => Path.Combine(TemplateSettingsDirectory, "Settings.xml"); }

    #region Store
    public static string StoreDirectory => DataCombine("Store");
    public static string StoreItemsDirectory => Path.Combine(StoreDirectory, "Items");
    public static string StoreCacheDirectory => Path.Combine(StoreDirectory, "Cache");
    public static string StoreLocalCacheDirectory => Path.Combine(StoreCacheDirectory, "Local");
    public static string StoreRepoCacheDirectory => Path.Combine(StoreCacheDirectory, "Repo");

    public static string StoreLocalUserItemDirectory => Path.Combine(StoreItemsDirectory, SharedConstants.LocalUserItemsId);
    #endregion

    #region ELUA

    public static string ELUADirectory => Path.Combine(CurrentDirectory, "ELUA");

    #endregion

    public static string TempFilesDirectory { get => DataCombine("TempFiles"); }

    #region Download Manager

    public static string DownloadManagerDirectory { get => Path.Combine(TempFilesDirectory, "Downloads"); }

    #endregion

    private static string DataCombine(params string[] paths)
    {
        return Path.Combine(DataDirectory, Path.Combine(paths));
    }
    private static string CurrentDirCombine(params string[] paths)
    {
        return Path.Combine(CurrentDirectory, Path.Combine(paths));
    }
}

public class DirectoriesHelper
{
    private static DirectoriesHelper? _instance;
    private static readonly object SyncRoot = new();

    public static DirectoriesHelper Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new DirectoriesHelper();
                return _instance;
            }
        }
    }

    private DirectoriesHelper() { }

    public string DataDirectory { get => TryGetDataDirectory(); }

    private string? _dataDirectoryProperty;
    private string TryGetDataDirectory()
    {
        if (_dataDirectoryProperty != null)
            return _dataDirectoryProperty;
        var result = DirectoriesManager.GetDataDirectory(Environment.ProcessPath, false, State.IsApplicationBuildAsMsi);

        if (result.Success)
        {
            _dataDirectoryProperty = result.Result;
            return result.Result!;
        }
        else
        {
            // An empty path made every file go to the current working directory.
            _dataDirectoryProperty = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CDPIUI");
            CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(result.Error ?? new UnknownException());
            return _dataDirectoryProperty;
        }
    }

    public string CurrentDirectory { get => TryGetCurrentDirectory(); }

    private string? _currentDirectoryProperty;
    private string TryGetCurrentDirectory()
    {
        if (_currentDirectoryProperty != null)
            return _currentDirectoryProperty;
        var result = DirectoriesManager.GetDataDirectory(Environment.ProcessPath, true);

        if (result.Success)
        {
            _currentDirectoryProperty = result.Result;
            return result.Result!;
        }
        else
        {
            _currentDirectoryProperty = string.Empty;
            CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(result.Error ?? new UnknownException());
            return string.Empty;
        }
    }
}
