using System;
using System.Threading.Tasks;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels.BlockCheck2;

internal interface IBlockCheck2ManualTestHost
{
    BlockCheck2StrategyEvidenceItem? SelectedStrategy { get; }
    bool IsStrategyTestRunning { get; }
    bool? IsManualTestChecked { get; }
    OperationResultModel<string> GetFullStrategyArguments(BlockCheck2StrategyEvidenceItem item);
    Task<bool> ConfirmReplacementAsync();
    void UpdateSelectedStrategy();
    void UpdateAssignmentView();
    string Text(string key);
    void ShowMessage(BlockCheck2NoticeSeverity severity, string title, string message);
    void Warn(Exception exception);
    void Enqueue(Func<Task> callback);
}
