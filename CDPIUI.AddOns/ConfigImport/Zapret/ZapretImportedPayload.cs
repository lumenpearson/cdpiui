namespace CDPIUI.AddOns.ConfigImport.Zapret;

/// <summary>
/// The launch found in a batch or text config, before it becomes a Config.
/// </summary>
internal sealed record ZapretImportedPayload(
    ZapretConfigGeneration Generation,
    string StartupString,
    string Name,
    string? BuiltinVersion,
    Dictionary<string, ZapretBatchVariable> Variables,
    IReadOnlyList<ZapretConditionalVariable> ConditionalVariables);
