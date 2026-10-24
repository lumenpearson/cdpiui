using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

public sealed record BlockCheck2PreparationPreview(
    int TargetCount,
    BlockCheckMaximumDurationEstimate? MaximumDuration,
    IReadOnlyList<BlockCheckIssue> Issues)
{
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}
