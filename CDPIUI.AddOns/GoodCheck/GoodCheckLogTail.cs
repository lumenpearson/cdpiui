namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Follows the newest GoodCheck log of the current run and returns the text written since the
/// last read. A log of this run is read from its start: reading from the end lost everything
/// written before the file was found.
/// </summary>
internal sealed class GoodCheckLogTail(string logsDirectory, DateTime notBeforeUtc) : IDisposable
{
    private const string LogPattern = "logfile_GoodCheckGoGo_*.log";

    private string? _currentLogFile;
    private FileStream? _stream;
    private StreamReader? _reader;

    /// <summary>
    /// Opens the newest log, or keeps the open one when it is still the newest.
    /// </summary>
    /// <returns>False while the run has written no log yet.</returns>
    public bool TryOpenNewest()
    {
        string? newest = GetNewestLogFile();

        if (string.IsNullOrEmpty(newest))
            return false;

        if (_reader == null || !string.Equals(_currentLogFile, newest, StringComparison.OrdinalIgnoreCase))
        {
            _reader?.Dispose();
            _stream?.Dispose();

            _currentLogFile = newest;
            _stream = new FileStream(_currentLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _reader = new StreamReader(_stream);
        }

        return true;
    }

    /// <summary>
    /// The text written since the last read; empty when no log is open.
    /// </summary>
    public async Task<string> ReadNewTextAsync() =>
        _reader != null ? await _reader.ReadToEndAsync().ConfigureAwait(false) : string.Empty;

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
    }

    // Only the log of this run: an older log could end with "All Done".
    private string? GetNewestLogFile()
    {
        try
        {
            if (!Directory.Exists(logsDirectory))
                return null;
            var files = Directory.GetFiles(logsDirectory, LogPattern)
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
}
