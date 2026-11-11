#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2RunHost
{
    bool CanStart { get; }
    bool CanCancel { get; }
    BlockCheckReport? LastReport { get; }
    void RenewGeneratedSiteListRoot();
    void ResetResultState();
    void StartProgress();
    BlockCheckSessionPreparationResult Prepare();
    void ApplyPrepared(BlockCheckSessionPreparationResult prepared);
    void SetState(BlockCheckSessionState state);
    Task<BlockCheckRunResult> RunPreparedAsync(BlockCheckSessionPreparationResult prepared, CancellationToken cancellationToken);
    void ApplyRunResult(BlockCheckSessionPreparationResult prepared, BlockCheckRunResult result);
    void CompleteDisplayedProgress();
    Task SaveReportToHistoryAsync(BlockCheckReport report);
    void SetOperationError(string message);
    void FinishProgress();
    void CleanupGeneratedSiteListsIfUnviewable();
    void NotifyActionState();
}
