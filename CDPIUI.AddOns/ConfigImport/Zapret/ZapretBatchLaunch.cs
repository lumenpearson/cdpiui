namespace CDPIUI.AddOns.ConfigImport.Zapret;

internal sealed record ZapretBatchLaunch(
    ZapretConfigGeneration Generation,
    string Arguments,
    int LineNumber);
