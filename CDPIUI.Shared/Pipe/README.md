# Pipe protocol

The GUI (`CDPIUI.exe`) and the tray (`CDPIUI_TrayIcon.exe`) use one named pipe to send messages.
The tray is the pipe server. The GUI is the pipe client.

| Part | File |
|---|---|
| Shared base class, framing, message models | `CDPIUI.Shared/Pipe/` |
| Server (tray) | `CDPIUI.TrayIcon/Helper/Basic/ConnectionHelper.cs` (`PipeServer`), `PipeAccessPolicy.cs` |
| Tray handlers and senders | `CDPIUI.TrayIcon/Helper/Basic/CommandsHandler.cs`, `CDPIUI.TrayIcon/Helper/PipeHelper.cs` |
| Client (GUI) | `CDPIUI.Core/Communication/PipeClientService.cs` |
| GUI handlers and senders | `CDPIUI.Core/Communication/CoreCommandsHandler.cs`, `CDPIUI.Core/Communication/PipeHelper.cs`, `CDPIUI/Commands/CommandsHandler.cs` |

## Pipe and connection

- The pipe name is `SharedConstants.PipeName`: `{203ABE1D-01A2-47C0-B33D-8C8F7934CF4F}_CDPIUI`.
- The server accepts one client at a time (`_maxServerInstances = 1`).
- The client connects with `TokenImpersonationLevel.Identification`.
- After a disconnect, the server creates a new pipe instance and waits for the next client.

The GUI connects in `PipeClientService.Start`:

1. The GUI tries to connect for 2 seconds.
2. If a `CDPIUI_TrayIcon` process runs, the GUI tries again for 15 seconds.
3. If there is no connection, the GUI starts `CDPIUI_TrayIcon.exe` with `runas` and stops its own process.
4. The tray then starts the GUI again (see [CDPIUI.TrayIcon/README.md](../../CDPIUI.TrayIcon/README.md)).

## Security

### Pipe access

`PipeAccessPolicy.CreatePipeSecurity` sets this access list:

| Account | Access |
|---|---|
| The tray user (owner) | `FullControl` |
| `LocalSystem` | `FullControl` |
| The desktop user, if it is not the tray user | `ReadWrite` |
| `Network` | `FullControl`, deny |

The desktop user is the owner of the shell window process (`GetShellWindow`).
This rule is necessary when the tray runs elevated with a different administrator account.

### Peer executable check

Each side checks the executable of the other process (`PipePeerVerifier`):

- The server calls `IsClientExpected`. The expected client is `CDPIUI.exe` in the tray directory.
- The client calls `IsServerExpected`. The expected server is `CDPIUI_TrayIcon.exe` in the GUI directory.
- Release builds compare the full image paths.
- `DEBUG` builds compare only the file names, because the GUI and the tray have different output directories.

The server disconnects a client that fails the check.
The client stops its own process if the server fails the check.

### Authorization

1. The server sends `Service/ConnectOK` after the connection.
2. The client sends `Service/RequestAuth` with the `GUID` key. The value is `Secret.AuthGuid`.
3. If the tray `Secret.AuthGuid` is not empty and the value is equal to it, the server sends `Service/AuthOK`.
4. In all other conditions, the server sends `Service/AuthFAIL` and disconnects the client.

Rules:

- An empty `Secret.AuthGuid` in the tray authorizes no client.
- The server disconnects the client if the client is not authorized 15 seconds after the connection.
- A failed `RequestAuth` always disconnects the client, also after an earlier `AuthOK`. The client cannot try again on the same connection.
- Before authorization, the server sends only `Service` messages (`PipeServer.CanSend`).
- Before authorization, the server handles only `RequestAuth`. It ignores all other messages and writes a warning to the log.
- The GUI and the tray must be built with the same `Secret.AuthGuid`.
- The log entries for received, queued and failed messages replace the `GUID` value with `***` (`PipeServiceBase.RedactSecrets`).

CAUTION: Do not build with an empty `AuthGuid`. With an empty value, the GUI cannot connect to the tray. `create_secret_stub.ps1` and `create_release_secret.ps1` write a random value.

## Framing

`StreamString` writes each message as one frame:

- The text is UTF-16 LE (`Encoding.Unicode`).
- A message shorter than 0xFFFF bytes has a 2-byte big-endian length before the text.
- A longer message has the marker `0xFF 0xFF`, then a 4-byte big-endian length.
- The maximum message length is 16 MiB. A longer message causes `InvalidDataException`.

`PipeServiceBase.SendMessageAsync` uses a lock, so only one frame is written at a time.

## Message model

A message is a URI:

```text
cdpiui://<Target>/<MessageType>?<key>=<value>&<key>=<value>&
```

