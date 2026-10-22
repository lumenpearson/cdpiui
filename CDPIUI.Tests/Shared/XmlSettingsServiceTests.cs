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
    public void DateTimeWithGroupPathIsReadable()
    {
        var value = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue(["ADDONS", "ASGKOI001"], "lastRun", value);

        Assert.Equal(value, new XMLSettingsService(SettingsPath).GetValue<DateTime>(["ADDONS", "ASGKOI001"], "lastRun"));
    }
}
