# Start and monitor the scan

1. On [Review settings], select [Start selection].
2. Do not change your internet connection, VPN, proxy or DNS until the scan is complete.

A network change during the scan can make the results inconsistent.

CDPIUI then does these steps:

1. It checks the access to the sites without bypass settings.
2. It starts different Zapret2 strategies and repeats the requests.
3. It combines the successful strategies into one preset.
4. It tests the full preset.
5. If necessary, it tries to replace a strategy that causes a conflict.

To see the current stage and the estimated remaining time, open [Details]. At the start, the estimate can be unavailable. The utility calculates it from the completed checks. The estimate becomes more accurate during the scan.

### During the scan

- CDPIUI stops its other network components.
- The internet connection can be intermittent or unavailable.
- Do not start a different bypass component manually. It can change the results.
- You can keep the window in the background.

### Stop the scan

1. Select [Stop].
2. Confirm the stop.

CDPIUI stops the temporary Zapret2 process. It starts again the components that operated before the scan.

The completed checks stay available. If the utility can make a report, you can open it immediately. You can also open it later in [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).

### Possible results

| Result | Meaning |
| ------ | ------- |
| **Config selected successfully** | The utility found and tested a common strategy. |
| **Preliminary config assembled** | The utility selected the best available strategy. Some targets possibly did not pass. |
| **Scan stopped** | The report contains only the data from before the stop. |
| **Error** | Open the result and the diagnostic information. The previous successful checks can still be available. |

Next: [Results and preset assembly](cdpiui://Help/Zapret2PresetSelection/4ResultsAndPreset/).
