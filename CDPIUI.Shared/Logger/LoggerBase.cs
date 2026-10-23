using System;
using System.Diagnostics;
using System.IO;

namespace CDPIUI.Shared.Logger
{
    public enum LogSelevirity
    {
        DEBG,
        INFO,
        WARN,
        CRIT,
    }


    /// <summary>
    /// Initialize new logger
    /// </summary>
    /// <param name="actualPath">Path, where logs are storaged</param>
    /// <param name="logLevel">Logging level</param>
    public class LoggerBase(string? actualPath, LogSelevirity logLevel)
    {
        /// <summary>
        /// Path, where logs are storaged
        /// </summary>
        public readonly string? ActualPath = actualPath;

        /// <summary>
        /// Minimum severity written to log files
        /// </summary>
        public readonly LogSelevirity? LogLevel = logLevel;
        private static readonly object _logLock = new object();

        // A log grows without limit otherwise; the previous file is kept as *.old.log.
        private const long MaxLogFileSize = 5 * 1024 * 1024;

        private void LogWrite(string fileName, string logMessage)
        {
            if (string.IsNullOrEmpty(ActualPath)) return;
            try
            {
                string logFileDir = Path.Combine(ActualPath, LoggerResources.LogsFolder);
                string logFilePath = Path.Combine(logFileDir, $"{fileName}{LoggerResources.LogsFileExtention}");

                if (!Directory.Exists(logFileDir)) Directory.CreateDirectory(logFileDir);

                var info = new FileInfo(logFilePath);
                if (info.Exists && info.Length > MaxLogFileSize)
                {
                    File.Copy(logFilePath, Path.ChangeExtension(logFilePath, ".old" + LoggerResources.LogsFileExtention), true);
                    File.WriteAllText(logFilePath, string.Empty);
                    info.Refresh();
                }

                // Shared write access: the UI and the tray may log to the same file at the same time.
                using var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                using var w = new StreamWriter(stream);
                if (!info.Exists || info.Length == 0)
                    w.WriteLine(LoggerResources.LoggerReadyMessage + $" {LogLevel} ");
                WriteMessageToTextWriter(logMessage, w);
            }
            catch { }
        }

        private static void WriteMessageToTextWriter(string logMessage, TextWriter txtWriter)
        {
            try
            {
                txtWriter.WriteLine("[{0} {1}] {2}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToShortDateString(), logMessage);
            }
            catch { }
        }

        private void CreateLog(string message, LogSelevirity severity, string sender)
        {
            lock (_logLock)
            {
                string logMessage = $"[{sender}] [{severity}] {message}";
                Debug.WriteLine(logMessage);
                bool write = true;
                try
                {
                    if (severity >= LogLevel)
                    {
                        write = true;
                    }
                    else
                    {
                        write = false;
                    }
                }
                catch { }

                if (write)
                    LogWrite(sender, logMessage);
            }
        }

        /// <summary>
        /// Create new DEBUG log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateDebugLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.DEBG, sender);
        }

        /// <summary>
        /// Create new INFO log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateInfoLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.INFO, sender);
        }

        /// <summary>
        /// Create new WARNING log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateWarningLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.WARN, sender);
        }

        /// <summary>
        /// Create new ERROR log message (marked as CRIT)
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateErrorLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.CRIT, sender);
        }
    }
}
