using System;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.AddOns.PresetTest;

/// <summary>
/// The checks one test mode runs while a preset is active. Disposing the check removes the
/// temporary files it created for the run.
/// </summary>
internal interface IPresetCheck : IDisposable
{
    Task RunAsync(PresetTestResult result, PresetTestLog log, CancellationToken ct);
}
