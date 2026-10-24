using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Saves the strategy results of a GoodCheck run as an XML report, one group per site list.
/// </summary>
internal static class GoodCheckReportWriter
{
    private const string AnalyzeRegex = @"Launching '[a-zA-Z]{1,}', strategy (\d{1,})/(\d{1,}): \[(.*?)\](?:.*?)worst result for this strategy: (\d{1,})/(\d{1,})(\n||This strategy has no successes){1,}Terminating program\.\.\.";

    /// <returns>The path of the saved report.</returns>
    public static string Save(
        string addOnDirectory,
        IEnumerable<GoodCheckOperationModel> operations,
        string componentId)
    {
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        string filePath = Path.Combine(
            addOnDirectory,
            "Reports",
            $"GoodCheckReport_{secondsSinceEpoch}.xml");
        if (!Path.Exists(Path.GetDirectoryName(filePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
        var root = new XElement("Report");
        doc.Add(root);

        foreach (var operation in operations)
            root.Add(CreateGroup(operation, componentId));

        doc.Save(filePath);
        return filePath;
    }

    private static XElement CreateGroup(GoodCheckOperationModel operation, string componentId)
    {
        // Both are set by InitGoodCheck; XAttribute does not take null.
        string operationName = operation.SiteListName ?? string.Empty;

        var group = new XElement("Group",
            new XAttribute("Name", operationName),
            new XAttribute("FullPath", operation.SiteListPath ?? string.Empty),
            new XAttribute("ComponentId", componentId));

        var matches = Regex.Matches(operation.Output, AnalyzeRegex, RegexOptions.Singleline);
        foreach (Match match in matches)
        {
            string strategy = match.Groups[3].Value;
            string success = match.Groups[4].Value;
            string all = match.Groups[5].Value;

            var matchElem = new XElement("StrategyResult",
                new XElement("Flag", false),
                new XElement("Strategy", strategy),
                new XElement("Success", success),
                new XElement("All", all)
            );

            group.Add(matchElem);
        }

        return group;
    }
}
