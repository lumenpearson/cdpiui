# Preset selection for Zapret2

This utility helps you make a Zapret2 preset for the sites that you need. A preset is a saved set of settings that the component uses at launch. Some buttons in this utility use the word "config". A config is the same as a preset.

The utility automatically does these steps:

1. It checks if the selected sites operate without Zapret2.
2. It tests different strategies. A strategy is a set of Zapret2 settings.
3. It selects the most reliable strategies and combines them into one preset.
4. It tests the full preset. If possible, it replaces the strategies that cause conflicts.
5. It saves a report. You can open the result again later.

[Start a new scan](cdpiui://Tools/AutoConfig/Zapret2/)

[Show previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports)

### When is this utility useful?

- The necessary sites do not operate with your installed presets.
- You want one preset for more than one site or site list.
- You want an automatic test of different settings. You do not want to try each setting manually.

You must install the Zapret2 component in CDPI UI before you start a scan.

### Important information

- The utility tests only the addresses that you add. A service can use separate addresses for images, video, sign-in or its API. Then you possibly must add these addresses too.
- A successful automatic check does not guarantee that the full page or application operates. After the scan, always test the preset in your usual browser or application.
- During the scan, CDPI UI stops the active network components. The internet connection on this computer can be unavailable until the scan stops.
- A stopped scan or a partially successful scan usually gives a report with the results that it collected.

If the recommended preset does not operate, read [Build a preset manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

Next: [Prepare for a scan](cdpiui://Help/Zapret2PresetSelection/2ChooseSitesAndMode/).
