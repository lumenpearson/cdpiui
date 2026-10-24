using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CDPIUI.Shared.Basic;

/// <summary>
/// Finds and changes the Group and Setting elements of a settings document.
/// </summary>
internal static class SettingsXmlTree
{
    public static XElement? FindGroup(XElement parent, string name) =>
        parent
            .Elements("Group")
            .FirstOrDefault(g => (string?)g.Attribute("Name") == name);

    /// <returns>The group at <paramref name="groupPath"/>, or null if any group of the path is missing</returns>
    public static XElement? FindGroup(XElement root, IEnumerable<string> groupPath)
    {
        XElement? current = root;
        foreach (var grp in groupPath)
        {
            current = FindGroup(current, grp);
            if (current == null)
                return null;
        }
        return current;
    }

    public static XElement? FindSetting(XElement? group, string key) =>
        group?
            .Elements("Setting")
            .FirstOrDefault(s => (string?)s.Attribute("Key") == key);

    public static void ApplySetting(XElement root, IEnumerable<string> groupPath, string key, string type, string? valueString)
    {
        XElement current = root;
        foreach (var grp in groupPath)
        {
            var next = FindGroup(current, grp);

            if (next == null)
            {
                next = new XElement("Group", new XAttribute("Name", grp));
                current.Add(next);
            }

            current = next;
        }

        var setting = FindSetting(current, key);

        if (setting != null)
        {
            setting.SetAttributeValue("Type", type);
            setting.SetAttributeValue("Value", valueString);
        }
        else
        {
            current.Add(new XElement("Setting",
                new XAttribute("Key", key),
                new XAttribute("Type", type),
                new XAttribute("Value", valueString)));
        }
    }
}
