namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// What one line of the GoodCheck log reported.
/// </summary>
/// <param name="Progress">The current strategy number, the strategy count and the strategy.</param>
/// <param name="HasNoSuccesses">The strategy reached none of the sites.</param>
/// <param name="StrategyResult">The worst result of a finished strategy: reached and checked sites.</param>
internal sealed record GoodCheckOutputLine(
    Tuple<string, string, string>? Progress,
    bool HasNoSuccesses,
    Tuple<int, int>? StrategyResult);
