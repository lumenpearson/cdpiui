using System.Diagnostics;
using CDPIUI.Core.Basic;

namespace CDPIUI.Core.System;

public enum TextFileOpenModes
{
    FollowSystem,
    UserChoose
}

public static class ShellHelper
{
    // Full paths: CreateProcess and ShellExecute also look for a bare name in the current
    // folder, so a planted explorer.exe or notepad.exe there could run, even elevated.
    public static readonly string ExplorerPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    public static readonly string NotepadPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

    /// <summary>
    /// Load all text from file.
    /// </summary>
    /// <param name="filepath">Path to file</param>
    /// <returns>File content</returns>
    public static string LoadAllTextFromFile(string filepath)
    {
        return File.ReadAllText(filepath);
    }

    /// <summary>
    /// Open folder in system's shell
    /// </summary>
    /// <param name="dir">Path to directory</param>
    public static void LookupDirectory(string dir)
    {
        try
        {
            Process.Start(ExplorerPath, $"\"{dir.Replace("/", "\\")}\"");
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ShellHelper), $"Cannot open path \"{dir}\" Because exception happens: {ex}");
        }
    }

    /// <summary>
    /// Open file's directory in system's shell, than highlighted it.
    /// </summary>
    /// <param name="file">Path to file</param>
    public static void LookupFileInDirectory(string file)
    {
        try
        {
            Process.Start(ExplorerPath, "/select," + $"\"{file.Replace("/", "\\")}\"");
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ShellHelper), $"Cannot open path \"{file}\" Because exception happens: {ex}");
        }
    }

    /// <summary>
    /// Run WIN32 application.
    /// </summary>
    /// <param name="executable">Target executable</param>
    /// <param name="arguments">Startup arguments</param>
    /// <param name="askUAC">Do process must request administrator rights</param>
    public static void RunApp(string executable, string arguments, bool askUAC = false)
    {
        try
        {
            var psi = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = true
            };
            if (askUAC)
                psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ShellHelper),
                $"Cannot open application '{executable}' with arguments '{arguments}', because exception happens: {ex.Message}");
        }
    }


    /// <summary>
    /// Open file in edit application
    /// </summary>
    /// <param name="file">Path to file</param>
    /// <param name="askUAC">Do process must request administrator rights</param>
    /// <param name="useNotepadAsDefault">Use "notepad" app as default</param>
    public static void OpenFile(string file, bool askUAC = false, bool useNotepadAsDefault = false)
    {
        int openMode = SettingsManager.Instance.GetValue<int>("FILEOPENACTIONS", "mode");
        string appPath = SettingsManager.Instance.GetValue<string>("FILEOPENACTIONS", "applicationPath");
        if (openMode == (int)TextFileOpenModes.UserChoose && File.Exists(appPath))
        {
            RunApp(appPath, $"\"{file}\"", askUAC);
        }
        else
        {
            if (useNotepadAsDefault)
                RunApp(NotepadPath, $"\"{file}\"", askUAC);
            else
                OpenFileInDefaultApp(file, askUAC);
        }
    }

    /// <summary>
    /// Open file in default edit application
    /// </summary>
    /// <param name="file">Path to file</param>
    /// <param name="askUAC">Do process must request administrator rights</param>
    public static void OpenFileInDefaultApp(string filePath, bool askUAC = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(filePath),
                UseShellExecute = true,
            };
            if (askUAC)
                psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ShellHelper), $"Cannot open file with path \"{filePath}\" Because exception happens: {ex}");
        }
    }
}