- `<Target>` is a `PipeMessageTargetIds` name.
- `<MessageType>` is a name from the message enum of the target, for example `CONPTYMessageIds`.
- `MessageBaseModel<T>.ToString` writes the enum name, not the integer. `IntMessageType` is not used in the URI.
- The data is a `NameValueCollection`. Keys and values are escaped with `Uri.EscapeDataString`.
- A message without data has no `?` part.

`PipeModelConvertor.ConvertBack` reads a message:

- The host and the path are parsed with `Enum.Parse(..., ignoreCase: true)`. `Uri` changes the host to lower case.
- The query is parsed with `HttpUtility.ParseQueryString`.
- An unknown target or type causes an exception. `PipeServiceBase` writes it to the log and ignores the message.
- A `null` value is sent as an empty string. The receiver gets `""`, not `null`.

Use `PipePayloadCodec` (URL-safe Base64 of UTF-8) for binary data and for paths that must not change on the way.
`CaptureHelpOutput` and the help output messages use it.

## Message families

Direction: "GUI → tray" or "tray → GUI". In the GUI, `CoreCommandsHandler` and `CDPIUI/Commands/CommandsHandler.cs` handle the messages.

### Service

| Type | Direction | Data |
|---|---|---|
| `ConnectOK` | tray → GUI | none |
| `RequestAuth` | GUI → tray | `GUID` |
| `AuthOK` | tray → GUI | none. The GUI raises `Connected`. |
| `AuthFAIL` | tray → GUI | none. The GUI writes an error to the log. |

### Presentation

| Type | Direction | Data |
|---|---|---|
| `ShowWindow` | tray → GUI | `windowName` and optional window keys, for example `page`, `id`, `isUpdateRequested`, `action`, `helpUrl` |

### CONPTY

Component processes. `componentId` is the component id, for example `CSZTBN012`.

| Type | Direction | Data | Effect |
|---|---|---|---|
| `StartProcessId` | GUI → tray | `componentId`, `exePath`, `args` | Starts the component through ConPTY. |
| `StopProcessId` | GUI → tray | `componentId` | Stops the component. |
| `RestartProcessId` | GUI → tray | `componentId` | Restarts the component. |
| `StopService` | GUI → tray | none | Stops the WinDivert components, then stops and deletes the `WinDivert` service. |
| `GetProcessIdFullOutput` | GUI → tray | `componentId` | The tray sends `ProcessIdFullOutput` for the task of `componentId`. If `componentId` is empty, the tray sends it for all tasks. |
| `GetAllProcessStates` | GUI → tray | none | The tray sends the state and the process name of all tasks. |
| `ProcessIdStartupArgsChanged` | GUI → tray | `componentId` | The next start asks the GUI for a new startup string. |
| `CaptureHelpOutput` | GUI → tray | `componentId`, `requestId`, `exePath` (`PipePayloadCodec`) | Runs `<exe> --help` for 10 seconds. |
| `GetProcessIdState` | GUI → tray | `componentId` | The tray sends the state and the process name of the task of `componentId`. If `componentId` is empty, the tray sends them for all tasks. |
| `GetStartupString` | tray → GUI | `componentId` | The GUI starts the task. |
| `GetAllStartupStrings` | tray → GUI | `componentId` (empty) | The GUI starts all autorun components. |
| `CleanOutputForId` | tray → GUI | `componentId` | The GUI clears the output. |
| `MarkProcessIdAsStarted` | tray → GUI | `componentId` | |
| `MarkProcessIdAsStopped` | tray → GUI | `componentId`; on error also `errorHappens`, `errorMessage`, `errorObject`, `stateSnapshot` | With `stateSnapshot=true`, the GUI does not open the error window. |
| `ChangeProcessIdExecutable` | tray → GUI | `componentId`, `processName` | |
| `ProcessIdNewOutput` | tray → GUI | `componentId`, `output` | |
| `ProcessIdFullOutput` | tray → GUI | `componentId`, `output` | |
| `HelpOutputChunk` | tray → GUI | `requestId`, `chunkIndex`, `output` (`PipePayloadCodec`) | Chunks are 12 KiB of UTF-8. |
| `HelpOutputCompleted` | tray → GUI | `requestId`, `totalChunks`, `exitCode`, `timedOut`, `error` (`PipePayloadCodec`) | The GUI checks that all chunks arrived. |

### GoodCheck

| Type | Direction | Data |
|---|---|---|
| `Start` | GUI → tray | `operationId`, `exeFileName`, `args` |
| `Stop` | GUI → tray | none |
| `Running` | tray → GUI | `operationId`, `error` |
| `Died` | tray → GUI | `operationId`, `error` |
| `DiedViaError` | tray → GUI | `operationId`, `error` |

