# If the result is not applicable

### The utility found no applicable preset

1. Examine the spelling of the address. Make sure that the site is really unavailable without Zapret2.
2. Use the Balanced mode. If necessary, use the Exhaustive mode.
3. If you selected [One strategy for the whole list], try [Select for every site].
4. Make sure that you selected the correct HTTPS and IP options.
5. Do the scan again on a stable connection. Do not change your VPN, proxy or DNS.

### The utility made only a preliminary preset

This is the best strategy that the utility found. But it did not pass the full threshold for all targets.

1. Open [Diagnostic information].
2. Find the sites that failed.
3. Test these sites manually.

You can save this preset for more work. But it is not fully verified.

If the report has other successful strategies, [build a preset manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

### The automatic check is successful, but the site does not operate in a browser

Add the addresses that the site uses for sign-in, images, video, APIs or content delivery. Sometimes the main page is available, but one of the related addresses is blocked.

Also examine IPv4 and IPv6. The browser can use a different IP version than the utility.

### The utility skipped a site because of DNS

The utility cannot get a network address for the domain.

1. Examine the spelling.
2. Try to open the domain without Zapret2.
3. If other applications also cannot find the domain, correct the connection or the DNS settings.
4. Do the scan again.

### An old report does not have a site list

1. On the preset review page, find the file with a warning.
2. Select [Replace].
3. Select the current copy of the list.

If the list changed, a new scan gives a more reliable result.

### The scan stopped or had an error

Open [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports). A partial report can contain useful strategies. When you ask for support, save a JSON report.

CAUTION: The report can contain the tested addresses and the paths to your files. Examine the report before you send it.

### For advanced users

An automatic request tests one specific address, protocol and IP version. It does not do all the operations of a browser. It does not test JavaScript or all additional page resources. A browser can also use different secure connection parameters.

The utility is mainly for HTTP and HTTPS sites. It cannot test Discord voice connections or other UDP traffic of applications.

Back to the start: [Preset selection for Zapret2](cdpiui://Help/Zapret2PresetSelection/1AboutPresetSelection/).
