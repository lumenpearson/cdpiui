using CDPIUI.Shared.Basic.CommandLine;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Helper;

/// <summary>
/// Decides whether the elevated tray may start a program from the data directory of the user,
/// with the given arguments. The user, and every program of the user, can change files there,
/// so a changed component, Lua script or argument file would otherwise get administrator
/// rights without a UAC prompt.
/// </summary>
public static class ExecutableTrust
{
    private static readonly SemaphoreSlim StoreLock = new(1, 1);

    // The question blocks the start (and the caller's locks) until it is answered.
    private static readonly TimeSpan QuestionTimeout = TimeSpan.FromMinutes(2);

    // A Lua file the arguments give the component is read whole to hash it.
    private const long MaxCodeFileBytes = 16 << 20;

    // Under the installation directory, which only administrators can change in an MSI installation.
    private static string ApprovalFile => Path.Combine(Utils.GetDataDirectory(), ".trust", "approved.txt");

    /// <summary>
    /// Locks the program, the code files of its folder and the files its arguments refer to,
    /// then checks them. Returns null when the program must not start. Dispose the result after
    /// the process has exited.
    /// </summary>
    /// <param name="executable">The program</param>
    /// <param name="arguments">Its command line; options that write a file must stay in the
    /// application directories, and Lua code it passes must be approved like the program</param>
    /// <param name="baseDirectory">The folder for relative paths in the arguments (the working directory)</param>
    /// <param name="operation">For the log</param>
    public static async Task<ExecutableLease?> AcquireAsync(string executable, string? arguments, string? baseDirectory, string operation)
    {
        if (!TrustedPaths.IsAllowed(executable, operation))
            return null;

        string fullPath = Path.GetFullPath(executable);

        // In a portable copy the user can change the tray itself, so the checks would protect
        // nothing; only the arguments are checked, as before.
        if (!Utils.IsInstalledByMsi())
        {
            string? unsafeOption = FileWritingOptions.FindOutsideWrite(arguments, TrustedPaths.AllowedRoots, baseDirectory, followArgumentFiles: true);
            if (unsafeOption == null)
                return ExecutableLease.CreateEmpty();
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Rejected {operation} for \"{fullPath}\": the arguments write a file outside the application directories: {unsafeOption}");
            return null;
        }

