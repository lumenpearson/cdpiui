using System.Globalization;
using CDPIUI.Shared.Basic;

namespace CDPIUI.Tests.Shared;

public class XmlSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-settings-" + Guid.NewGuid().ToString("N"));
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    private string SettingsPath => Path.Combine(_directory, "Settings.xml");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public void DoubleRoundTripsAcrossCultures()
    {
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        new XMLSettingsService(SettingsPath).SetValue("PSEUDOCONSOLE", "fontSize", 12.5);

        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        Assert.Equal(12.5, new XMLSettingsService(SettingsPath).GetValue<double>("PSEUDOCONSOLE", "fontSize"));
    }

    [Fact]
    public void DateTimeRoundTripsAcrossCultures()
    {
        var value = new DateTime(2026, 5, 10, 13, 45, 0, DateTimeKind.Utc);

        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        new XMLSettingsService(SettingsPath).SetValue("STORE", "lastSyncTime", value);

        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        Assert.Equal(value, new XMLSettingsService(SettingsPath).GetValue<DateTime>("STORE", "lastSyncTime"));
    }

    [Fact]
    public void ReadsDoubleWrittenInCurrentCultureByOlderVersions()
    {
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        Assert.True(XMLSettingsService.TryParseDouble("12,5", out double value));
        Assert.Equal(12.5, value);
    }

    [Fact]
    public void ValueOfAnotherTypeReturnsDefault()
    {
        new XMLSettingsService(SettingsPath).SetValue("SYSTEM", "someNumber", "not a number");

        var settings = new XMLSettingsService(SettingsPath);
        Assert.Equal(0, settings.GetValue<int>("SYSTEM", "someNumber"));
        Assert.Equal(0, settings.GetValue<int>(["SYSTEM"], "someNumber"));
    }

    [Fact]
    public void DateTimeWithGroupPathIsReadable()
    {
        var value = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue(["ADDONS", "ASGKOI001"], "lastRun", value);

        Assert.Equal(value, new XMLSettingsService(SettingsPath).GetValue<DateTime>(["ADDONS", "ASGKOI001"], "lastRun"));
    }

    [Fact]
    public async Task ConcurrentReadsAndWritesDoNotThrow()
    {
        var settings = new XMLSettingsService(SettingsPath);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                settings.SetValue("GROUP" + worker, "key" + i, i);
                settings.GetValue<int>("GROUP" + ((worker + 1) % 8), "key" + i);
            }
        })));

        Assert.Equal(49, new XMLSettingsService(SettingsPath).GetValue<int>("GROUP3", "key49"));
    }

    [Fact]
    public void ProcessWithOlderCopyDoesNotOverwriteOtherChanges()
    {
        var ui = new XMLSettingsService(SettingsPath);
        var tray = new XMLSettingsService(SettingsPath);

        ui.SetValue("APPEARANCE", "Theme", "Dark");
        tray.SetValue("TRAY", "iconGUID", "guid");
        ui.SetValue(["CONFIGS", "CSZTBN012"], "configFile", "preset.json");

        var reloaded = new XMLSettingsService(SettingsPath);
        Assert.Equal("Dark", reloaded.GetValue<string>("APPEARANCE", "Theme"));
        Assert.Equal("guid", reloaded.GetValue<string>("TRAY", "iconGUID"));
        Assert.Equal("preset.json", reloaded.GetValue<string>(["CONFIGS", "CSZTBN012"], "configFile"));
        Assert.False(File.Exists(SettingsPath + ".lock"));
    }

    [Fact]
    public void SaveIsSkippedWhileTheOtherProcessHoldsTheLock()
    {
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue("APPEARANCE", "Theme", "Dark");

        using (new FileStream(SettingsPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            settings.SetValue("APPEARANCE", "Theme", "Light");

        Assert.Equal("Dark", new XMLSettingsService(SettingsPath).GetValue<string>("APPEARANCE", "Theme"));
    }

    [Fact]
    public void ListItemsWithSeparatorsRoundTrip()
    {
        List<string> items = [@"C:\Lists\a;b.txt", "100%.txt", "%3B literal", ""];
        new XMLSettingsService(SettingsPath).SetValue<List<string>>("USERCHOISES", "siteLists", items);

        Assert.Equal(items, new XMLSettingsService(SettingsPath).GetValue<List<string>>("USERCHOISES", "siteLists"));
    }

    [Fact]
    public void ListsWrittenByOlderVersionsAreRead()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath,
            "<Settings><Group Name=\"USERCHOISES\"><Setting Key=\"siteLists\" Type=\"List\" Value=\"C:\\x\\100%\\a.txt;b.txt\" /></Group></Settings>");

        Assert.Equal([@"C:\x\100%\a.txt", "b.txt"], new XMLSettingsService(SettingsPath).GetValue<List<string>>("USERCHOISES", "siteLists"));
    }
}
