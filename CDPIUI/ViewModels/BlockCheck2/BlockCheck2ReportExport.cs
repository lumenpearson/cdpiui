using System;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels.BlockCheck2;

/// <summary>Coordinates report picking, saving and completion notices for the result page.</summary>
internal sealed class BlockCheck2ReportExport
{
    private readonly Func<BlockCheckReport?> _report;
    private readonly Func<string, string, string, string?> _pickSave;
    private readonly Func<string, BlockCheckReport, Task> _saveJson;
    private readonly Func<string, BlockCheckReport, Task> _saveText;
    private readonly Func<string, string> _text;
    private readonly Action<BlockCheck2NoticeSeverity, string, string> _show;
    private readonly Action<string> _error;

    public BlockCheck2ReportExport(Func<BlockCheckReport?> report, Func<string, string, string, string?> pickSave,
        Func<string, BlockCheckReport, Task> saveJson, Func<string, BlockCheckReport, Task> saveText,
        Func<string, string> text, Action<BlockCheck2NoticeSeverity, string, string> show, Action<string> error)
    {
        _report = report;
        _pickSave = pickSave;
        _saveJson = saveJson;
        _saveText = saveText;
        _text = text;
        _show = show;
        _error = error;
    }

    public async Task SaveJsonAsync()
    {
        if (_report() == null)
        {
            return;
        }
        string? fileName = _pickSave(
            _text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.json",
            _text("BlockCheck2JsonFileFilter"));
        if (fileName != null)
        {
            await SaveAsync(() => _saveJson(fileName, _report()!));
        }
    }

    public async Task SaveTextAsync()
    {
        if (_report() == null)
        {
            return;
        }
        string? fileName = _pickSave(
            _text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.txt",
            _text("BlockCheck2TextFileFilter"));
        if (fileName != null)
        {
            await SaveAsync(() => _saveText(fileName, _report()!));
        }
    }

    public async Task SaveAsync(Func<Task> saveAction)
    {
        try
        {
            await saveAction();
            _show(
                BlockCheck2NoticeSeverity.Success,
                _text("BlockCheck2ActionCompletedTitle"),
                _text("BlockCheck2FileSavedMessage"));
        }
        catch (Exception exception)
        {
            _error(exception.Message);
        }
    }
}
