# Results and preset assembly

When the scan is complete, select [View result]. To open an old report, use [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).

When the result window opens, it asks [How do you want to continue?]:

- Select [Automatically suggested] to open the review of the recommended preset immediately.
- Select [Manual builder] to examine the strategies and assemble the preset.

The result window has three pages: [Selection result], [Preset builder] and [Diagnostic information].

### Selection result

This page shows the result of each strategy for each site and connection type.

- **Result**, for example `5/5 (100%)`, means that all five attempts were successful.
- **Response**, for example `45 ms`, shows the response time. Compare the success rate first and the speed second.
- **Without Zapret 2** shows the result of the usual connection. If a site is always available without bypass settings, it does not need a strategy.
- A warning icon means that the row passed. But the strategy is not confirmed for all related connection types of the site.

Use search, filters and sort to hide unnecessary rows. A good start is to show the successful results and select [Best results first].

### Preset builder

CDPI UI adds the automatically selected strategies to the builder. If the result is successful and has no warnings, usually you can keep the list. Then select [Proceed to testing and saving the config].

The sequence is important. A specific strategy for one site must have priority over a general strategy. The warnings panel shows overlaps, partial coverage and other possible problems.

### For advanced users

You can use the selected result row to do these steps:

- Repeat the automatic request test.
- Start the strategy temporarily for a manual test in the browser.
- Add the strategy for the current site or for a related site list.
- Copy the displayed actions or the full test strategy.

The manual assembly is useful when the recommended strategy fails a real test. Do not select a strategy because of one successful attempt. Compare the repeatability for all necessary connection types.

For a detailed procedure, see [Build a preset manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

### Diagnostic information

This page shows the scan problems and the full list of network attempts. It is useful when you look for the cause of a failure or ask for support. The short problem codes are technical identifiers. Use the explanation next to each code.

Next: [Test and save the preset](cdpiui://Help/Zapret2PresetSelection/5SavePreset/).
