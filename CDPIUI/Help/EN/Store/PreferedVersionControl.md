# Preferred version control

From version 0.3.0.0, you can change the preferred server for item updates and application updates in the Store.

### What is this for?

The application regularly connects to the server to get this information:

- The availability of application updates.
- The availability of updates for items from the Store.
- The latest version of the Store database.

**Note.** You can fully disable the update check. Open [Settings] and clear the related check boxes in the [Notification settings] group.

The Store also uses the server to download new items to your device.

### What does a change of the preferred server do?

The application checks for updates and downloads updates from a different server. Some items can be on external servers. The application settings do not change this.

### When must I change the preferred server?

Change the preferred server in these conditions:

- The download speed from the current server is very slow.
- The application cannot connect to the current server. For example, the Store database update fails with an SSL error. Or the check for application updates fails with an `UNEXPECTED_STATUS_CODE` error.
- Network errors (`HTTP_REQUEST_EXCEPTION` and similar) occur each time you download an item. Do not change the server for write errors (`IO_GENERIC` and similar).

If the Store and the application updates operate correctly, do not change the preferred server. *"If it works, do not touch it!"*

### How do I change the preferred server?

1. Open the Component Store. Use the [Utils] page of the main window.
2. Open the [Settings] page of the Store.
3. Set [Preferred version control type] to the necessary value.
4. Restart the application. The change starts to operate only after the restart.

### Which version controls are available?

At this time, the Store has repositories on public GitHub and GitLab servers. The number of servers can change in the future.

| Version control | Description |
| --------------- | ----------- |
| **GitHub** | The default repository. It links to the original repositories on GitHub. Some users cannot connect to it. |
| **GitLab** | The backup repository. It links to backup copies of the original repositories on GitLab. The application developer keeps the backup copies. The authors of the items do not control them. |

The official Telegram channel of the application developer has the latest information about the servers and their addresses.
