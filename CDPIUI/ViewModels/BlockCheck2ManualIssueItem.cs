using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.Helper.AddOns.BlockCheck2;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ManualIssueItem
{
    public BlockCheck2ManualIssueItem(
        BlockCheckIssueSeverity severity,
        string code,
        string message,
        int count,
        IEnumerable<string> subjects)
    {
        Severity = severity;
        Code = code;
        Message = BlockCheck2IssueText.GetMessage(code, message);
        Count = Math.Max(1, count);
        string[] materializedSubjects = subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(subject => subject, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SubjectText = string.Join(", ", materializedSubjects);
        Key = $"{Severity}|{Code}|{message}|{string.Join('|', materializedSubjects)}";
    }

    public BlockCheckIssueSeverity Severity { get; }
    public string Code { get; }
    public string DisplayCode => Count > 1 ? $"{Code} ×{Count}" : Code;
    public string Message { get; }
    public int Count { get; }
    public string SubjectText { get; }
    public bool HasSubjects => SubjectText.Length > 0;
    public string Key { get; }
    public bool CanQuickFix => string.Equals(
        Code,
        "MANUAL_SCOPE_OVERLAP",
        StringComparison.OrdinalIgnoreCase);
}
