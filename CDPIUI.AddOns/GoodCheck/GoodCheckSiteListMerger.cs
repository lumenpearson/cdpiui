using System.Text;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Writes the lines of several site lists into one file, each line once, in the order first seen.
/// </summary>
internal static class GoodCheckSiteListMerger
{
    public static void MergeUniquePreserveOrder(
        List<GoodCheckSiteListModel> siteLists,
        string outputFile,
        Encoding? encoding = null,
        bool trimLines = true,
        bool ignoreEmpty = true,
        StringComparer? comparer = null)
    {
        // Without BOM: GoodCheck took it for a part of the first domain.
        encoding ??= new UTF8Encoding(false);
        comparer ??= StringComparer.Ordinal;

        var seen = new HashSet<string>(comparer);

        using var writer = new StreamWriter(outputFile, false, encoding);
        foreach (var siteList in siteLists)
        {
            if (!File.Exists(siteList.SiteListPath))
                continue;
            using var reader = new StreamReader(siteList.SiteListPath, encoding);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (trimLines)
                    line = line.Trim();
                if (ignoreEmpty && string.IsNullOrEmpty(line))
                    continue;

                if (seen.Add(line))
                {
                    writer.WriteLine(line);
                }
            }
        }
    }
}
