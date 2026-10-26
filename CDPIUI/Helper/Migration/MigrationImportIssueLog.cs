using System;
using System.Collections.Generic;
using CDPIUI.Core.Basic;

namespace CDPIUI.Helper.Migration;

internal static class MigrationImportIssueLog
{
    /// <summary>
    /// Records a part of the migration that could not be imported and logs it.
    /// </summary>
    internal static void Add(
        ICollection<MigrationImportIssue> issues,
        string kind,
        string? source,
        Exception exception)
    {
        string normalizedSource = string.IsNullOrWhiteSpace(source) ? "(unknown)" : source;
        issues.Add(new MigrationImportIssue(kind, normalizedSource, exception.Message));
        Logger.Instance.CreateWarningLog(
            nameof(MigrationImportService),
            $"Cannot import {kind} '{normalizedSource}': {exception}");
    }
}
