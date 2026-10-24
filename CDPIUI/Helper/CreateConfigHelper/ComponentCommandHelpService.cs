using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices.Helpers;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ComponentCommandHelpService
{
    private const int ParserFormatVersion = 3;
    private static readonly ConcurrentDictionary<string, ComponentCommandHelpDocument> Cache = new();
    private static readonly TimeSpan HelpTimeout = TimeSpan.FromSeconds(15);

    private static string Text(string key) => WinUI3Localizer.Localizer.Get().GetLocalizedString(key);

    public async Task<ComponentCommandHelpDocument> LoadAsync(
        string componentId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        ComponentHelper component = ComponentItemsLoaderHelper.Instance
            .GetComponentHelperFromId(componentId);
        string executablePath = component?.GetExecutablePath();

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return CreateError(componentId, executablePath, Text("ConfigMakerHelpExecutableNotFound"));
        }

        string cacheKey = $"{executablePath}|{File.GetLastWriteTimeUtc(executablePath).Ticks}|parser:{ParserFormatVersion}";
        if (!forceRefresh && Cache.TryGetValue(cacheKey, out ComponentCommandHelpDocument cached))
        {
            return cached;
        }

        ComponentCommandHelpDocument result = await ReadHelpAsync(
            componentId,
            executablePath,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Error))
        {
            Cache[cacheKey] = result;
        }

        return result;
    }

    public static ComponentCommandHelpDocument Parse(
        string componentId,
        string executablePath,
        string rawText,
        string error = "") =>
        ComponentCommandHelpParser.Parse(componentId, executablePath, rawText, error);

    private static async Task<ComponentCommandHelpDocument> ReadHelpAsync(
        string componentId,
        string executablePath,
        CancellationToken cancellationToken)
    {
        try
        {
            ConPtyHelpCaptureResult capture = await ConPtyHelpCaptureClient.CaptureHelpAsync(
                componentId,
                executablePath,
                HelpTimeout,
                cancellationToken);
            string rawText = ConPtyOutputSanitizer.Sanitize(capture.Output).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                string error = !string.IsNullOrWhiteSpace(capture.Error)
                    ? capture.Error
                    : capture.TimedOut
                        ? Text("ConfigMakerHelpTimedOut")
                        : string.Format(Text("ConfigMakerHelpNoOutput"), capture.ExitCode);
                return CreateError(
                    componentId,
                    executablePath,
                    error);
            }

            string warning = capture.TimedOut
                ? Text("ConfigMakerHelpIncomplete")
                : string.Empty;
            return Parse(componentId, executablePath, rawText, warning);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateError(componentId, executablePath, exception.Message);
        }
    }

    private static ComponentCommandHelpDocument CreateError(
        string componentId,
        string executablePath,
        string error) => new()
        {
            ComponentId = componentId,
            ExecutablePath = executablePath ?? string.Empty,
            Error = error,
        };
}