        bool insideInstallation = TrustedPathValidator.IsInside(fullPath, [Utils.GetDataDirectory()]);
        ExecutableLease lease;
        try
        {
            // A program of the installation folder is not checked (only administrators change
            // it), but its arguments are: the lease then holds only the files they refer to.
            lease = insideInstallation ? ExecutableLease.CreateEmpty() : ExecutableLease.Open(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot lock the files of {fullPath}: {exception.Message}");
            return null;
        }

        try
        {
            string? rejection = CheckArguments(lease, arguments, baseDirectory, out List<CodeReference> code);
            if (rejection != null)
            {
                Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Rejected {operation} for \"{fullPath}\": {rejection}");
                lease.Dispose();
                return null;
            }

            await StoreLock.WaitAsync();
            try
            {
                Dictionary<string, string> approved = LoadApprovals();
                bool changed = false;

                if (!insideInstallation)
                {
                    string fingerprint = lease.ComputeFingerprint();
                    if (!(approved.TryGetValue(fullPath, out string? known) && known == fingerprint))
                    {
                        bool trusted = await IsSignedStoreItemAsync(fullPath) || AskUser(ProgramQuestion(fullPath, lease));
                        if (!trusted)
                            return Reject(lease, operation, fullPath, "the files are new or changed and were not approved");
                        approved[fullPath] = fingerprint;
                        changed = true;
                    }
                }

                foreach (CodeReference reference in code)
                {
                    string key = ExecutableApprovals.CodeKey(reference.Hash);
                    if (approved.ContainsKey(key))
                        continue;
                    if (!AskUser(CodeQuestion(fullPath, reference)))
                        return Reject(lease, operation, fullPath, "the Lua code of the arguments is new or changed and was not approved");
                    approved[key] = reference.Hash;
                    changed = true;
                }

                if (changed)
                    SaveApprovals(approved);
                return lease;
            }
            finally
            {
                StoreLock.Release();
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    // config.ini of GoodCheck names the folder and the program of each component it starts.
    private static readonly (string Section, string FolderKey, string ExecutableKey)[] GoodCheckComponents =
    {
        ("Zapret", "ZapretFolder", "ZapretExecutableName"),
        ("GoodbyeDPI", "GoodbyeDPIFolder", "GoodbyeDPIExecutableName"),
        ("ByeDPI", "ByeDPIFolder", "ByeDPIExecutableName"),
    };

    /// <summary>
    /// GoodCheck runs with administrator rights and starts the components itself: the program
    /// from the folders of its config.ini, with each line of the strategy list (-s) as the
    /// arguments. Both files are in folders the user can change, so the tray locks them, checks
    /// the component folders like a program it starts, and checks every strategy like the
    /// arguments of a component. Returns the leases to keep until GoodCheck exits, or null.
    /// </summary>
    public static async Task<List<ExecutableLease>?> AcquireGoodCheckChildrenAsync(ExecutableLease goodCheckLease, string goodCheckExecutable, string arguments)
    {
        const string operation = "GoodCheck component start";
        string goodCheckDirectory = Path.GetDirectoryName(Path.GetFullPath(goodCheckExecutable))!;
        var leases = new List<ExecutableLease>();
        try
        {
            string? config = ReadLocked(goodCheckLease, Path.Combine(goodCheckDirectory, "config.ini"));
            if (config == null)
                return RejectAll(leases, "cannot lock config.ini of GoodCheck");

            // Every value of these keys is checked, in any section: whichever one GoodCheck reads
            // (the first, the last, another section) has been checked.
            List<(string Key, string Value)> entries = ParseIniEntries(config);
            foreach (var (section, folderKey, executableKey) in GoodCheckComponents)
            {
                foreach (var (_, executableName) in entries.Where(entry => entry.Key.Equals(executableKey, StringComparison.OrdinalIgnoreCase)))
                {
                    if (Unquote(executableName).IndexOfAny(['\\', '/', ':']) >= 0)
                        return RejectAll(leases, $"the program name of {section} in config.ini is a path: {executableName}");
                }
                foreach (var (_, value) in entries.Where(entry => entry.Key.Equals(folderKey, StringComparison.OrdinalIgnoreCase)))
                {
                    string folder = Unquote(value);
                    if (folder.Length == 0)
                        continue;
                    if (!Path.IsPathRooted(folder))
                        return RejectAll(leases, $"the folder of {section} in config.ini is not a full path: {folder}");
                    if (!Directory.Exists(folder))
                        continue;
                    ExecutableLease? componentLease = await AcquireFolderAsync(folder, operation);
                    if (componentLease == null)
                        return RejectAll(leases, $"the folder of {section} in config.ini was rejected: {folder}");
                    leases.Add(componentLease);
                }
            }

            string? strategyFile = GetOptionValue(arguments, "-s");
            if (strategyFile != null)
            {
                string fullStrategyFile = Path.GetFullPath(Path.Combine(goodCheckDirectory, strategyFile));
                if (!TrustedPaths.IsAllowed(fullStrategyFile, operation))
                    return RejectAll(leases, $"the strategy list is outside the application directories: {fullStrategyFile}");
                string? strategies = ReadLocked(goodCheckLease, fullStrategyFile);
                if (strategies == null)
                    return RejectAll(leases, $"cannot lock the strategy list {fullStrategyFile}");

                var code = new List<CodeReference>();
                foreach (string line in strategies.Split('\n').Select(text => text.Trim()))
                {
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                        continue;
                    // No folder for relative paths: a strategy may not write or load a file by a relative path.
                    string? rejection = CheckArguments(goodCheckLease, line, baseDirectory: null, out List<CodeReference> lineCode);
                    if (rejection != null)
                        return RejectAll(leases, $"a strategy is not allowed: {rejection}");
                    code.AddRange(lineCode);
                }
                if (!await ApproveCodeAsync(goodCheckExecutable, code))
                    return RejectAll(leases, "the Lua code of a strategy was not approved");
            }
            return leases;
        }
        catch
        {
            foreach (ExecutableLease lease in leases)
                lease.Dispose();
            throw;
        }
    }

    private static List<ExecutableLease>? RejectAll(List<ExecutableLease> leases, string reason)
    {
        Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Rejected GoodCheck start: {reason}");
        foreach (ExecutableLease lease in leases)
            lease.Dispose();
        return null;
    }

    /// <summary>
    /// Locks the code files of a component folder and checks them like a program: the same
    /// fingerprint, catalog check and question, with the folder as the approved path.
    /// </summary>
    private static async Task<ExecutableLease?> AcquireFolderAsync(string folder, string operation)
    {
        string fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
        if (!TrustedPaths.IsAllowed(fullFolder + Path.DirectorySeparatorChar, operation))
            return null;
        if (TrustedPathValidator.IsInside(fullFolder + Path.DirectorySeparatorChar, [Utils.GetDataDirectory()]))
            return ExecutableLease.CreateEmpty();

        ExecutableLease lease;
        try
        {
            lease = ExecutableLease.OpenFolder(fullFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot lock the files of {fullFolder}: {exception.Message}");
            return null;
        }

        try
        {
            string fingerprint = lease.ComputeFingerprint();
            await StoreLock.WaitAsync();
            try
            {
                Dictionary<string, string> approved = LoadApprovals();
                if (approved.TryGetValue(fullFolder, out string? known) && known == fingerprint)
                    return lease;
                string? program = lease.Paths.FirstOrDefault(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                bool trusted = (program != null && await IsSignedStoreItemAsync(program)) ||
                    AskUser(ProgramQuestion(program ?? fullFolder, lease));
                if (!trusted)
                    return Reject(lease, operation, fullFolder, "the files are new or changed and were not approved");
                approved[fullFolder] = fingerprint;
                SaveApprovals(approved);
                return lease;
            }
            finally
            {
                StoreLock.Release();
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static async Task<bool> ApproveCodeAsync(string fullPath, List<CodeReference> code)
    {
        if (code.Count == 0)
            return true;
        await StoreLock.WaitAsync();
        try
        {
            Dictionary<string, string> approved = LoadApprovals();
            bool changed = false;
            foreach (CodeReference reference in code.GroupBy(item => item.Hash).Select(group => group.First()))
            {
                string key = ExecutableApprovals.CodeKey(reference.Hash);
                if (approved.ContainsKey(key))
                    continue;
                if (!AskUser(CodeQuestion(fullPath, reference)))
                    return false;
                approved[key] = reference.Hash;
                changed = true;
            }
            if (changed)
                SaveApprovals(approved);
            return true;
        }
        finally
        {
            StoreLock.Release();
        }
    }

    private static string? ReadLocked(ExecutableLease lease, string path)
    {
        try
        {
            return File.Exists(path) ? lease.ReadArgumentFile(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot lock {path}: {exception.Message}");
            return null;
        }
    }

    // The value after an option of GoodCheck such as -s "path".
    private static string? GetOptionValue(string arguments, string option)
    {
        IReadOnlyList<string> tokens = CommandLineTokens.Tokenize(arguments);
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (string.Equals(tokens[index], option, StringComparison.OrdinalIgnoreCase))
                return CommandLineTokens.Unquote(tokens[index + 1]);
        }
        return null;
    }

    // Every key=value line of an INI file, comments and section headers left out.
    private static List<(string Key, string Value)> ParseIniEntries(string text)
    {
        var entries = new List<(string, string)>();
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                continue;
            int equals = line.IndexOf('=');
            if (equals > 0)
                entries.Add((line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim()));
        }
        return entries;
    }

    private static string Unquote(string value) => value.Trim().Trim('"', '\'').Trim();

    private static ExecutableLease? Reject(ExecutableLease lease, string operation, string fullPath, string reason)
    {
        Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Rejected {operation} for \"{fullPath}\": {reason}");
        lease.Dispose();
        return null;
    }

    private sealed record CodeReference(string? File, string Text, string Hash);

    /// <summary>
    /// Checks the arguments with the files they refer to locked by the lease: options that write
    /// a file stay in the application directories and their folders cannot be swapped for a
    /// link, and the Lua code given by --lua-init (inline, or a file outside the locked
    /// component folder) is collected for approval. Returns the reason to reject, or null.
    /// </summary>
    private static string? CheckArguments(ExecutableLease lease, string? arguments, string? baseDirectory, out List<CodeReference> code)
    {
        code = new List<CodeReference>();
        var writeTargets = new List<string>();
        var luaValues = new List<(string Value, string? BaseDirectory)>();

        string? unsafeOption = FileWritingOptions.FindOutsideWrite(
            arguments, TrustedPaths.AllowedRoots, baseDirectory, followArgumentFiles: true,
            readArgumentFile: lease.ReadArgumentFile, writeTargets: writeTargets, luaInitValues: luaValues);
        if (unsafeOption != null)
            return $"the arguments write a file outside the application directories: {unsafeOption}";

        try
        {
            foreach (string target in writeTargets.Distinct(StringComparer.OrdinalIgnoreCase))
                lease.GuardWriteTarget(target, TrustedPaths.AllowedRoots);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"a file the arguments write is not safe: {exception.Message}";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (value, valueBase) in luaValues)
        {
            CodeReference reference;
            if (value.StartsWith("@", StringComparison.Ordinal))
            {
                string? file = Resolve(value.Substring(1).Trim(), valueBase);
                if (file == null)
                    return $"cannot resolve the Lua file {value}";
                if (lease.Holds(file))
                    continue; // A file of the component folder: part of the program fingerprint.
                try
                {
                    byte[] content = lease.LockAndRead(file, MaxCodeFileBytes);
                    reference = new CodeReference(file, file, ExecutableLease.HashText("file\n" + file.ToLowerInvariant() + "\n" + ExecutableLease.HashBytes(content)));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    return $"cannot lock the Lua file {file}: {exception.Message}";
                }
            }
            else
            {
                reference = new CodeReference(null, value, ExecutableLease.HashText("inline\n" + value));
            }
            if (seen.Add(reference.Hash))
                code.Add(reference);
        }
        return null;
    }

    private static string? Resolve(string value, string? baseDirectory)
    {
        try
        {
            if (Path.IsPathRooted(value))
                return Path.GetFullPath(value);
            return baseDirectory == null ? null : Path.GetFullPath(Path.Combine(baseDirectory, value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// A Store item whose signed catalog matches its code files needs no question. Lists and
    /// other data files are skipped, because the user can edit them.
    /// </summary>
    private static async Task<bool> IsSignedStoreItemAsync(string fullPath)
    {
        string? itemDirectory = ExecutableApprovals.GetStoreItemDirectory(fullPath);
        if (itemDirectory == null)
            return false;

        string catalog = Path.Combine(itemDirectory, "catalog.cat");
        if (!File.Exists(catalog))
            return false;

        var dataFiles = Directory.EnumerateFiles(itemDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !ExecutableLease.CodeExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        CatalogVerification result = await CatalogVerifier.VerifyAsync(catalog, itemDirectory, Secret.TrustedCertificates, dataFiles);
        if (result != CatalogVerification.Success)
            Logger.Instance.CreateInfoLog(nameof(ExecutableTrust), $"Catalog check of {itemDirectory}: {result}");
        return result == CatalogVerification.Success;
    }

    private static string ProgramQuestion(string fullPath, ExecutableLease lease) => string.Format(
        LocaleHelper.GetLocaleString("ExecutableTrustQuestion"),
        fullPath,
        lease.GetHash(fullPath),
        // Only a signature that WinVerifyTrust accepts names a signer: the certificate block of
        // a changed file still carries the old name.
        PackageStaging.GetVerifiedSigner(fullPath) ?? LocaleHelper.GetLocaleString("ExecutableTrustNoSignature"));

    private static string CodeQuestion(string fullPath, CodeReference reference)
    {
        const int MaxShown = 600;
        string shown = reference.File ?? LocaleHelper.GetLocaleString("ExecutableTrustInlineCode") + "\n" + reference.Text;
        if (shown.Length > MaxShown)
            shown = shown.Substring(0, MaxShown) + "…";
        return string.Format(LocaleHelper.GetLocaleString("ExecutableTrustCodeQuestion"), fullPath, shown, reference.Hash);
    }

    /// <summary>
    /// Shows the question in a window of the elevated tray. Programs of the user cannot press
    /// its buttons, because Windows blocks input from a lower integrity level. An unanswered
    /// question is closed after <see cref="QuestionTimeout"/> and counts as No, so a start does
    /// not hold the component and pipe locks forever.
    /// </summary>
    private static bool AskUser(string text)
    {
        string title = LocaleHelper.GetLocaleString("ExecutableTrustTitle");
        DialogResult answer = DialogResult.No;
        uint threadId = 0;
        var thread = new Thread(() =>
        {
            threadId = NativeMethods.GetCurrentThreadId();
            // A top-most owner on this thread keeps the box in front of other windows. The box is
            // not shown with DefaultDesktopOnly: Windows then creates it in a system process, so
            // the tray could not close it on the timeout and it stayed on screen after the start
            // had already been rejected.
            using var owner = new Form
            {
                TopMost = true,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(1, 1),
                Opacity = 0,
            };
            owner.Show();
            owner.Activate();
            answer = MessageBox.Show(
                owner,
                text,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (thread.Join(QuestionTimeout))
            return answer == DialogResult.Yes;

        Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), "The start question was not answered in time; the start is rejected.");
        NativeMethods.CloseThreadWindows(threadId);
        thread.Join(TimeSpan.FromSeconds(5));
        return false;
    }

    private static Dictionary<string, string> LoadApprovals()
    {
        try
        {
            return ExecutableApprovals.Load(ApprovalFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot read the approved programs: {exception.Message}");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveApprovals(Dictionary<string, string> approved)
    {
        try
        {
            ExecutableApprovals.Save(ApprovalFile, approved);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot save the approved programs: {exception.Message}");
        }
    }

    private static class NativeMethods
    {
        // A Yes/No message box ignores WM_CLOSE (it has no Cancel button), so No is pressed.
        private const uint WmCommand = 0x0111;
        private const int IdNo = 7;

        private delegate bool EnumThreadWindowsProc(IntPtr window, IntPtr parameter);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumThreadWindowsProc callback, IntPtr parameter);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        public static void CloseThreadWindows(uint threadId)
        {
            if (threadId == 0)
                return;
            EnumThreadWindows(threadId, (window, _) =>
            {
                PostMessage(window, WmCommand, (IntPtr)IdNo, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }
    }
}
