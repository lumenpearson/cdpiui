using CDPIUI.Core.ComponentServices.Configuration;

namespace CDPIUI.Tests.Core;

public class PresetTargetCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cdpiui-presets-{Guid.NewGuid():N}");

    public PresetTargetCacheTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string Preset(string target, string name) =>
        $$"""{"target":["{{target}}"],"name":"{{name}}","startup_string":"--x"}""";

    [Fact]
    public void MatchingPresetIsReadAndOtherTargetIsSkipped()
    {
        string file = Path.Combine(_directory, "a.json");
        File.WriteAllText(file, Preset("CSGIVS036", "One"));

        Assert.Null(ConfigurationService.LoadPresetForTarget(file, "CSBIHA024", out _));
        Assert.Equal("One", ConfigurationService.LoadPresetForTarget(file, "CSGIVS036", out _)?.name);
    }

    [Fact]
    public void EachCallGivesItsOwnItem()
    {
        string file = Path.Combine(_directory, "a.json");
        File.WriteAllText(file, Preset("CSGIVS036", "One"));

        var first = ConfigurationService.LoadPresetForTarget(file, "CSGIVS036", out _);
        first!.name = "Changed";

        Assert.Equal("One", ConfigurationService.LoadPresetForTarget(file, "CSGIVS036", out _)?.name);
    }

    [Fact]
    public void ChangedFileIsReadAgain()
    {
        string file = Path.Combine(_directory, "a.json");
        File.WriteAllText(file, Preset("CSGIVS036", "One"));
        File.SetLastWriteTimeUtc(file, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Null(ConfigurationService.LoadPresetForTarget(file, "CSBIHA024", out _));

        File.WriteAllText(file, Preset("CSBIHA024", "Two, longer"));
        File.SetLastWriteTimeUtc(file, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Two, longer", ConfigurationService.LoadPresetForTarget(file, "CSBIHA024", out _)?.name);
    }

    [Fact]
    public void KnownFileOfOtherTargetIsNotParsedAgain()
    {
        string file = Path.Combine(_directory, "a.json");
        string text = Preset("CSGIVS036", "One");
        DateTime stamp = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(file, text);
        File.SetLastWriteTimeUtc(file, stamp);
        Assert.Null(ConfigurationService.LoadPresetForTarget(file, "CSBIHA024", out _));

        // Same size and write time, but not JSON: only a parse would notice it.
        File.WriteAllText(file, new string('x', text.Length));
        File.SetLastWriteTimeUtc(file, stamp);

        Assert.Null(ConfigurationService.LoadPresetForTarget(file, "CSBIHA024", out _));
        Assert.ThrowsAny<Exception>(() => ConfigurationService.LoadPresetForTarget(file, "CSGIVS036", out _));
    }

    [Fact]
    public void LegacyZapretPresetMatchesZapret2()
    {
        string file = Path.Combine(_directory, "a.json");
        File.WriteAllText(file, Preset("CSZTBN012", "Old"));

        Assert.NotNull(ConfigurationService.LoadPresetForTarget(file, "CSZTBN012", out bool legacyForZapret));
        Assert.False(legacyForZapret);
        Assert.NotNull(ConfigurationService.LoadPresetForTarget(file, "CSZTBN062", out bool legacyForZapret2));
        Assert.True(legacyForZapret2);
    }
}
