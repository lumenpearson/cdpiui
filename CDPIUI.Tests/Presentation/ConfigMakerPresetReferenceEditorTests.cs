using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.ViewModels;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerPresetReferenceEditorTests
{
    [Fact]
    public void AttachingFilesRewritesTheCommandAndEveryVariableValue()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string hosts = fixture.CreateFile("hosts.txt");
        var variable = new ConfigMakerVariableDefinition
        {
            Value = hosts,
            OnValue = $"--hostlist=\"{hosts}\"",
            OffValue = hosts,
        };
        variable.Values.Add(hosts);
        fixture.Variables.Add(variable);

        string command = fixture.CreateEditor().AttachExistingFiles($"--hostlist=\"{hosts}\"");

        Assert.Equal("--hostlist=\"preset://hosts.txt\"", command);
        Assert.Equal("preset://hosts.txt", variable.Value);
        Assert.Equal("--hostlist=\"preset://hosts.txt\"", variable.OnValue);
        Assert.Equal("preset://hosts.txt", variable.OffValue);
        Assert.Equal("preset://hosts.txt", Assert.Single(variable.Values));
        Assert.Single(fixture.Resources);
    }

    [Fact]
    public void ReattachingAnAlreadyAttachedCommandDoesNotCreateResources()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string file = fixture.CreateFile("hosts.txt");
        var editor = fixture.CreateEditor();
        string once = editor.AttachExistingFiles($"--hostlist=\"{file}\"");

        string twice = editor.AttachExistingFiles(once);

        Assert.Equal(once, twice);
        Assert.Single(fixture.Resources);
    }

    [Fact]
    public void MissingReferencesRetainTheOriginalText()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string original = $"--hostlist=\"{fixture.PathFor("missing.txt")}\"";

        Assert.Equal(original, fixture.CreateEditor().AttachExistingFiles(original));
        Assert.Empty(fixture.Resources);
    }

    [Fact]
    public void ReplacingAFilePreservesBlobNameOffsetAndLuaFileMarker()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string blob = fixture.CreateFile("old.bin");
        string lua = fixture.CreateFile("old.lua");
        string replacementBlob = fixture.CreateFile("new.bin");
        string replacementLua = fixture.CreateFile("new.lua");
        var editor = fixture.CreateEditor();
        string command = $"--blob=tls:+4@\"{blob}\" --lua-init=@\"{lua}\"";

        var first = editor.ReplaceReferences(() => command, new("old.bin", blob, "", ConfigMakerPresetFileKind.Payload), replacementBlob);
        var second = editor.ReplaceReferences(() => first!.UpdatedCommandText,
            new("old.lua", lua, "", ConfigMakerPresetFileKind.Library), replacementLua);

        Assert.True(first.CommandChanged);
        Assert.True(second!.CommandChanged);
        Assert.Equal("--blob=tls:+4@preset://new.bin --lua-init=@preset://new.lua", second.UpdatedCommandText);
    }

    [Fact]
    public void VariableOnlyReplacementReturnsUnchangedCommandAndKeepsTheResource()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string original = fixture.CreateFile("old.txt");
        string replacement = fixture.CreateFile("new.txt");
        var variable = new ConfigMakerVariableDefinition { Value = original, OnValue = original, OffValue = original };
        variable.Values.Add(original);
        fixture.Variables.Add(variable);

        var result = fixture.CreateEditor().ReplaceReferences(() => "--debug=1", new("old", original, ""), replacement);

        Assert.NotNull(result);
        Assert.False(result.CommandChanged);
        Assert.Equal("--debug=1", result.UpdatedCommandText);
        Assert.Equal("preset://new.txt", variable.Value);
        Assert.Equal("preset://new.txt", variable.OnValue);
        Assert.Equal("preset://new.txt", variable.OffValue);
        Assert.Equal("preset://new.txt", Assert.Single(variable.Values));
        Assert.Single(fixture.Resources);
    }

    [Fact]
    public void NoMatchRemovesOnlyTheNewlyCreatedResource()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string replacement = fixture.CreateFile("new.txt");

        var result = fixture.CreateEditor().ReplaceReferences(() => "--debug=1", new("old", "unused.txt", ""), replacement);

        Assert.Null(result);
        Assert.Empty(fixture.Resources);
    }

    [Fact]
    public void NoMatchRetainsAnAlreadyAttachedReplacement()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string replacement = fixture.CreateFile("new.txt");
        var existing = fixture.Registry.GetOrCreate(replacement, ConfigMakerPresetFileKind.SiteList, out _);

        var result = fixture.CreateEditor().ReplaceReferences(() => "--debug=1", new("old", "unused.txt", ""), replacement);

        Assert.Null(result);
        Assert.Same(existing, Assert.Single(fixture.Resources));
    }

    [Fact]
    public void ReplacementReadsCommandAfterResourceAddedNotifications()
    {
        using var fixture = new ConfigMakerResourceFixture();
        string original = fixture.CreateFile("old.txt");
        string replacement = fixture.CreateFile("new.txt");
        string command = "--debug=1";
        var resources = new System.Collections.ObjectModel.ObservableCollection<ConfigMakerPresetResource>();
        resources.CollectionChanged += (_, _) => command = $"--hostlist=\"{original}\"";
        var registry = new CDPIUI.Helper.CreateConfigHelper.ConfigMakerPresetResourceRegistry(resources,
            path => path, _ => false);
        var editor = new CDPIUI.Helper.CreateConfigHelper.ConfigMakerPresetReferenceEditor([], registry, _ => [], _ => null!);

        var result = editor.ReplaceReferences(() => command, new("old", original, ""), replacement);

        Assert.NotNull(result);
        Assert.Equal("--hostlist=\"preset://new.txt\"", result.UpdatedCommandText);
    }
}
