# Select the best preset

This tool selects the best preset for a component. It uses the presets that you downloaded or made.

To open this tool:

1. Open the [Utils] page of the main window.
2. Open the [Autoselection utilities] utility.
3. Select the component.
4. Select [Select best config automatically].

### The utility window

The window has three areas:

- **Settings.** Here you set the selection settings before the test.
- **Results.** Here you see the selection results after the test.
- **Output.** Here you see the progress of the current selection.

### Test settings

Before you start the selection, make sure that these parameters are correct:

- **Component.** Make sure that you selected the correct component.
- **Test type.** Select the test type. [Standard tests (HTTP/ping)] is faster, but it does not find some types of blocks. [DPI checkers (TCP 16-20 freeze)] is slower. It freezes each request to find a block accurately.
- **Configs to test.** Select the presets for the test. If you are not sure, keep [All configs].

For the standard tests, also set the list of sites. To open the list, click [Test sites list] at the bottom of the settings area. The DPI test does not use this list. It downloads its list of sites from the internet.

The list has this format:

```
SITE_NAME = Site_URL
```

The URL can have one of two formats:

- **Standard.** The URL starts with `http://` or `https://`. The test does a ping and an HTTP or HTTPS connection check.
- **Ping only.** The URL starts with `PING:`. The test does only a ping.

The name is the display name of the site in the log file and during the selection. It does not change the selection. You can use any name. Use only English letters in the name.

### Start the test

1. Click [Start test].
2. Wait until the test is complete.

The selection can take from some minutes to one hour. The time depends on your provider and on the number of presets in the test.

You can use your computer during the selection. Applications that use the network can show errors.

To stop the selection before it is complete, click [Stop test]. The results area shows the data for the presets that the test examined.

### Results

When the selection is complete, examine the results. Make sure that the recommended preset is applicable to you. Then apply it.

### What do I do if no preset operates?

- Open the [Troubleshooting] utility on the [Utils] page of the main window. Select [None of the configs work for me]. Wait until the check is complete. Do the recommended steps and do the test again.
- Download new preset sets from the Store.
- Speak to the technical support.
