# Build a preset manually

With the manual builder, you select strategies from a scan report yourself. Use it in these conditions:

- The recommended preset does not operate in a browser.
- The scan gave only a partial result.
- You want to replace one strategy.

The builder does not scan the full catalog again. It uses the results from the scan. But it can test one strategy again.

### When must I use the manual builder?

- The report has successful strategies, but the full automatic preset does not operate.
- Different sites in one list need different strategies.
- The automatic strategy is not stable, or you do not want to use it.
- You want to keep only the strategies that you tested manually.

If no strategy passed for the necessary site, the manual builder cannot help. [Do a new scan](cdpiui://Tools/AutoConfig/Zapret2/) with a different mode or site list.

### Open the builder

1. Open the result of the completed scan. Or select it in [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).
2. In [How do you want to continue?], select [Manual builder].
3. If this dialog is closed, open [Preset builder] at the top of the result window.

At the start, the builder contains the automatically selected strategies. You can use them as a base or change them. To start with an empty preset, select [Remove all].

Test each strategy that you add. Use the site and the connection type for which you will use the strategy.

Next: [Select strategies](cdpiui://Help/Zapret2ManualPresetBuilding/2ChooseManualStrategies/).
