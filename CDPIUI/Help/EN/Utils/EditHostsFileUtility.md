# Change the HOSTS file on your PC

From version 0.5.0.0, the application has a utility that replaces the HOSTS file on your PC.

The console window can flicker when the utility operates.

### Why is this necessary?

A change of the HOSTS file can solve these problems:

1. The connection to Discord servers in Finland does not operate.
2. The GitHub servers are not available. The application needs these servers for application updates and for the Store.

For more information about the HOSTS file and its recovery in Windows, see the [Microsoft Support site](https://support.microsoft.com/en-us/topic/c2a43f9d-e176-c6f3-e4ef-3500277a6dae).

### How do I add addresses to the file?

*Note:* If the [Add addresses to the hosts file] menu item is not available, restore the file first. Then try again.

1. Open the [Hosts file utility] on the [Utilities] page of the main window.
2. Select [Add addresses to the hosts file].
3. Give administrator rights to the `EditHostFile.exe` process. The utility cannot edit the file without these rights.
4. Make sure that the status at the top of the utility window is "✅ The hosts file has been modified."

### How do I remove addresses from the file?

*Note:* If the [Remove addresses from the hosts file] menu item is not available, the hosts file has no changes.

1. Open the [Hosts file utility] on the [Utilities] page of the main window.
2. Select [Remove addresses from the hosts file].
3. Give administrator rights to the `EditHostFile.exe` process. The utility cannot edit the file without these rights.
4. Make sure that the status at the top of the utility window is "❎ The hosts file is not modified."

### How do I restore the file?

*Note:* If the [Restore original hosts file from backup] menu item is not available, the hosts file has no changes.

1. Open the [Hosts file utility] on the [Utilities] page of the main window.
2. Select [Restore original hosts file from backup].
3. Give administrator rights to the `EditHostFile.exe` process. The utility cannot restore the file without these rights.
4. Make sure that the status at the top of the utility window is "❎ The hosts file is not modified."
5. Make sure that the restore button is not available.

## Possible errors

Antivirus software can identify the `EditHostFile.exe` file as malware and delete it. Then the application shows an error such as `APPLICATION_DAMAGED_NEED_REPAIR` when you edit the hosts file.

To solve this problem:

- For the Portable version, copy the `EditHostFile.exe` file from the latest Portable release. Put it into the application directory next to `CDPIUI.exe`.
- For the MSI version, do a repair of the installation.

For manual troubleshooting, run `EditHostFile.exe` in a terminal with administrator rights. Then read the console output.

| Utility launch flag | Description |
| ------------------- | ----------- |
| /add | Add addresses to the hosts file |
| /remove | Remove addresses from the hosts file |
| /recover | Restore addresses from the hosts.bak file |

## For developers

The utility contains a hosts file from the [zapret-discord-youtube](https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts) repository. For security, a change of this file needs a new build of the utility.

The utility puts the `[CDOM-B]` tag before the added addresses and the `[CDOM-E]` tag after them. These tags make a later removal or edit easier.
