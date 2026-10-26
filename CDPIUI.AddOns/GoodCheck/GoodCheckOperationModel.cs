namespace CDPIUI.AddOns.GoodCheck;

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