The GUI reads `operationId` as an integer.

### Utils

| Type | Direction | Data |
|---|---|---|
| `GrantAccessRequest` | GUI → tray | `file`: a directory below `Store` |
| `GrantAccessResponse` | tray → GUI | `result`: `True` or `False` |

### Settings

| Type | Direction | Data |
|---|---|---|
| `AddToAutorun` | GUI → tray | none |
| `RemoveFromAutorun` | GUI → tray | none |
| `ReloadSettings` | GUI → tray | none |
| `ComponentSetupFinished` | GUI → tray | `componentId` |
| `ComponentSetupNotFinished` | GUI → tray | `componentId` |
| `ComponentNotInstalled` | GUI → tray | `componentId`. The tray stops and removes the task. |
| `AutorunFalse` | tray → GUI | none. The GUI sets `SYSTEM/autorun` to `false`. |

### Update

| Type | Direction | Data |
|---|---|---|
| `BeginApplicationUpdate` | GUI → tray | `filePath`: the update package (`.msi` or ZIP) |
| `UpdatesAreAvailable` | GUI → tray | none. The tray shows a notification. |
| `CheckForUpdates` | tray → GUI | none |

### MSIInstallation

| Type | Direction | Data |
|---|---|---|
| `Begin` | GUI → tray | `operationId`, `fileName` |
| `Kill` | GUI → tray | `operationId` |
| `SetOperationStatus` | tray → GUI | `operationId`, `state`: a `MsiState` name |
| `RemoveOperationId` | tray → GUI | `operationId`, `state` (empty) |

`MsiState` values: `GettingReady`, `Installing`, `Complete`, `CompleteRestartRequest`, `ExceptionHappens`.
The enum exists in `CDPIUI.TrayIcon/Helper/MsiInstallerHelper.cs` and in `CDPIUI.Core/Store/MSI/MsiInstallerService.cs`. The names must stay equal.

### Proxy

All messages go from the GUI to the tray.

| Type | Data |
|---|---|
| `Init` | `componentId`, `proxyFirePath` |
| `Setup` | `componentId`, `proxyType` (`AllSystem` or `ProxiFyre`), `ip`, `port` |
| `Clean` | `componentId` |

### CompatibilityCheck

| Type | Direction | Data |
|---|---|---|
| `Begin` | tray → GUI | none |

### Notifications

All messages go from the GUI to the tray. The tray shows a toast notification.

| Type | Data |
|---|---|
| `ProxySetupRequired` | `componentName` |
| `CompatibilityCheckAssistant` | `componentName` |

### Application

| Type | Direction | Data |
|---|---|---|
| `HardRestart` | GUI → tray | none. The tray closes the GUI, starts it again and exits. |
| `CloseApplicationUI` | tray → GUI | none. The GUI stops its process. |

### ConditionalLaunch

| Type | Direction | Data |
|---|---|---|
| `ExecuteAction` | tray → GUI | `operationId`, `actionType`, and the action parameters |
| `ActionCompleted` | GUI → tray | `operationId`, `success`, `error` |
| `ReloadTasks` | GUI → tray | `tasksDirectory` |
| `ExecuteTask` | GUI → tray | `taskId` |

The tray waits 45 seconds for `ActionCompleted`.

## Messages when the GUI is not connected

`PipeHelper.TrySendMessage` in the tray starts the GUI as the desktop user when the send fails.
The message goes in the argument `--direct:<message>` for all targets.

- For background messages to the `Service`, `CONPTY`, `Settings`, `Application`, `ConditionalLaunch`, `CompatibilityCheck` and `Update` targets, the tray also adds `--create-no-window`.
- For the other targets, for example `Presentation`, the tray does not add `--create-no-window`.

The GUI handles `--direct:` with `CoreCommandsHandler`, then with `CommandsHandler.HandleCommand`.
The tray does not use `----ms-protocol:`. The GUI handles `----ms-protocol:` with `CommandUriMapper`, which accepts only public routes. Thus a pipe message in `----ms-protocol:`, for example `ShowWindow`, has no effect.

## Add a message

1. Add the value at the end of the enum in `Pipe/Models/PipeMessageIds.cs`.
2. For a new target, add a model in `PipeMessageModels.cs` and a case in `PipeModelConvertor.ConvertBack`.
3. Add a sender in the `PipeHelper` of the sending side.
4. Add a handler in `CommandsHandler` (tray) or in `CoreCommandsHandler` / `CDPIUI/Commands/CommandsHandler.cs` (GUI).
5. In the tray, check each path from the GUI with `TrustedPaths` before you use it.

Tests: `CDPIUI.Tests/Shared/PipeMessageModelTests.cs`, `StreamStringTests.cs`, `PipePayloadCodecTests.cs`.
