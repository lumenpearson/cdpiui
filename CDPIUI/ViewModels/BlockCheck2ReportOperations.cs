#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.ViewModels;

// Keeps the existing report services and their export/error behavior at the boundary.
internal sealed class BlockCheck2ReportOperations : IBlockCheck2ReportOperations
{
    private readonly BlockCheckReportBuilder _builder;
    private readonly BlockCheckReportSerializer _serializer;

    public BlockCheck2ReportOperations(BlockCheckReportBuilder builder, BlockCheckReportSerializer serializer)
    {
        _builder = builder;
        _serializer = serializer;
    }

    public BlockCheckReport Build(StrategyCatalog catalog, BlockCheckRunPreset preset,
        IReadOnlyList<BlockCheckTarget> targets, BlockCheckRunResult result) =>
        _builder.Build(catalog, preset, targets, result);
    public string SerializeJson(BlockCheckReport report) => _serializer.SerializeJson(report);
    public string SerializeText(BlockCheckReport report) => _serializer.SerializeText(report);
    public Task SaveJsonAsync(string path, BlockCheckReport report, CancellationToken token) =>
        _serializer.SaveJsonAsync(path, report, token);
    public Task SaveTextAsync(string path, BlockCheckReport report, CancellationToken token) =>
        _serializer.SaveTextAsync(path, report, token);
    public Task SavePresetArgumentsAsync(string path, BlockCheckReport report, CancellationToken token) =>
        _serializer.SavePresetArgumentsAsync(path, report, token);
}
