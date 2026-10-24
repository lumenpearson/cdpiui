using System.Text.RegularExpressions;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration;

/// <summary>
/// Finds the site and IP lists used by the profiles of an expanded startup string.
/// </summary>
internal static partial class SiteListParser
{
    private const string TokenPattern = @"(?<=\s|^)(?:(?:(?:--|/)[^\s]*=""[^""]*"")|""[^""]*""|[^ ]+)+";

    private delegate bool ListOptionMatcher(List<string> tokens, ref int index, out string? name, out string? type);

    [GeneratedRegex(@"("")(/.*?\\)(.*?"")")]
    private static partial Regex WindowConverterRegex();

    /// <summary>
    /// Splits a startup string into its profiles. Only the --new option separates profiles,
    /// not options such as --newfoo.
    /// </summary>
    public static List<string> SplitProfiles(string startupString) =>
        Regex.Split(startupString, @"(?<!\S)--new(?!\S)")
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .ToList();

    public static List<SiteListItem> GetIncludedLists(
        List<string> windows,
        string localItemFolder,
        bool unique,
        bool ignoreNull) =>
        Collect(windows, localItemFolder, unique, ignoreNull, MatchIncludedList);

    public static List<SiteListItem> GetExcludedLists(
        List<string> windows,
        string localItemFolder,
        bool unique,
        bool ignoreNull) =>
        Collect(windows, localItemFolder, unique, ignoreNull, MatchExcludedList);

    private static List<SiteListItem> Collect(
        List<string> windows,
        string localItemFolder,
        bool unique,
        bool ignoreNull,
        ListOptionMatcher matcher)
    {
        var results = new List<SiteListItem>();
        var seenNames = new HashSet<string>();

        foreach (var window in windows)
        {
            string prettyWindow = WindowConverterRegex()
                .Replace(window.Replace(localItemFolder, ""), m => m.Groups[1].Value + m.Groups[3].Value);

            var tokens = Regex.Matches(window, TokenPattern)
                              .Cast<Match>()
                              .Select(m => m.Value.Trim('"'))
                              .ToList();

            bool found = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!matcher(tokens, ref i, out string? name, out string? type) || name == null)
                    continue;

                if (unique && seenNames.Contains(name))
                {
                    var item = results.FirstOrDefault(
                        x => string.Equals(x.Name, Path.GetFileName(name), StringComparison.Ordinal));

                    if (item != null)
                    {
                        item.ApplyParams.Add(window);
                        item.PrettyApplyParams.Add(prettyWindow);
                    }
                    found = true;
                    break;
                }

                seenNames.Add(name);

                results.Add(new SiteListItem
                {
                    Name = Path.GetFileName(name),
                    Type = type,
                    FilePath = name.Replace("\"", ""),
                    ApplyParams = [window],
                    PrettyApplyParams = [prettyWindow]
                });

                found = true;
            }
            if (!found && !ignoreNull)
            {
                results.Add(new SiteListItem
                {
                    Name = "",
                    Type = "NULL",
                    FilePath = "",
                    ApplyParams = [window],
                    PrettyApplyParams = [prettyWindow]
                });
            }
        }
        return results;
    }

    private static bool MatchIncludedList(List<string> tokens, ref int index, out string? name, out string? type)
    {
        string param = tokens[index];
        name = null;
        type = null;

        if (param.StartsWith("--hostlist="))
        {
            name = param.Substring("--hostlist=".Length);
            type = "SiteList";
        }
        else if (param.StartsWith("--blacklist="))
        {
            name = param.Substring("--blacklist=".Length);
            type = "SiteList";
        }
        else if (param.StartsWith("--ipset="))
        {
            name = param.Substring("--ipset=".Length);
            type = "IpList";
        }
        else if (param.StartsWith("--hostlist-auto="))
        {
            name = param.Substring("--hostlist-auto=".Length);
            type = "AutoSiteList";
        }
        else if ((param == "--hostlist" || param == "--ipset" || param == "--hostlist-auto" || param == "--blacklist")
                 && index + 1 < tokens.Count)
        {
            name = tokens[index + 1];
            type = param == "--hostlist" || param == "--blacklist"
                       ? "SiteList"
                       : param == "--ipset"
                           ? "IpList"
                           : "AutoSiteList";
            index++;
        }

        return name != null;
    }

    private static bool MatchExcludedList(List<string> tokens, ref int index, out string? name, out string? type)
    {
        string param = tokens[index];
        name = null;
        type = null;

        if (param.StartsWith("--hostlist-exclude="))
        {
            name = param.Substring("--hostlist-exclude=".Length);
            type = "SiteList";
        }
        else if (param.StartsWith("--ipset-exclude="))
        {
            name = param.Substring("--ipset-exclude=".Length);
            type = "IpList";
        }
        else if ((param == "--hostlist-exclude" || param == "--ipset-exclude")
                 && index + 1 < tokens.Count)
        {
            name = tokens[index + 1];
            type = param == "--hostlist-exclude"
                       ? "SiteList"
                       : "IpList";
            index++;
        }

        return name != null;
    }
}
