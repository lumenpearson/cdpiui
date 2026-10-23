using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.Extensions;
using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.Items;

/// <summary>
/// Reads the short name of each preset pack once while one list of presets is built.
/// </summary>
internal sealed class PackNameLookup
{
    // Ids are compared as the database compares them.
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);

    public string GetDisplayName(string packId)
    {
        if (packId == null)
        {
            return ReadDisplayName(packId);
        }

        if (!names.TryGetValue(packId, out string name))
        {
            name = ReadDisplayName(packId);
            names[packId] = name;
        }
        return name;
    }

    private static string ReadDisplayName(string packId) =>
        DatabaseHelper.Instance.GetItemById(packId)?.ShortName.NullIfEmpty() ?? packId;
}
