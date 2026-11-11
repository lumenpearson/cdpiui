#nullable enable

using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels;

internal sealed class BlockCheck2ManualStrategyTestOperations : IBlockCheck2ManualStrategyTestOperations
{
    private readonly BlockCheckManualStrategyTestService _service;

    public BlockCheck2ManualStrategyTestOperations(BlockCheckManualStrategyTestService service) => _service = service;

    public OperationResultModel<string> GetFullStrategyArguments(StrategyDefinition strategy,
        BlockCheckTarget target, BlockCheckManualStrategyTestOptions options) =>
        _service.GetFullStrategyArguments(strategy, target, options);

    public Task<BlockCheckManualStrategyTestResult> TestAsync(StrategyDefinition strategy,
        BlockCheckTarget target, BlockCheckManualStrategyTestOptions options,
        CdpiuiZapret2StrategyRunnerOptions strategyOptions, CurlBlockCheckProbeRunnerOptions probeOptions,
        CancellationToken cancellationToken) =>
        _service.TestWithCdpiuiAdaptersAsync(strategy, target, options, strategyOptions, probeOptions, cancellationToken);
}
