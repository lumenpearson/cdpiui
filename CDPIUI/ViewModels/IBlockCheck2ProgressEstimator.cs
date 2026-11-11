#nullable enable

using System;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels;

internal interface IBlockCheck2ProgressEstimator
{
    void StartRun();
    void StopRun();
    void ResetObservations();
    (TimeSpan? Stage, TimeSpan? Overall) Update(BlockCheckScanProgress progress,
        int targetCount, int validationAttempts, double startupGraceSeconds);
}
