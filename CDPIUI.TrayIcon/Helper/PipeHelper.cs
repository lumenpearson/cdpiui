using System.Collections.Specialized;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.Helper.Basic;
using static CDPIUI.TrayIcon.Helper.MsiInstallerHelper;

namespace CDPIUI.TrayIcon.Helper;

public static class PipeHelper
{
    public static async Task<bool> SendOpenWindowPacket(string window, bool openIfNotConnected = false)
    {
        return await SendOpenWindowPacket(window, [], openIfNotConnected);
    }

    public static async Task<bool> SendOpenWindowPacket(string window, NameValueCollection @params, bool openIfNotConnected = false)
    {
        NameValueCollection collection = new()
        {
            { "windowName", window },
            @params
        };

        PresentationMessageModel model = new()
        {
            MessageType = PresentationMessageIds.ShowWindow,
            MessageData = collection,
        };

        if (!openIfNotConnected)
            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        else
            return await TrySendMessage(model);
    }

    public static async Task<bool> SendApplicationPacket(ApplicationMessageIds targetAction)
    {
        ApplicationMessageModel model = new()
        {
            MessageType = targetAction,
        };
        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendMsiInstallationPacket(
        MSIInstallationMessageIds messageId,
        string operationId,
        MsiState? state = null)
    {
        MSIInstallationMessageModel model = new()
        {
            MessageType = messageId,
            MessageData = new()
            {
                { "operationId", operationId },
                { "state", state.ToString() }
            }
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendConPTYPacket(
        CONPTYMessageIds messageId,
        string id,
        NameValueCollection? @params = null,
        bool openIfNotConnected = false)
    {
        NameValueCollection collection = new()
        {
            { "componentId", id },
            @params ?? []
        };

        CONPTYMessageModel model = new()
        {
            MessageType = messageId,
            MessageData = collection,
        };

        if (!openIfNotConnected)
            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        else
            return await TrySendMessage(model, createNoWindow: true);
    }

    public static async Task<bool> SendHelpOutputChunk(
        string requestId,
        int chunkIndex,
        byte[] output)
    {
        CONPTYMessageModel model = new()
        {
            MessageType = CONPTYMessageIds.HelpOutputChunk,
            MessageData = new()
            {
                { "requestId", requestId },
                { "chunkIndex", chunkIndex.ToString() },
                { "output", PipePayloadCodec.Encode(output) },
            },
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendHelpOutputCompleted(
        string requestId,
        int totalChunks,
        int exitCode,
        bool timedOut,
        string error)
    {
        CONPTYMessageModel model = new()
        {
            MessageType = CONPTYMessageIds.HelpOutputCompleted,
            MessageData = new()
            {
                { "requestId", requestId },
                { "totalChunks", totalChunks.ToString() },
                { "exitCode", exitCode.ToString() },
                { "timedOut", timedOut.ToString() },
                { "error", PipePayloadCodec.Encode(error) },
            },
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendGoodCheckPacket(GoodCheckMessageIds messageId, string operationId, string? error = null)
    {
        GoodCheckMessageModel model = new()
        {
            MessageType = messageId,
            MessageData = new()
            {
                { "operationId", operationId },
                { "error", error  }
            }
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendGrantAccessPacket(bool result)
    {
        UtilsMessageModel model = new()
        {
            MessageType = UtilsMessageIds.GrantAccessResponse,
            MessageData = new()
            {
                { "result", result.ToString() },
            }
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendCheckUpdatesPacket()
    {
        UpdateMessageModel model = new()
        {
            MessageType = UpdateMessageIds.CheckForUpdates,
        };

        return await TrySendMessage(model, createNoWindow: true);
    }

    public static async Task<bool> SendCompatibilityCheckPacket()
    {
        CompatibilityCheckMessageModel model = new()
        {
            MessageType = CompatibilityCheckMessageIds.Begin,
        };

        return await TrySendMessage(model, createNoWindow: true);
    }

    public static async Task<bool> SendSettingsPacket(SettingsMessageIds messageId)
    {
        SettingsMessageModel model = new()
        {
            MessageType = messageId,
        };

        return await PipeServer.Instance.SendMessageAsync(model.ToString());
    }

    public static async Task<bool> SendConditionalActionPacket(
        string operationId,
        ConditionalAction action)
    {
        NameValueCollection data = new()
        {
            { "operationId", operationId },
            { "actionType", action.Type.ToString() }
        };

        foreach (var parameter in action.Parameters)
            data[parameter.Name] = parameter.Value;

        ConditionalLaunchMessageModel model = new()
        {
            MessageType = ConditionalLaunchMessageIds.ExecuteAction,
            MessageData = data
        };

        return await TrySendMessage(model, createNoWindow: true);
    }


    private static async Task<bool> TrySendMessage<T>(MessageBaseModel<T> message, bool createNoWindow = false) where T : Enum
    {
        if (!await PipeServer.Instance.SendMessageAsync(message.ToString()))
        {
            // Without a connected GUI the message starts the GUI with --direct:. ----ms-protocol:
            // is the cdpiui:// link activation, which accepts public routes only, so window and
            // notification messages passed that way were dropped. The sender decides about
            // --create-no-window; the target of the message does not change the command line.
            var backgroundArgument = createNoWindow
                ? "--create-no-window "
                : string.Empty;
            return RunHelper.RunAsDesktopUser(
                Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"),
                $"{backgroundArgument}--direct:{message}");
        }

        return true;
    }
}
