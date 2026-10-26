using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Secrets;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace CDPIUI.TrayIcon.Helper
{
    /// <summary>
    /// Decides whether the elevated tray may start a program from the data directory of the user.
    /// The user, and every program of the user, can change files there, so a changed component
    /// would otherwise get administrator rights without a UAC prompt.
    /// </summary>
    public static class ExecutableTrust
    {
        // Files that Windows loads as code from the folder of the program.
        private static readonly string[] CodeExtensions = [".exe", ".dll", ".sys"];

        private static readonly SemaphoreSlim StoreLock = new(1, 1);

        // Under the installation directory, which only administrators can change in an MSI installation.
        private static string ApprovalFile => Path.Combine(Utils.GetDataDirectory(), ".trust", "approved.json");

        /// <summary>
        /// Locks the program and the code files next to it, then checks them. Returns null when the
        /// program must not start. Dispose the result after the process has started.
        /// </summary>
        public static async Task<ExecutableLease?> AcquireAsync(string executable, string operation)
        {
            if (!TrustedPaths.IsAllowed(executable, operation))
                return null;

            string fullPath = Path.GetFullPath(executable);

            // In a portable copy the user can change the tray itself, so the check would protect nothing.
            if (!Utils.IsInstalledByMsi() || TrustedPathValidator.IsInside(fullPath, [Utils.GetDataDirectory()]))
                return ExecutableLease.None;

            ExecutableLease lease;
            try
            {
                lease = ExecutableLease.Open(fullPath, CodeExtensions);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot lock the files of {fullPath}: {exception.Message}");
                return null;
            }

            try
            {
                string fingerprint = lease.ComputeFingerprint();
                await StoreLock.WaitAsync();
                try
                {
                    Dictionary<string, string> approved = LoadApprovals();
                    if (approved.TryGetValue(fullPath, out string? known) && known == fingerprint)
                        return lease;

                    bool trusted = await IsSignedStoreItemAsync(fullPath) || AskUser(fullPath, lease);
                    if (!trusted)
                    {
                        Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Rejected {operation} for \"{fullPath}\": the files are new or changed and were not approved");
                        lease.Dispose();
                        return null;
                    }

                    approved[fullPath] = fingerprint;
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

        /// <summary>
        /// A Store item whose signed catalog matches its code files needs no question. Lists and
        /// other data files are skipped, because the user can edit them.
        /// </summary>
        private static async Task<bool> IsSignedStoreItemAsync(string fullPath)
        {
            string? itemDirectory = GetStoreItemDirectory(fullPath);
            if (itemDirectory == null)
                return false;

            string catalog = Path.Combine(itemDirectory, "catalog.cat");
            if (!File.Exists(catalog))
                return false;

            var dataFiles = Directory.EnumerateFiles(itemDirectory, "*", SearchOption.AllDirectories)
                .Where(file => !CodeExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase);

            CatalogVerification result = await CatalogVerifier.VerifyAsync(catalog, itemDirectory, Secret.TrustedCertificates, dataFiles);
            if (result != CatalogVerification.Success)
                Logger.Instance.CreateInfoLog(nameof(ExecutableTrust), $"Catalog check of {itemDirectory}: {result}");
            return result == CatalogVerification.Success;
        }

        // <data>\Store\Items\<item id>\...
        private static string? GetStoreItemDirectory(string fullPath)
        {
            string? current = Path.GetDirectoryName(fullPath);
            while (current != null)
            {
                string? parent = Path.GetDirectoryName(current);
                if (parent != null &&
                    string.Equals(Path.GetFileName(parent), "Items", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetFileName(Path.GetDirectoryName(parent)), "Store", StringComparison.OrdinalIgnoreCase))
                    return current;
                current = parent;
            }
            return null;
        }

        /// <summary>
        /// Shows the question in a window of the elevated tray. Programs of the user cannot press
        /// its buttons, because Windows blocks input from a lower integrity level.
        /// </summary>
        private static bool AskUser(string fullPath, ExecutableLease lease)
        {
            string text = string.Format(
                LocaleHelper.GetLocaleString("ExecutableTrustQuestion"),
                fullPath,
                lease.GetHash(fullPath),
                GetSigner(fullPath) ?? LocaleHelper.GetLocaleString("ExecutableTrustNoSignature"));

            DialogResult answer = DialogResult.No;
            var thread = new Thread(() =>
            {
                answer = MessageBox.Show(
                    text,
                    LocaleHelper.GetLocaleString("ExecutableTrustTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2,
                    MessageBoxOptions.DefaultDesktopOnly);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return answer == DialogResult.Yes;
        }

        private static string? GetSigner(string path)
        {
            try
            {
#pragma warning disable SYSLIB0057 // Only the subject is shown; the signature is not trusted on it.
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                return certificate.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        private static Dictionary<string, string> LoadApprovals()
        {
            try
            {
                if (File.Exists(ApprovalFile))
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ApprovalFile))
                        is Dictionary<string, string> stored
                        ? new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase)
                        : new(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot read the approved programs: {exception.Message}");
            }
            return new(StringComparer.OrdinalIgnoreCase);
        }

        private static void SaveApprovals(Dictionary<string, string> approved)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ApprovalFile)!);
                // Programs that no longer exist are dropped, so the file does not grow without limit.
                var existing = approved.Where(pair => File.Exists(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
                string temporary = ApprovalFile + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(existing));
                File.Move(temporary, ApprovalFile, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Logger.Instance.CreateWarningLog(nameof(ExecutableTrust), $"Cannot save the approved programs: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Read handles on a program and the code files in its folder. While they are open, nobody can
    /// change, rename or delete these files, and nobody can rename the folders above them.
    /// </summary>
    public sealed class ExecutableLease : IDisposable
    {
        public static readonly ExecutableLease None = new([]);

        private readonly List<(string Path, FileStream Stream)> _files;

        private ExecutableLease(List<(string Path, FileStream Stream)> files) => _files = files;

        public static ExecutableLease Open(string executable, IEnumerable<string> codeExtensions)
        {
            string directory = Path.GetDirectoryName(executable)!;
            var paths = Directory.EnumerateFiles(directory)
                .Where(file => codeExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Append(executable)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var files = new List<(string, FileStream)>();
            try
            {
                foreach (string path in paths)
                    files.Add((path, new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)));
            }
            catch
            {
                foreach (var (_, stream) in files)
                    stream.Dispose();
                throw;
            }
            return new ExecutableLease(files);
        }

        public string GetHash(string path)
        {
            foreach (var (filePath, stream) in _files)
            {
                if (string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase))
                    return Hash(stream);
            }
            return string.Empty;
        }

        /// <summary>
        /// One hash over the names and contents of all locked files.
        /// </summary>
        public string ComputeFingerprint()
        {
            var builder = new StringBuilder();
            foreach (var (path, stream) in _files)
                builder.Append(Path.GetFileName(path).ToLowerInvariant()).Append(':').Append(Hash(stream)).Append('\n');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static string Hash(FileStream stream)
        {
            stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        public void Dispose()
        {
            foreach (var (_, stream) in _files)
                stream.Dispose();
            _files.Clear();
        }
    }
}
