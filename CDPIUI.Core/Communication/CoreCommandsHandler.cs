using System.Diagnostics;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.MSI;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Pipe.Models;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;

namespace CDPIUI.Core.Communication;

public class CoreCommandsHandler
{
    public static bool HandleCommand(IPipeMessage message)
    {
        if (!CanHandle(message))
            return false;

        _ = HandleCommandAsync(message);
        return true;
    }

    public static async Task<bool> HandleCommandAsync(IPipeMessage message)
    {
        switch (message)
        {
            case ServiceMessageModel model:
                HandleServiceMessage(model);
                break;

            case CONPTYMessageModel model:
                await HandleCONPTYMessage(model);
                break;

            case SettingsMessageModel model:
                HandleSettingsMessage(model);
                break;

            case MSIInstallationMessageModel model:
                HandleMSIMessage(model);
                break;

            case ApplicationMessageModel model:
                HandleApplicationMessage(model);
                break;
            case ConditionalLaunchMessageModel model:
                await HandleConditionalLaunchMessage(model);
                break;
            default:
                return false;
        }
        return true;
    }

    private static bool CanHandle(IPipeMessage message) =>
        message is ServiceMessageModel or
            CONPTYMessageModel or
            SettingsMessageModel or
            MSIInstallationMessageModel or
            ApplicationMessageModel or
            ConditionalLaunchMessageModel;

    private static void HandleServiceMessage(ServiceMessageModel model)
    {
        switch (model.MessageType)
        {
            case ServiceMessageIds.AuthFAIL:
                Logger.Instance
                    .CreateErrorLog(nameof(PipeClientService), "Auth status: FAIL. The pipe key in .session\\pipe.key does not match the key of the running tray.");
                return;
            default:
                return;
        }
    }

    private static async Task HandleCONPTYMessage(CONPTYMessageModel model)
    {
        if (ConPtyHelpCaptureClient.HandleMessage(model))
        {
            return;
        }

        switch (model.MessageType)
        {
            case CONPTYMessageIds.GetStartupString:
                await RunComponentTaskAsync(model);
                return;

            case CONPTYMessageIds.GetAllStartupStrings:
                await ComponentTasksManager.Instance.RunAllPreferredActions();
                return;

            case CONPTYMessageIds.CleanOutputForId:
                await ClearProcessOutputAsync(model);
                return;

            case CONPTYMessageIds.MarkProcessIdAsStarted:
                await MarkProcessAsStartedAsync(model);
                return;

            case CONPTYMessageIds.MarkProcessIdAsStopped:
                await MarkProcessAsStoppedAsync(model);
                return;

            case CONPTYMessageIds.ChangeProcessIdExecutable:
                await ChangeProcessExecutableAsync(model);
                return;

            case CONPTYMessageIds.ProcessIdNewOutput:
                await AddProcessOutputAsync(model);
                return;

            case CONPTYMessageIds.ProcessIdFullOutput:
                await SetProcessOutputAsync(model);
                return;
        }
    }

    private static async Task RunComponentTaskAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        if (id == null)
            return;

