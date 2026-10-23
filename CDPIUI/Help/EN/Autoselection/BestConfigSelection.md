# Select the best preset

This tool selects the best preset for a component. It uses the presets that you downloaded or made.

To open this tool:

1. Open the [Utilities] page of the main window.
2. Open the [Automatic Selection Tools] utility.
3. Select [Select the best preset automatically].

### The utility window

The window has three areas:

- **Select Settings.** Here you set the selection settings before the test.
- **Results.** Here you see the selection results after the test.
- **Debug Output.** Here you see the progress of the current selection.

### Test settings

Before you start the selection, make sure that these parameters are correct:

- **Component.** Make sure that you selected the correct component.
- **Test Type.** Select the test type. "Standard" is faster, but it does not find some types of blocks. "DPI Check" is slower. It freezes each request to find a block accurately.
- **Which presets to test.** Select the list of presets for the test. If you are not sure, keep "All presets".

Also set the list of sites for the test. To open the list, click [List of sites to test] at the bottom of the selection settings area.

The list has this format:

```
SITE_NAME = Site_URL
```

The URL can have one of two formats:

- **Standard.** The URL starts with `https://`. The test does a ping and an HTTPS connection check.
- **Ping only.** The URL starts with `PING:`. The test does only a ping.

The name is the display name of the site in the log file and during the selection. It does not change the selection. You can use any name. Use only English letters in the name.

### Start the test

1. Click [Start test].
2. Wait until the test is complete.

The selection can take from some minutes to one hour. The time depends on your provider and on the number of presets in the test.

You can use your computer during the selection. Applications that use the network can show errors.

To stop the selection before it is complete, click [Stop test]. The results window shows the data for the presets that the test examined.

### Results

When the selection is complete, examine the results. Make sure that the recommended preset is applicable to you. Then apply it.

### What do I do if no preset operates?

- Open the [Troubleshooting] utility on the [Utilities] page of the main window. Select [All configs not work to me]. Wait until the check is complete. Do the recommended steps and do the test again.
- Download new preset sets from the Store.
- Speak to the technical support.
