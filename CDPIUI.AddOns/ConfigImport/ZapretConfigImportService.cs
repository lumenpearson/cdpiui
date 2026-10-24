using CDPIUI.AddOns.ConfigImport.Zapret;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.AddOns.ConfigImport;

/// <summary>
/// Statically imports zapret batch scripts and text configs without executing them.
/// This importer deliberately does not perform winws1 to winws2 conversion.
/// </summary>
public sealed class ZapretConfigImportService
{
    public ZapretConfigImportResult Import(
        string filePath,
        ZapretConfigImportOptions? options = null)
    {
        options ??= new ZapretConfigImportOptions();
        var context = new ZapretImportContext(options);

        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                context.AddError("FILE_NOT_FOUND", "Config source file was not found.", filePath);
                return context.CreateResult();
            }

            string fullPath = Path.GetFullPath(filePath);
            string extension = Path.GetExtension(fullPath);

            ZapretImportedPayload? payload;
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                payload = ZapretBatchImporter.Import(fullPath, context);
            }
            else if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                payload = ZapretTextConfigImporter.Import(fullPath, expectedGeneration: null, context, depth: 0);
            }
            else
            {
                context.AddError(
                    "UNSUPPORTED_FORMAT",
                    $"The '{extension}' file format is not supported by the zapret importer.",
                    fullPath);
                return context.CreateResult();
            }

            if (payload == null)
                return context.CreateResult();

            string version = ResolveVersion(payload, context.Options);
            return context.CreateResult(
                ZapretConfigItemBuilder.Build(payload, version),
                payload.Generation,
                payload.Variables.Values);
        }
        catch (Exception exception)
        {
            context.AddError("IMPORT_FAILED", exception.Message, filePath);
            return context.CreateResult();
        }
    }

    /// <inheritdoc cref="ZapretSyntax.TrimShellTail(string)"/>
    internal static string TrimShellTail(string input) => ZapretSyntax.TrimShellTail(input);

    /// <inheritdoc cref="ZapretBatchExpander.ExpandFileModifiers(string, string)"/>
    internal static string ExpandFileModifiers(string modifiers, string batchFile) =>
        ZapretBatchExpander.ExpandFileModifiers(modifiers, batchFile);

    private static string ResolveVersion(ZapretImportedPayload payload, ZapretConfigImportOptions options)
    {
        if (payload.Generation == ZapretConfigGeneration.Winws2 &&
            !string.IsNullOrWhiteSpace(payload.BuiltinVersion))
        {
            return payload.BuiltinVersion;
        }

        string? version = null;
        try
        {
            version = options.CurrentVersionResolver?.Invoke(payload.Generation);
        }
        catch (Exception ex)
        {
            // Version lookup is deliberately best-effort. Import must continue.
            Logger.Instance.CreateWarningLog(nameof(ZapretConfigImportService), $"Cannot resolve the current {payload.Generation} version: {ex.Message}");
        }
        if (string.IsNullOrWhiteSpace(version))
            version = TryGetInstalledVersion(payload.Generation);

        return string.IsNullOrWhiteSpace(version)
            ? ZapretConfigImportOptions.CurrentVersionPlaceholder
            : version;
    }

    private static string? TryGetInstalledVersion(ZapretConfigGeneration generation)
    {
        try
        {
            string id = generation == ZapretConfigGeneration.Winws2
                ? HardcodedItemIds.ComponentIds[Components.Zapret2]
                : HardcodedItemIds.ComponentIds[Components.Zapret];

            return DatabaseHelper.Instance.GetItemById(id)?.CurrentVersion;
        }
        catch
        {
            return null;
        }
    }
}