        await ComponentTasksManager.Instance.CreateAndRunNewTask(id);
    }

    private static async Task ClearProcessOutputAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        if (id == null)
            return;

        (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager?
            .ClearOutput();
    }

    private static async Task MarkProcessAsStartedAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        if (id == null)
            return;

        (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager?
            .MarkAsStarted();
    }

    private static async Task MarkProcessAsStoppedAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        if (id == null)
            return;

        var title = model.MessageData?["errorMessage"];
        var text = model.MessageData?["errorObject"];

        var pm = (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager;

        if (title != null && text != null)
        {
            Debug.WriteLine(title, text);
            if (pm != null)
                await pm.ShowErrorMessage(title, text, showWindow: model.MessageData?["stateSnapshot"] != "true");
        }
        else
        {
            pm?.MarkAsFinished();
        }
    }

    private static async Task ChangeProcessExecutableAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        var processName = model.MessageData?["processName"];

        if (id == null || processName == null)
            return;

        (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager?
            .ChangeProcName(processName);
    }

    private static async Task AddProcessOutputAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        var output = model.MessageData?["output"];

        if (id == null || output == null)
            return;

        (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager?
            .AddOutput(output);
    }

    private static async Task SetProcessOutputAsync(CONPTYMessageModel model)
    {
        var id = model.MessageData?["componentId"];
        var output = model.MessageData?["output"];

        if (id == null || output == null)
            return;

        (await ComponentTasksManager.Instance.GetTaskFromId(id))?
            .ProcessManager?
            .SetOutput(output);
    }

    private static void HandleSettingsMessage(SettingsMessageModel model)
    {
        switch (model.MessageType)
        {
            case SettingsMessageIds.AutorunFalse:
                SettingsManager.Instance.SetValue("SYSTEM", "autorun", false);
                return;
        }
    }

    private static void HandleMSIMessage(MSIInstallationMessageModel model)
    {
        switch (model.MessageType)
        {
            case MSIInstallationMessageIds.SetOperationStatus:
                {
                    var id = model.MessageData?["operationId"];
                    var state = model.MessageData?["state"]?.ToEnum<MsiState>();

                    if (id == null || state == null)
                        return;

                    MsiInstallerQueueManager.Instance.GetMsiInstallerMessage(id, (MsiState)state);
                    return;
                }

            case MSIInstallationMessageIds.RemoveOperationId:
                {
                    var id = model.MessageData?["operationId"];
                    if (id == null)
                        return;

                    MsiInstallerQueueManager.Instance.RemoveMsiInstallerModel(id, notify: false);
                    return;
                }
        }
    }


    private static void HandleApplicationMessage(ApplicationMessageModel model)
    {
        switch (model.MessageType)
        {
            case ApplicationMessageIds.CloseApplicationUI:
                Process.GetCurrentProcess().Kill();
                return;
        }
    }

    private static async Task HandleConditionalLaunchMessage(ConditionalLaunchMessageModel model)
    {
        if (model.MessageType != ConditionalLaunchMessageIds.ExecuteAction)
            return;

        var operationId = model.MessageData?["operationId"];
        if (string.IsNullOrWhiteSpace(operationId))
            return;

        try
        {
            if (!Enum.TryParse<ConditionalActionType>(
                model.MessageData?["actionType"],
                ignoreCase: true,
                out var actionType))
            {
                throw new ArgumentException("Unknown conditional action type.");
            }

            await ExecuteConditionalActionAsync(actionType, model);

            await PipeHelper.SendConditionalLaunchResult(operationId, success: true);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(
                nameof(CoreCommandsHandler),
                $"Conditional action failed: {ex}");
            await PipeHelper.SendConditionalLaunchResult(operationId, success: false, ex.Message);
        }
    }

    private static async Task ExecuteConditionalActionAsync(
        ConditionalActionType actionType,
        ConditionalLaunchMessageModel model)
    {
        switch (actionType)
        {
            case ConditionalActionType.ApplyPreset:
                ApplyPreset(model);
                break;

            case ConditionalActionType.StartComponent:
                {
                    var componentId = RequireParameter(model, "componentId");
                    if (!await ComponentTasksManager.Instance.IsTaskRunning(componentId))
                        await StartComponentOrThrowAsync(componentId);
                    break;
                }

            case ConditionalActionType.ToggleComponent:
                {
                    var componentId = RequireParameter(model, "componentId");
                    if (!await ComponentTasksManager.Instance.IsTaskRunning(componentId))
                        await StartComponentOrThrowAsync(componentId);
                    else
                        await ComponentTasksManager.Instance.StopTask(componentId);
                }
                break;

            case ConditionalActionType.StartAutorunComponents:
                await ComponentTasksManager.Instance.RunAllPreferredActions();
                break;

            case ConditionalActionType.CheckStoreUpdates:
                await StoreHelper.Instance.CheckUpdates();
                if (StoreHelper.Instance.IsExceptonHappensWhileCheckingUpdates)
                    throw new InvalidOperationException("One or more Store update checks failed.");
                break;

            case ConditionalActionType.CheckApplicationUpdates:
                await ApplicationUpdate.Instance.CheckForUpdates(notify: true);
                if (ApplicationUpdate.Instance.ErrorHappened)
                {
                    throw new InvalidOperationException(
                        ApplicationUpdate.Instance.ErrorInfo ??
                        "The application update check failed.");
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Action '{actionType}' cannot be executed by CDPIUI.Core.");
        }
    }

    private static async Task StartComponentOrThrowAsync(string componentId)
    {
        await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
        var task = await ComponentTasksManager.Instance.GetTaskFromId(componentId);
        if (task?.ProcessManager.IsErrorHappens == true)
        {
            throw new InvalidOperationException(
                task.ProcessManager.LastError?.ErrorCode ??
                $"Component '{componentId}' could not be started.");
        }
    }

    private static void ApplyPreset(ConditionalLaunchMessageModel model)
    {
        var componentId = RequireParameter(model, "componentId");
        var packId = RequireParameter(model, "packId");
        var fileName = RequireParameter(model, "fileName");

        ComponentItemsLoaderHelper.Instance.Init();
        var componentHelper = ComponentItemsLoaderHelper.Instance
            .GetComponentHelperFromId(componentId)
            ?? throw new InvalidOperationException($"Component '{componentId}' was not found.");

        SettingsManager.Instance.SetValue(["CONFIGS", componentId], "configFile", fileName);
        SettingsManager.Instance.SetValue(["CONFIGS", componentId], "configId", packId);
        componentHelper.ReInitConfigs();
    }

    private static string RequireParameter(
        ConditionalLaunchMessageModel model,
        string name)
    {
        var value = model.MessageData?[name];
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Parameter '{name}' is required.");
        return value;
    }
}
