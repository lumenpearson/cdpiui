using CDPIUI.AddOns.ConfigImport;

namespace CDPIUI.Helper.AddOns.ConfigImport;

/// <param name="Result">The converted configuration.</param>
/// <param name="TargetWasDetected">True when the file itself showed which component it is for.</param>
internal sealed record AnalyzedConfigImport(ConfigImportResult Result, bool TargetWasDetected);
