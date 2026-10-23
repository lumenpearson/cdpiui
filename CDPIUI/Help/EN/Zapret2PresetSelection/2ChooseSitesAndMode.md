# Prepare for a scan

The preparation has three steps:

1. Select the sites.
2. Select a scan mode.
3. Examine the advanced settings.

### 1. Add sites

Select [Add a site list]. Then do one of these steps:

- Select a list that is installed in CDPI UI.
- Add a TXT file.
- Drag a TXT file into the window.
- Make your own list.

In a custom list or a TXT file, put one address on each line. For example:

```text
example.com
https://example.org/video
```

The utility does not test lines that start with `#`. These lines are comments.

If you enter only a domain, the utility tests the connection types that you select below. If you enter a full address that starts with `http://` or `https://`, the test uses this scheme.

### Select the list mode

Select one option for each list:

- **One strategy for the whole list.** The utility finds one strategy that operates for all sites in the list. The preset is smaller. Use this option first for a ready-made themed list.
- **Select for every site.** Different sites can use different strategies. Use this option for a mixed list. The scan takes more time, and the preset can be larger.

If no common strategy operates for a list, do the scan again with **Select for every site**.

### 2. Select a mode

| Mode | Description |
| ---- | ----------- |
| **Quick** | A minimal set of strategies and two attempts. Use it for a short first scan. |
| **Balanced** | A larger set, three attempts and an additional test of the full preset. Use it in most conditions. |
| **Exhaustive** | The full set, five attempts and stronger stability checks. It can take much more time. |

Start with Balanced. Use Exhaustive if the result is not stable or if the utility finds no applicable strategy.

### 3. Examine the advanced settings

If you do not know the function of a setting, keep the recommended value. Or select [Restore recommended values].

Most modern sites need HTTPS checks. A check of only HTTP is not sufficient. A successful response or a redirect does not mean that the secure page opens.

- **HTTPS (automatic TLS)** is the nearest to a usual secure connection.
- **TLS 1.2** adds a test of a different frequent HTTPS option.
- **TLS 1.3 (exact)** tests only TLS 1.3.
- **QUIC / HTTP/3** tests HTTP/3 connections over QUIC. The curl build must support HTTP/3.
- **IPv4** is applicable to most connections.
- **IPv6**: enable it only if your provider gives a working IPv6 connection.

### For advanced users

| Setting | Description |
| ------- | ----------- |
| **Connection timeout** | The time to wait for a connection to the site. |
| **Total request timeout** | The maximum time for one attempt. It cannot be shorter than the connection timeout. |
| **Zapret 2 startup wait** | The pause between the start of a strategy and the request to the site. |
| **Requests per domain** | The number of times that the utility repeats each check. More attempts find unstable results better, but the scan takes much more time. |
| **Continue after finding the best strategies** | The utility tests the remaining strategies of the selected mode. The scan takes more time. This setting does not add strategies from a more exhaustive mode. |

On [Review settings], examine the number of sites, the connection types and the selected mode. The duration on this page is a maximum estimate. It is not an exact prediction.

Next: [Start and monitor the scan](cdpiui://Help/Zapret2PresetSelection/3DuringPresetSelection/).
