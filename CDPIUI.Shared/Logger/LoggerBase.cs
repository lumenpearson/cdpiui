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

        // Failed writes since the last reported one. Changed only under _logLock.
        private static int _failedWriteCount;

        private void LogWrite(string fileName, string logMessage)
        {
            if (string.IsNullOrEmpty(ActualPath)) return;
            try
            {
                string logFileDir = Path.Combine(ActualPath, LoggerResources.LogsFolder);
                // A sender such as "StoreHelper/CheckUpdates" named a missing folder, and the log was lost.
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(invalid, '_');
                string logFilePath = Path.Combine(logFileDir, $"{fileName}{LoggerResources.LogsFileExtension}");

                if (!Directory.Exists(logFileDir)) Directory.CreateDirectory(logFileDir);

                var info = new FileInfo(logFilePath);
                if (info.Exists && info.Length > MaxLogFileSize)
                {
                    File.Copy(logFilePath, Path.ChangeExtension(logFilePath, ".old" + LoggerResources.LogsFileExtension), true);
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
            catch (Exception ex)
            {
                // Logging must never throw into the caller.
                ReportWriteFailure(ex);
                return;
            }
            ReportWriteRecovery();
        }

        private static void WriteMessageToTextWriter(string logMessage, TextWriter txtWriter)
        {
            txtWriter.WriteLine("[{0} {1}] {2}", DateTime.Now.ToLongTimeString(),
                DateTime.Now.ToShortDateString(), logMessage);
        }

        // Only the first failure of a series goes to Trace; the others are counted, so that a
        // missing or locked log folder does not flood the debug output.
        private static void ReportWriteFailure(Exception ex)
        {
            if (_failedWriteCount++ > 0) return;
            try
            {
                Trace.WriteLine($"[LoggerBase] Cannot write a log file: {ex}");
            }
            catch { }
        }

        private static void ReportWriteRecovery()
        {
            if (_failedWriteCount == 0) return;
            int failed = _failedWriteCount;
            _failedWriteCount = 0;
            try
            {
                Trace.WriteLine($"[LoggerBase] Log files are writable again after {failed} failed writes.");
            }
            catch { }
        }

        private void CreateLog(string message, LogSelevirity severity, string sender)
        {
            lock (_logLock)
            {
                string logMessage = $"[{sender}] [{severity}] {message}";
                Debug.WriteLine(logMessage);
                // A comparison with a null LogLevel is false, so nothing is written then.
                bool write = severity >= LogLevel;

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
