using System;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2ResultPageViewModel
{
    public Task SaveJsonReportAsync() => _reportExport.SaveJsonAsync();

    public Task SaveTextReportAsync() => _reportExport.SaveTextAsync();

    private Task SaveAsync(Func<Task> saveAction) => _reportExport.SaveAsync(saveAction);

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
