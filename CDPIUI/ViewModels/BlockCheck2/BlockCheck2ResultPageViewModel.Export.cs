using System;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2ResultPageViewModel
{
    public async Task SaveJsonReportAsync()
    {
        if (Result.Report == null)
        {
            return;
        }
        string? fileName = _view.PickSaveFile(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.json",
            Text("BlockCheck2JsonFileFilter"));
        if (fileName != null)
        {
            await SaveAsync(() => _reportSerializer.SaveJsonAsync(fileName, Result.Report));
        }
    }

    public async Task SaveTextReportAsync()
    {
        if (Result.Report == null)
        {
            return;
        }
        string? fileName = _view.PickSaveFile(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.txt",
            Text("BlockCheck2TextFileFilter"));
        if (fileName != null)
        {
            await SaveAsync(() => _reportSerializer.SaveTextAsync(fileName, Result.Report));
        }
    }

    private async Task SaveAsync(Func<Task> saveAction)
    {
        try
        {
            await saveAction();
            ShowActionMessage(
                BlockCheck2NoticeSeverity.Success,
                Text("BlockCheck2ActionCompletedTitle"),
                Text("BlockCheck2FileSavedMessage"));
        }
        catch (Exception exception)
        {
            ShowActionError(exception.Message);
        }
    }

    public void CopySelectedStrategyArguments()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null)
        {
            return;
        }

        Copy(item.Evidence.StrategyArguments);
    }

    public void CopyFullSelectedStrategyArguments()
    {
        BlockCheck2StrategyEvidenceItem? item = _view.SelectedStrategy;
        if (item == null)
        {
            return;
        }
        var result = Result.GetFullStrategyArguments(item);
        if (result.Success)
            Copy(result.Result ?? string.Empty);
    }

    private void Copy(string text)
    {
        _view.CopyToClipboard(text);

        ShowActionMessage(BlockCheck2NoticeSeverity.Informational, Text("CopyComplete"), string.Empty);
    }
}
