using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class BlockCheck2OptionsTests
{
    [Fact]
    public void DefaultsRetainBalancedTlsAndIpv4WithoutWritingSettings()
    {
        var settings = new MemorySettings();
        var options = Create(settings);

        Assert.Equal(BlockCheckRunPreset.Balanced, options.SelectedRunPreset);
        Assert.True(options.IncludeTls12);
        Assert.True(options.IncludeTlsAuto);
        Assert.True(options.IncludeIpv4);
        Assert.False(options.IncludeHttp);
        Assert.False(options.IncludeTls13);
        Assert.False(options.IncludeQuic);
        Assert.False(options.IncludeIpv6);
        Assert.Equal(5d, options.ConnectTimeoutSeconds);
        Assert.Equal(10d, options.RequestTimeoutSeconds);
        Assert.Equal(0.75d, options.StartupGraceSeconds);
        Assert.Equal(3d, options.RequestsPerDomain);
        Assert.True(options.AdvancedOptionsValid);
        Assert.Empty(settings.Writes);
    }

    [Fact]
    public void CorruptSavedValuesUseTheExistingFallbacks()
    {
        var settings = new MemorySettings();
        settings.Values["SelectedRunPreset"] = 999;
        settings.Values["ConnectTimeoutSeconds"] = double.NaN;
        settings.Values["RequestTimeoutSeconds"] = -1d;
        settings.Values["StartupGraceSeconds"] = double.PositiveInfinity;
        settings.Values["RequestsPerDomain"] = 99;

        var options = Create(settings);

        Assert.Equal(BlockCheckRunPreset.Balanced, options.SelectedRunPreset);
        Assert.Equal(5d, options.ConnectTimeoutSeconds);
        Assert.Equal(10d, options.RequestTimeoutSeconds);
        Assert.Equal(0.75d, options.StartupGraceSeconds);
        Assert.Equal(3d, options.RequestsPerDomain);
        Assert.Empty(settings.Writes);
    }

    [Fact]
    public void InvalidSavedRequestTimeoutIsAtLeastTheValidConnectTimeout()
    {
        var settings = new MemorySettings();
        settings.Values["ConnectTimeoutSeconds"] = 40d;
        settings.Values["RequestTimeoutSeconds"] = 10d;

        var options = Create(settings);

        Assert.Equal(40d, options.RequestTimeoutSeconds);
    }

    [Fact]
    public void ProtocolChangeKeepsNotificationAndSaveOrder()
    {
        var settings = new MemorySettings();
        var options = Create(settings);
        options.PropertyChanged += (_, args) => settings.Trace.Add("notify:" + args.PropertyName);

        options.IncludeHttp = true;

        Assert.Equal(["notify:IncludeHttp", "save:IncludeHttp", "notify:IsHttpOnly", "notify:HasProtocolSelection",
            "notify:CanContinueFromTargets", "notify:CanStart"], settings.Trace);
        Assert.Equal(("IncludeHttp", (object)true), Assert.Single(settings.Writes));
    }

    [Fact]
    public void AssigningTheSameOptionDoesNotWriteOrNotify()
    {
        var settings = new MemorySettings();
        var options = Create(settings);
        options.PropertyChanged += (_, args) => settings.Trace.Add("notify:" + args.PropertyName);

        options.IncludeTls12 = true;
        options.RequestsPerDomain = 3d;
        options.ConnectTimeoutSeconds = 5d;

        Assert.Empty(settings.Trace);
    }

    [Fact]
    public void PresetChangeSavesPresetBeforeUpdatingRecommendedAttempts()
    {
        var settings = new MemorySettings();
        var options = Create(settings);
        options.PropertyChanged += (_, args) => settings.Trace.Add("notify:" + args.PropertyName);

        options.SelectedRunPreset = BlockCheckRunPreset.Exhaustive;

        Assert.Equal(5d, options.RequestsPerDomain);
        Assert.Equal(["notify:SelectedRunPreset", "save:SelectedRunPreset", "notify:RequestsPerDomain",
            "notify:AdvancedOptionsValid", "notify:CanStart", "save:RequestsPerDomain"], settings.Trace);
        Assert.Equal(("SelectedRunPreset", (object)2), settings.Writes[0]);
        Assert.Equal(("RequestsPerDomain", (object)5), settings.Writes[1]);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteAdvancedValuesInvalidateTheFormWithoutPersisting(double value)
    {
        var settings = new MemorySettings();
        var options = Create(settings);

        options.ConnectTimeoutSeconds = value;

        Assert.Equal(value, options.ConnectTimeoutSeconds);
        Assert.False(options.AdvancedOptionsValid);
        Assert.Empty(settings.Writes);
    }

    [Fact]
    public void FiniteInvalidAdvancedValueRemainsVisibleAndIsSavedAsBefore()
    {
        var settings = new MemorySettings();
        var options = Create(settings);

        options.ConnectTimeoutSeconds = 0d;

        Assert.False(options.AdvancedOptionsValid);
        Assert.Equal(("ConnectTimeoutSeconds", (object)0d), Assert.Single(settings.Writes));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1.5d)]
    [InlineData(21d)]
    public void InvalidAttemptsAreNotPersisted(double value)
    {
        var settings = new MemorySettings();
        var options = Create(settings);

        options.RequestsPerDomain = value;

        Assert.False(options.AdvancedOptionsValid);
        Assert.Empty(settings.Writes);
    }

    [Fact]
    public void StartAvailabilityReadsLiveRunStateAndSiteListCount()
    {
        bool running = false;
        int selected = 0;
        var options = new BlockCheck2OptionsViewModel(new MemorySettings(), () => running, () => selected);

        Assert.False(options.CanStart);
        selected = 1;
        Assert.True(options.CanStart);
        running = true;
        Assert.False(options.CanStart);
        Assert.False(options.CanContinueFromTargets);
        running = false;
        selected = 0;
        options.TargetInput = " example.com\nEXAMPLE.COM\r\n second.example ";
        Assert.Equal(2, options.DirectTargetInputCount);
        Assert.True(options.CanStart);
    }

    [Fact]
    public void SessionRequestPreservesSelectionsAndTargetSnapshotTiming()
    {
        var options = Create(new MemorySettings());
        options.TargetInput = "before.example";
        options.IncludeHttp = true;
        options.IncludeTls12 = false;
        options.IncludeIpv6 = true;
        options.TestAllStrategies = true;
        var siteList = new BlockCheckSiteListInput("List", "list.txt");

        var request = options.CreateSessionRequest(() =>
        {
            options.TargetInput = "after.example";
            options.SelectedRunPreset = BlockCheckRunPreset.Quick;
            return new[] { siteList };
        });

        Assert.Equal("before.example", request.TargetInput);
        Assert.Equal(BlockCheckRunPreset.Quick, request.RunPreset);
        Assert.Equal(2, request.AttemptsPerTarget);
        Assert.True(request.TestAllStrategies);
        Assert.Same(siteList, Assert.Single(request.SiteLists));
        Assert.True(request.Protocols.SetEquals([BlockCheckProtocol.Http, BlockCheckProtocol.TlsAuto]));
        Assert.True(request.IpVersions.SetEquals([BlockCheckIpVersion.IPv4, BlockCheckIpVersion.IPv6]));
    }

    private static BlockCheck2OptionsViewModel Create(MemorySettings settings) => new(settings, () => false, () => 0);

    private sealed class MemorySettings : IBlockCheck2OptionsSettings
    {
        public Dictionary<string, object> Values { get; } = [];
        public List<(string Key, object Value)> Writes { get; } = [];
        public List<string> Trace { get; } = [];
        public int ReadInt(string key, int defaultValue) => Values.TryGetValue(key, out object? value) ? (int)value : defaultValue;
        public bool ReadBool(string key, bool defaultValue) => Values.TryGetValue(key, out object? value) ? (bool)value : defaultValue;
        public double ReadDouble(string key, double defaultValue) => Values.TryGetValue(key, out object? value) ? (double)value : defaultValue;
        public void Save(string? key, int value) => Record(key, value);
        public void Save(string? key, bool value) => Record(key, value);
        public void Save(string? key, double value) => Record(key, value);

        private void Record(string? key, object value)
        {
            if (key == null) throw new InvalidOperationException("Setting key is required");
            Values[key] = value;
            Writes.Add((key, value));
            Trace.Add("save:" + key);
        }
    }
}
