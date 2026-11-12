#nullable enable

using System;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels.BlockCheck2;

// Selects the final notice with the original live reads and localization order.
internal sealed class BlockCheck2RunCompletionPresenter
{
    private readonly Func<BlockCheckSessionState> _state;
    private readonly Func<bool> _noBypass, _httpOnly;
    private readonly Func<string?> _operationError, _historyError;
    private readonly Func<string, string> _text;
    private readonly Func<string> _formatIssues;
    private readonly Action<BlockCheck2NoticeSeverity, string, string?> _publish;

    public BlockCheck2RunCompletionPresenter(Func<BlockCheckSessionState> state, Func<bool> noBypass,
        Func<bool> httpOnly, Func<string?> operationError, Func<string?> historyError,
        Func<string, string> text, Func<string> formatIssues,
        Action<BlockCheck2NoticeSeverity, string, string?> publish)
    {
        _state = state;
        _noBypass = noBypass;
        _httpOnly = httpOnly;
        _operationError = operationError;
        _historyError = historyError;
        _text = text;
        _formatIssues = formatIssues;
        _publish = publish;
    }

    public void Show()
    {
        BlockCheck2NoticeSeverity severity;
        string title;
        string? message;
        if (_state() == BlockCheckSessionState.Completed && _noBypass())
        {
            severity = BlockCheck2NoticeSeverity.Informational;
            title = _text("BlockCheck2NoBypassRequiredTitle");
            message = _text(_httpOnly()
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage");
        }
        else if (_state() == BlockCheckSessionState.Completed)
        {
            severity = BlockCheck2NoticeSeverity.Success;
            title = _text("BlockCheck2CompletedTitle");
            message = _text("BlockCheck2CompletedMessage");
        }
        else if (_state() == BlockCheckSessionState.CompletedWithWarnings)
        {
            severity = BlockCheck2NoticeSeverity.Warning;
            title = _text("BlockCheck2BestEffortCompletedTitle");
            message = _text("BlockCheck2BestEffortCompletedMessage");
        }
        else if (_state() == BlockCheckSessionState.Canceled)
        {
            severity = BlockCheck2NoticeSeverity.Informational;
            title = _text("BlockCheck2CanceledTitle");
            message = _text("BlockCheck2CanceledMessage");
        }
        else
        {
            severity = BlockCheck2NoticeSeverity.Error;
            title = _text("BlockCheck2FailedTitle");
            message = string.IsNullOrWhiteSpace(_operationError())
                ? _formatIssues()
                : _operationError();
        }

        if (!string.IsNullOrWhiteSpace(_historyError()))
        {
            message = string.Concat(
                message,
                Environment.NewLine,
                _text("BlockCheck2HistorySaveFailedMessage"),
                " ",
                _historyError());
        }

        _publish(severity, title, message);
    }
}
