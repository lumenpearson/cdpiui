#nullable enable

using System;

namespace CDPIUI.ViewModels.BlockCheck2;

// Keeps report notice selection and localization in their original read order.
internal sealed class BlockCheck2ReportSummaryPresenter
{
    private readonly Func<bool> _hasReport, _noBypass, _success, _bestEffort, _httpOnly;
    private readonly Func<string, string> _text;
    private readonly Action<BlockCheck2NoticeSeverity, string, string> _show;

    public BlockCheck2ReportSummaryPresenter(Func<bool> hasReport, Func<bool> noBypass,
        Func<bool> success, Func<bool> bestEffort, Func<bool> httpOnly, Func<string, string> text,
        Action<BlockCheck2NoticeSeverity, string, string> show)
    {
        _hasReport = hasReport;
        _noBypass = noBypass;
        _success = success;
        _bestEffort = bestEffort;
        _httpOnly = httpOnly;
        _text = text;
        _show = show;
    }

    public void Show()
    {
        if (!_hasReport())
        {
            _show(BlockCheck2NoticeSeverity.Error, _text("BlockCheck2ResultEmptyTitle"), _text("BlockCheck2ResultEmptyMessage"));
            return;
        }

        var severity = _noBypass()
            ? BlockCheck2NoticeSeverity.Informational
            : _success()
                ? BlockCheck2NoticeSeverity.Success
                : BlockCheck2NoticeSeverity.Warning;
        var title = _noBypass()
            ? _text("BlockCheck2NoBypassRequiredTitle")
            : _success()
                ? _text("BlockCheck2ResultSuccessTitle")
                : _bestEffort()
                    ? _text("BlockCheck2ResultBestEffortTitle")
                    : _text("BlockCheck2ResultPartialTitle");
        var message = _noBypass()
            ? _text(_httpOnly()
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage")
            : _success()
                ? _text("BlockCheck2ResultSuccessMessage")
                : _bestEffort()
                    ? _text("BlockCheck2ResultBestEffortMessage")
                    : _text("BlockCheck2ResultPartialMessage");

        _show(severity, title, message);
    }

}
