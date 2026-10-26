using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Update.exe compiles this file as well (linked in Update.csproj): it must not load
// CDPIUI.Shared.dll from the folder it replaces, and both sides must use the same rule.
#if UPDATE_TOOL
namespace Update;
#else
namespace CDPIUI.Shared.Basic.Filesystem;
#endif

/// <summary>
/// The folders of an application update package that are installed, in order. The tray checks
/// the signed catalog of exactly these folders and Update.exe installs exactly these, so a
/// package that the two could read differently is rejected instead of guessed.
/// </summary>
#if UPDATE_TOOL
internal
#else
public
#endif
static class UpdatePackageLayers
{
    public const string LayersPrefix = "layers/";
    public const string UiRootPrefix = "CDPIUI/";

    /// <summary>
    /// Returns layers/0/, layers/1/, ... of a package made by the application, or CDPIUI/ of a
    /// published patch. Throws <see cref="InvalidDataException"/> for a layer that is not a
    /// plain number, a file directly in layers/, or a package with both layers/ and CDPIUI/.
    /// </summary>
    public static List<string> GetPrefixes(IEnumerable<string> entryNames)
    {
        var numbers = new SortedSet<int>();
        bool hasLayers = false;
        bool hasUiRoot = false;
        foreach (string entryName in entryNames)
        {
            string name = entryName.Replace('\\', '/');
            if (name.StartsWith(UiRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hasUiRoot = true;
                continue;
            }
            if (!name.StartsWith(LayersPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            hasLayers = true;
            string rest = name.Substring(LayersPrefix.Length);
            if (rest.Length == 0)
                continue; // The entry of the layers/ folder itself.
            int slash = rest.IndexOf('/');
            if (slash < 0)
                throw new InvalidDataException($"The update package has a file directly in {LayersPrefix}: {entryName}");
            if (!TryParseLayer(rest.Substring(0, slash), out int number))
                throw new InvalidDataException($"The update package has a layer that is not a number: {entryName}");
            numbers.Add(number);
        }

        if (hasLayers && hasUiRoot)
            throw new InvalidDataException($"The update package has both {LayersPrefix} and {UiRootPrefix}.");
        if (hasLayers)
        {
            if (numbers.Count == 0)
                throw new InvalidDataException($"The update package has an empty {LayersPrefix} folder.");
            return numbers.Select(number => $"{LayersPrefix}{number.ToString(CultureInfo.InvariantCulture)}/").ToList();
        }
        return new List<string> { UiRootPrefix };
    }

    // Digits only and no leading zero, so "01", "+1" or " 1" cannot name the same layer as "1".
    private static bool TryParseLayer(string segment, out int number)
    {
        number = 0;
        if (segment.Length == 0 || segment.Length > 6 || (segment.Length > 1 && segment[0] == '0'))
            return false;
        if (segment.Any(character => character < '0' || character > '9'))
            return false;
        number = int.Parse(segment, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }
}
