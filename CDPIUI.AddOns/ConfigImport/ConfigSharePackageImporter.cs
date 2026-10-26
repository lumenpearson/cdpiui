using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Opens a shared Config package for review; the package names its own target component.
/// </summary>
internal static class ConfigSharePackageImporter
{
    public static ConfigImportResult Import(string path, ConfigImportTarget? selectedTarget)
    {
        var target = selectedTarget ?? new ConfigImportTarget("", "", "", null);
        try
        {
            var package = new ConfigShare.ConfigShareService().Read(path);
            string componentId = package.Config.target![0];
            var component = DatabaseHelper.Instance.GetItemById(componentId);
            target = component == null
                ? new ConfigImportTarget(componentId, componentId,
                    componentId == HardcodedItemIds.ComponentIds[Components.Zapret] ? "winws" : "",
                    package.Config.target.ElementAtOrDefault(1))
                : ConfigImportTargets.Create(component);
            return new ConfigImportResult
            {
                SharedPackage = package,
                Config = package.Config,
                Target = target,
                SourcePath = Path.GetFullPath(path),
                Issues = [],
                SourceFiles = [path],
                ReferencedFiles = package.Manifest.Resources.Select(resource => Path.Combine(package.DirectoryPath, resource.Path)).ToList(),
                MissingReferencedFiles = [],
                MissingFileResolutions = [],
                GeneratedFiles = []
            };
        }
        catch (ConfigShare.ConfigShareException exception)
        {
            return new ConfigImportResult
            {
                Target = target,
                SourcePath = path,
                Config = null,
                Issues = [new(ConfigImportIssueSeverity.Error, exception.Code, exception.Message, path)],
                SourceFiles = [path],
                ReferencedFiles = [],
                MissingReferencedFiles = [],
                MissingFileResolutions = [],
                GeneratedFiles = []
            };
        }
    }
}
