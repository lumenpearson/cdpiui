#nullable enable

using System.Collections.Generic;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2ManualPresetHost
{
    BlockCheckResultSession? Session { get; }
    void ClearIssues();
    void AddIssueGroup(BlockCheckIssueSeverity severity, string code, string message, int count, IEnumerable<string> subjects);
    void RefreshAssignmentIndexes();
    void RefreshEvidenceSelections();
    void NotifyManualState();
}
