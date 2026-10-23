using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace EditHostFile
{
    internal class Program
    {
        private const int E_INVALIDARG = unchecked((int)0x80070057);

        private static void Log(string message)
        {
            Console.WriteLine($"[CDPI UI/EditHostFileHelper] {message}");
        }

        static int Main(string[] args)
        {
            RegisterCodePages();
            try
            {
                Console.Title = Strings.WindowTitle;
            }
            catch (IOException)
            {
                // There is no console window, for example when the output is redirected.
            }
            Log(Strings.GettingReady);
            string pathpart = "hosts";
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                //is windows NT
                pathpart = "system32\\drivers\\etc\\hosts";
            }
            string hostfile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), pathpart);

            try
            {
                if (args.Contains("/remove"))
                {
                    RemoveAddedLines(hostfile);
                }
                else if (args.Contains("/add"))
                {
                    AddNewLines(hostfile);
                }
                else if (args.Contains("/recover"))
                {
                    RestoreBackup(hostfile);
                }
                else
                {
                    Log(Strings.UnknownCommand);
                    return E_INVALIDARG;
                }
            }
            catch (Exception ex)
            {
                Log(string.Format(Strings.WorkFailed, ex));
                return ex.HResult != 0 ? ex.HResult : 1;
            }
            Log(Strings.WorkFinished);
            return 0;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetACP();

        /// <summary>
        /// .NET 8 has only the Unicode encodings, ASCII and Latin-1. The ANSI code pages
        /// (for example 1251 and 1252) come from CodePagesEncodingProvider.
        /// </summary>
        internal static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// The ANSI code page of the system. On .NET Framework, Encoding.Default gave this encoding.
        /// On .NET 8, Encoding.Default is always UTF-8.
        /// </summary>
        internal static Encoding GetAnsiEncoding()
        {
            RegisterCodePages();
            int codePage = OperatingSystem.IsWindows()
                ? (int)GetACP()
                : CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            return Encoding.GetEncoding(codePage);
        }

        internal sealed class HostsText
        {
            public string Text;
            public Encoding Encoding;
            public string NewLine;
        }

        internal static HostsText Read(string path)
        {
            // Some systems have no hosts file at all; it is then treated as empty, and /add creates it.
            if (!File.Exists(path))
                return new HostsText { Text = string.Empty, Encoding = new UTF8Encoding(false), NewLine = "\r\n" };

            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding;
            int offset = 0;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                offset = 3;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = new UnicodeEncoding(false, true);
                offset = 2;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = new UnicodeEncoding(true, true);
                offset = 2;
            }
            else
            {
                // No BOM: keep UTF-8 if the content is valid UTF-8, otherwise it is in the ANSI code page.
                encoding = new UTF8Encoding(false, true);
                try
                {
                    encoding.GetString(bytes);
                    encoding = new UTF8Encoding(false);
                }
                catch (DecoderFallbackException)
                {
                    encoding = GetAnsiEncoding();
                }
            }

            string text = encoding.GetString(bytes, offset, bytes.Length - offset);
            string newLine = text.Contains("\r\n") ? "\r\n" : text.Contains("\n") ? "\n" : "\r\n";
            return new HostsText { Text = text, Encoding = encoding, NewLine = newLine };
        }

        internal static void Write(string path, HostsText content)
        {
            byte[] preamble = content.Encoding.GetPreamble();
            byte[] body = content.Encoding.GetBytes(content.Text);

            // Write next to the target and swap it in, so the hosts file is never left half written.
            string temp = $"{path}.cdpiui.tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(preamble, 0, preamble.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush(true);
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void CreateBackup(string path, HostsText content)
        {
            Log(string.Format(Strings.CreatingBackup, path, $"{path}.bak"));
            Write($"{path}.bak", content);
        }

        internal static void RestoreBackup(string path)
        {
            // The backup is the file as it was before /add without the added block. Copying it back
            // would also drop every change made to the hosts file since then (VPN clients, Docker,
            // manual edits), so only the added block is removed and the backup is discarded.
            Log(string.Format(Strings.RestoringFile, path));
            RemoveAddedLines(path);

            string backup = $"{path}.bak";
            if (File.Exists(backup)) File.Delete(backup);
        }

        internal static string RemoveBlock(string text)
        {
            return Regex.Replace(text, @"(# \[CDOM-B\](?:.*?)# \[CDOM-E\](?:.*?))(?:\n|$)", "", RegexOptions.Singleline);
        }

        internal static void RemoveAddedLines(string path)
        {
            Log(string.Format(Strings.RemovingLines, path));
            HostsText content = Read(path);
            string text = RemoveBlock(content.Text);
            if (text == content.Text) return;

            content.Text = text;
            Write(path, content);
        }

        private static List<string> SplitLines(string text)
        {
            return text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        }

        internal static void AddNewLines(string path)
        {
            Log(string.Format(Strings.AddingLines, path));
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resource;
            using (var stream = assembly.GetManifestResourceStream("EditHostFile.Data.hosts"))
            using (var reader = new StreamReader(stream))
            {
                resource = reader.ReadToEnd();
            }

            HostsText content = Read(path);
            string text = RemoveBlock(content.Text);
            content.Text = text;
            CreateBackup(path, content);

            List<string> lines = SplitLines(text);
            var builder = new StringBuilder(text);
            if (text.Length > 0 && !text.EndsWith("\n"))
            {
                builder.Append(content.NewLine);
            }
            foreach (string line in SplitLines(resource))
            {
                // Blank lines and comments keep the block readable and are always added.
                if (line.Length == 0 || line.StartsWith("#") || !lines.Contains(line))
                {
                    builder.Append(line).Append(content.NewLine);
                }
            }

            content.Text = builder.ToString();
            Write(path, content);

            Log(string.Format(Strings.AddingLinesComplete, path));
        }
    }
}
