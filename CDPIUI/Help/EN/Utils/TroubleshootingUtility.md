# Troubleshooting utility

The [Troubleshooting] utility finds frequent problems on your PC and tries to correct them.

To open the utility, use the [Utilities] page of the main window.

### Select a problem

The first page shows a list of problems. Select the problem that you have:

| Problem | What the utility does |
| ------- | --------------------- |
| [All configs not work to me] | Starts the basic system check. |
| [Component constantly crashes with an error] | Opens the help article about component exceptions. |
| [I cannot open or download items from Store] | Starts the Store check. |
| [My problem is not on the list] | Opens the page for a problem report. |

### Basic system check

Before the check, the utility stops all components. The check examines these items:

- The Base Filtering Engine service.
- The system proxy.
- The `netsh.exe` file in the PATH variable.
- The TCP timestamps setting.
- AdGuard, Killer, Intel Connectivity Network, Check Point and SmartByte services.
- VPN services.
- The system DNS service.
- The WinDivert service, other running bypass components and their services.

### Store check

Before the check, the utility stops all components. The check examines these items:

- The access to the Store repositories on GitHub and on GitLab.
- The write access to the Store directories: the internal directory, the items directory, the repository directory and the database directory.

### Results and corrections

1. Wait until the check is complete. The command prompt window can flicker during the check.
2. Examine the results table.
3. To correct the problems, select [Next].
4. Wait until the correction is complete.
5. Examine the correction results.
6. Select [Exit].

The utility cannot correct all problems automatically. It can enable TCP timestamps, stop the WinDivert service and give write access to the Store directories. For the other problems, use the help link next to the result.
