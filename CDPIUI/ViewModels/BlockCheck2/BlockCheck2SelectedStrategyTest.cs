using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>Runs a selected strategy test and presents its result through the result page.</summary>
internal sealed class BlockCheck2SelectedStrategyTest
{
    private readonly Func<BlockCheck2StrategyEvidenceItem?> _selected;
    private readonly Func<bool> _running;
    private readonly Action<bool> _setRunning;
    private readonly Action<bool> _progress;
    private readonly Action _selection;
    private readonly Func<BlockCheck2StrategyEvidenceItem, Task<BlockCheckManualStrategyTestResult>> _test;
    private readonly Action _assignments;
    private readonly Func<string, string> _text;
    private readonly Func<BlockCheckIssue, string> _issueText;
    private readonly Action<string> _error;
    private readonly Action<BlockCheck2NoticeSeverity, string, string> _show;

    public BlockCheck2SelectedStrategyTest(Func<BlockCheck2StrategyEvidenceItem?> selected, Func<bool> running,
        Action<bool> setRunning, Action<bool> progress, Action selection,
        Func<BlockCheck2StrategyEvidenceItem, Task<BlockCheckManualStrategyTestResult>> test, Action assignments,
        Func<string, string> text, Func<BlockCheckIssue, string> issueText, Action<string> error,
        Action<BlockCheck2NoticeSeverity, string, string> show)
    {
        _selected = selected;
        _running = running;
        _setRunning = setRunning;
        _progress = progress;
        _selection = selection;
        _test = test;
        _assignments = assignments;
        _text = text;
        _issueText = issueText;
        _error = error;
        _show = show;
    }

    public async Task TestAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = _selected();
        if (item == null || _running())
        {
            return;
        }

        _setRunning(true);
        _progress(true);
        _selection();
        try
        {
            BlockCheckManualStrategyTestResult result = await _test(item);
            _selection();
            ShowResult(result);
        }
        catch (Exception exception)
        {
            _error(exception.Message);
        }
        finally
        {
            _setRunning(false);
            _progress(false);
            _selection();
            _assignments();
        }
    }

    public void ShowResult(BlockCheckManualStrategyTestResult result)
    {
        if (result.ProbeResult == null)
        {
            BlockCheckIssue firstIssue = result.Issues.FirstOrDefault();
            _error(firstIssue != null
                ? _issueText(firstIssue)
                : _text("BlockCheck2StrategyTestFailedMessage"));
            return;
        }

        ProbeSummary summary = ProbeSummary.FromAttempts(result.ProbeResult.Attempts);
        string message = string.Format(
            _text("BlockCheck2StrategyTestResultFormat"),
            summary.SuccessCount,
            summary.AttemptCount,
            summary.SuccessRate);
        if (result.Issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            _error(_issueText(result.Issues.First(issue => issue.Severity == BlockCheckIssueSeverity.Error)));
        }
        else
        {
            _show(
                summary.SuccessCount == summary.AttemptCount
                    ? BlockCheck2NoticeSeverity.Success
                    : BlockCheck2NoticeSeverity.Warning,
                _text("BlockCheck2StrategyTestCompletedTitle"),
                message);
        }
    }
}
