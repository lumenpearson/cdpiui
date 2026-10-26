using System.Text.RegularExpressions;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Reads the progress and the strategy results from the GoodCheck log, line by line. A strategy
/// result spans several lines, so the parser keeps the lines since its first one.
/// </summary>
internal sealed class GoodCheckOutputParser
{
    private const string GetProgressRegex = @"Launching '[a-zA-Z]{1,}', strategy (\d{1,})/(\d{1,}): \[(.*?)\]";
    private const string GetStrategyCountRegex = @"worst result for this strategy: (\d{1,})/(\d{1,})\n{1,}Terminating program\.\.\.";

    private string? _goodStrategyCountString;

    public GoodCheckOutputLine Parse(string output)
    {
        Tuple<string, string, string>? progress = null;
        Match progressMatch = Regex.Match(output, GetProgressRegex);
        if (progressMatch.Success)
        {
            string current = progressMatch.Groups[1].Value;
            string all = progressMatch.Groups[2].Value;
            string strategy = progressMatch.Groups[3].Value;

            progress = Tuple.Create(current, all, strategy);
        }

        bool hasNoSuccesses = output.Contains("This strategy has no successes");

        if (output.Contains("worst result for this strategy:"))
        {
            _goodStrategyCountString = output;
        }

        Tuple<int, int>? strategyResult = null;
        if (!string.IsNullOrEmpty(_goodStrategyCountString))
        {
            _goodStrategyCountString += $"\n{output}";
            Match resultMatch = Regex.Match(_goodStrategyCountString, GetStrategyCountRegex);
            if (resultMatch.Success)
            {
                string current = resultMatch.Groups[1].Value;
                string all = resultMatch.Groups[2].Value;

                int currentInt = 0;
                int allInt = 1;

                int.TryParse(current, out currentInt);
                int.TryParse(all, out allInt);

                strategyResult = Tuple.Create(currentInt, allInt);
                _goodStrategyCountString = null;
            }
        }

        return new GoodCheckOutputLine(progress, hasNoSuccesses, strategyResult);
    }
}
