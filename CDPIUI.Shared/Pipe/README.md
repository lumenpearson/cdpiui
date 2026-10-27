**English** | [Русский](README.ru.md)

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

- The pipe has no fixed name. The tray makes a new name at each start: `CDPIUI_` and 32 upper-case hexadecimal characters, that is 128 random bits (`PipeSession.CreatePipeName`).
- The tray creates the pipe with `PipeOptions.FirstPipeInstance`. If a pipe with this name exists, the creation fails. Thus the tray never adds an instance to a pipe of another program.
- The tray writes the name to the session file only after it has created the pipe (see [Session file](#session-file)). Thus no other program can create a pipe with this name first.
- The server accepts one client at a time (`_maxServerInstances = 1`).
- The client connects with `TokenImpersonationLevel.Identification`.
- After a disconnect, the server creates a new pipe instance with the same name and waits for the next client.
- If the server cannot create the pipe or write the session file, it makes a new name and tries again after 2 seconds. A program of the desktop user can read the name and create a pipe with it while the server creates the next instance. That program does not know the new name until the server has created the pipe again.

The GUI connects in `PipeClientService.Start`:

1. The GUI tries to connect for 2 seconds.
2. If a `CDPIUI_TrayIcon` process runs, the GUI tries again for 15 seconds.
3. If there is no connection, the GUI starts `CDPIUI_ElevatedLauncher.exe tray <arguments>` with `runas` and stops its own process. The launcher starts `CDPIUI_TrayIcon.exe` without the .NET variables of the user profile.
4. The tray then starts the GUI again (see [CDPIUI.TrayIcon/README.md](../../CDPIUI.TrayIcon/README.md)).

In steps 1 and 2, the GUI reads the session file each 0.5 seconds and tries to connect to the pipe that the file names. Thus the GUI finds a tray that starts during the wait. If the file is missing or damaged, the GUI operates as if the tray does not run.

One tray runs on the computer (the mutex `Global\CDPIUI_TrayInstance`). The random name lets a second pipe exist, but the components, the system proxy and the session file are the same for all Windows sessions. Thus a tray in a second Windows session still exits at start.

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

### Session file

The session file is `<application folder>\.session\pipe.key` (`PipeSessionFile`). The tray writes it (`PipeSessionWriter`). The GUI reads it.

- Line 1 is the pipe name. Line 2 is the pipe key.
- The key is new at each start: 32 random bytes, hexadecimal (`PipeSession.CreateKey`).
- At start, before the pipe server starts, the tray deletes the file of an earlier start.
- After the tray has created the pipe, it writes the file. It writes a new file with a random temporary name, then it renames this file to `pipe.key`. Thus a reader never gets a part of the file. The tray replaces a link or a file of another owner at this path. It does not write through them.
- The file has a protected access list: SYSTEM, Administrators and the tray user have full control, and the desktop user can read it. Other users cannot read the name or the key.
- In an MSI installation, only administrators can create files in the installation folder. Thus nobody can put a file there before the tray.
- The GUI reads the name and the key together, before each connection attempt. Thus the name and the key always come from the same tray start. A restarted tray has a new name and a new key. The GUI uses them at its next connection.
- The GUI ignores a file that is larger than 1 KiB, that does not have exactly two lines, or that has a name or a key in a different format.
- The GUI also ignores the one-line format of earlier versions (the key only). A tray of an earlier version uses the fixed name, and an update stops the GUI and the tray before it replaces their files. Thus a new GUI and an earlier tray do not operate together.

The file of a tray that stopped, for example after a crash, stays until the next tray start. Another program can create a pipe with the old name. The GUI can then connect to this pipe. The GUI checks the image of the pipe server before it sends the key (see [Peer executable check](#peer-executable-check)). Thus the other program does not get the key, and the GUI stops its own process.

NOTE: The name and the key separate the users of the computer, not the programs of one user. When the desktop user elevates the tray with their own account (the usual case), every program of that user can read `pipe.key`, and the tray user can also replace or delete it. A program of that user can also keep the only pipe instance busy, or keep `pipe.key` open so that the tray cannot replace it. These are denials of service by the same user. The check that only the GUI connects is the client path check of `PipePeerVerifier`.

### Authorization

1. The server sends `Service/ConnectOK` after the connection.
2. The client sends `Service/RequestAuth` with the `GUID` key. The value is the key from the same read of the session file as the pipe name.
3. If the key of the tray is not empty and the value is equal to it, the server sends `Service/AuthOK`. The comparison takes the same time for each wrong value.
4. In all other conditions, the server sends `Service/AuthFAIL` and disconnects the client.

Rules:

- An empty key authorizes no client.
- The server disconnects the client if the client is not authorized 15 seconds after the connection.
- A failed `RequestAuth` always disconnects the client, also after an earlier `AuthOK`. The client cannot try again on the same connection.
- Before authorization, the server sends only `Service` messages (`PipeServer.CanSend`).
- Before authorization, the server handles only `RequestAuth`. It ignores all other messages and writes a warning to the log.
- The log entries for received, queued and failed messages replace the `GUID` value with `***` (`PipeServiceBase.RedactSecrets`).

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
| `CaptureHelpOutput` | GUI → tray | `componentId`, `requestId`, `exePath` (`PipePayloadCodec`) | Runs `<exe> --help` with a timeout of 10 seconds. |
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

- For background messages (`CONPTY`, `ConditionalLaunch`, `CompatibilityCheck` and `Update`), the tray also adds `--create-no-window`.
- For `ShowWindow` (`Presentation`), the tray does not add `--create-no-window`, because the message opens a window.

The GUI handles `--direct:` with `CoreCommandsHandler`, then with `CommandsHandler.HandleCommand`.
The tray does not use `----ms-protocol:`. The GUI handles `----ms-protocol:` with `CommandUriMapper`, which accepts only public routes. Thus a pipe message in `----ms-protocol:`, for example `ShowWindow`, has no effect.

## Add a message

1. Add the value at the end of the enum in `Pipe/Models/PipeMessageIds.cs`.
2. For a new target, add a model in `PipeMessageModels.cs` and a case in `PipeModelConvertor.ConvertBack`.
3. Add a sender in the `PipeHelper` of the sending side.
4. Add a handler in `CommandsHandler` (tray) or in `CoreCommandsHandler` / `CDPIUI/Commands/CommandsHandler.cs` (GUI).
5. In the tray, check each path from the GUI with `TrustedPaths` before you use it.

Tests: `CDPIUI.Tests/Shared/PipeMessageModelTests.cs`, `StreamStringTests.cs`, `PipePayloadCodecTests.cs`, `PipeSessionFileTests.cs`.
