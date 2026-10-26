namespace CDPIUI.AddOns.BlockCheck2.Presentation;

/// <summary>
/// Estimates the remaining strategy-scan time using an exponential moving
/// average. One unusually slow or fast strategy therefore does not make the
/// displayed value jump as sharply as a simple last-sample estimate would.
/// </summary>
public sealed class BlockCheckRemainingTimeEstimator
{
    private const double Alpha = 0.2d;
    private int _lastCompleted;
    private TimeSpan _lastElapsed;
    private double? _secondsPerJob;

    public void Reset()
    {
        _lastCompleted = 0;
        _lastElapsed = TimeSpan.Zero;
        _secondsPerJob = null;
    }

    public TimeSpan? Update(int completed, int total, TimeSpan elapsed)
    {
        if (completed < 0 || total <= 0 || elapsed < TimeSpan.Zero)
        {
            return null;
        }

        if (completed > _lastCompleted)
        {
            int completedDelta = completed - _lastCompleted;
            double elapsedDelta = Math.Max(0d, (elapsed - _lastElapsed).TotalSeconds);
            double sample = elapsedDelta / completedDelta;
            _secondsPerJob = _secondsPerJob.HasValue
                ? Alpha * sample + (1d - Alpha) * _secondsPerJob.Value
                : sample;
            _lastCompleted = completed;
            _lastElapsed = elapsed;
        }

        if (!_secondsPerJob.HasValue || completed >= total)
        {
            return completed >= total ? TimeSpan.Zero : null;
        }

        double seconds = Math.Max(0d, (total - completed) * _secondsPerJob.Value);
        return TimeSpan.FromSeconds(seconds);
    }
}
