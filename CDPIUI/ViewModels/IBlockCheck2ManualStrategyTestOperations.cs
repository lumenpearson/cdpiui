#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2ManualStrategyTestOperations
{
    OperationResultModel<string> GetFullStrategyArguments(StrategyDefinition strategy,
        BlockCheckTarget target, BlockCheckManualStrategyTestOptions options);

    Task<BlockCheckManualStrategyTestResult> TestAsync(StrategyDefinition strategy,
        BlockCheckTarget target, BlockCheckManualStrategyTestOptions options,
        CdpiuiZapret2StrategyRunnerOptions strategyOptions, CurlBlockCheckProbeRunnerOptions probeOptions,
        CancellationToken cancellationToken);
}
