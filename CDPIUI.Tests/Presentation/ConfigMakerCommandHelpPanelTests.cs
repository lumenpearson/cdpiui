using System.Collections.Specialized;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class ConfigMakerCommandHelpPanelTests
{
    [Fact]
    public void ModulesRetainFirstAppearanceOrderAndIncludeGeneralOnlyWhenNeeded()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--b", "Beta"), Option("--a", "Alpha"), Option("--other", "beta"), Option("--general")] };

        panel.RebuildCommandModules();

        Assert.Equal(["All", "General", "Beta", "Alpha"], panel.CommandModules.Select(module => module.DisplayName));
        Assert.True(panel.HasCommandModules);
        Assert.True(((ConfigMakerCommandModuleViewModel)panel.SelectedCommandModule!).IsAll);
        Assert.Equal(["--a", "--b", "--general", "--other"], panel.CommandOptions.Select(option => option.DisplayName));
    }

    [Fact]
    public void UnnamedOnlyHelpKeepsOneAllModuleAndHidesTheModuleSelector()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--one"), Option("--two")] };

        panel.RebuildCommandModules();

        Assert.False(panel.HasCommandModules);
        Assert.True(Assert.Single(panel.CommandModules).IsAll);
        Assert.Equal(2, panel.CommandOptions.Count);
    }

    [Fact]
    public void ReloadRestoresThePreviousGroupIgnoringCase()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--before", "Old")] };
        panel.RebuildCommandModules();
        panel.SelectedCommandModule = panel.CommandModules.Single(module => !module.IsAll);
        panel.Document = new() { Options = [Option("--after", "OLD"), Option("--other", "Other")] };

        panel.RebuildCommandModules();

        Assert.Equal("OLD", ((ConfigMakerCommandModuleViewModel)panel.SelectedCommandModule!).GroupName);
        Assert.Equal("--after", Assert.Single(panel.CommandOptions).DisplayName);
    }

    [Fact]
    public void RemovedPreviousGroupFallsBackToAll()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--old", "Old")] };
        panel.RebuildCommandModules();
        panel.SelectedCommandModule = panel.CommandModules[1];
        panel.Document = new() { Options = [Option("--new", "New")] };

        panel.RebuildCommandModules();

        Assert.True(((ConfigMakerCommandModuleViewModel)panel.SelectedCommandModule!).IsAll);
        Assert.Equal("--new", Assert.Single(panel.CommandOptions).DisplayName);
    }

    [Theory]
    [InlineData(" FLAG ", "--flag")]
    [InlineData("SYNTAX-TOKEN", "--syntax")]
    [InlineData("description-token", "--description")]
    public void SearchTrimsAndMatchesNameSyntaxOrDescription(string query, string expected)
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--flag"), Option("--syntax", syntax: "syntax-token"), Option("--description", description: "DESCRIPTION-token")] };

        panel.CommandSearchText = query;

        Assert.Equal(expected, Assert.Single(panel.CommandOptions).DisplayName);
    }

    [Fact]
    public void SearchAndModuleFiltersAreCombined()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--flag-one", "First"), Option("--flag-two", "Second")] };
        panel.RebuildCommandModules();
        panel.SelectedCommandModule = panel.CommandModules[1];

        panel.CommandSearchText = "flag";

        Assert.Equal("--flag-one", Assert.Single(panel.CommandOptions).DisplayName);
    }

    [Fact]
    public void MissingDescriptionGetsLocalizedWithoutChangingTheSource()
    {
        var panel = CreatePanel();
        var option = Option("--flag");
        panel.Document = new() { Options = [option] };

        panel.RebuildCommandOptions();

        var result = Assert.Single(panel.CommandOptions);
        Assert.Same(option, result.Source);
        Assert.Equal("No description", result.Description);
        Assert.Empty(option.Description);
    }

    [Fact]
    public void SelectingACommandNotifiesArgumentFieldsBeforeSelectionProperties()
    {
        var panel = CreatePanel();
        panel.CommandArgumentText = "old";
        List<string?> notifications = [];
        panel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        panel.SelectCommand(new() { Source = Option("--port", placeholder: "<port>", required: true), Description = "Port" });

        Assert.Equal(["CommandArgumentText", "CommandArgumentPlaceholder", "CommandArgumentLabel", "SelectedCommandName",
            "SelectedCommandSyntax", "SelectedCommandDescription", "HasCommandArgument", "HasSelectedCommand", "CanInsertCommand"], notifications);
        Assert.Equal("Required: <port>", panel.CommandArgumentLabel);
        Assert.Empty(panel.CommandArgumentText);
        Assert.True(panel.HasCommandArgument);
    }

    [Fact]
    public void SelectingNullRetainsArgumentFieldsButClearsSelectionProperties()
    {
        var panel = CreatePanel();
        panel.SelectCommand(new() { Source = Option("--port", placeholder: "<port>") });
        panel.CommandArgumentText = "1234";

        panel.SelectCommand(null);

        Assert.False(panel.HasSelectedCommand);
        Assert.False(panel.CanInsertCommand);
        Assert.False(panel.HasCommandArgument);
        Assert.Empty(panel.SelectedCommandName);
        Assert.Equal("1234", panel.CommandArgumentText);
        Assert.Equal("<port>", panel.CommandArgumentPlaceholder);
        Assert.Equal("Optional: <port>", panel.CommandArgumentLabel);
    }

    [Theory]
    [InlineData(true, "", null)]
    [InlineData(false, "", "--port")]
    [InlineData(true, " 1234 ", "--port=1234")]
    public void SelectedCommandRetainsRequiredOptionalAndEqualsArgumentRules(bool required, string value, string? expected)
    {
        var panel = CreatePanel();
        panel.SelectCommand(new() { Source = Option("--port", syntax: "--port=<port>", placeholder: "<port>", required: required) });
        panel.CommandArgumentText = value;

        Assert.Equal(expected, panel.BuildSelectedCommand());
    }

    [Fact]
    public void InsertAvailabilityReadsTheCurrentReadonlyState()
    {
        bool readOnly = false;
        var panel = new ConfigMakerCommandHelpPanel(Localize, () => readOnly);
        panel.SelectCommand(new() { Source = Option("--flag") });

        Assert.True(panel.CanInsertCommand);
        readOnly = true;
        Assert.False(panel.CanInsertCommand);
    }

    [Fact]
    public void ReassigningSearchStillRebuildsTheSameCollection()
    {
        var panel = CreatePanel();
        var collection = panel.CommandOptions;
        int resets = 0;
        collection.CollectionChanged += (_, args) => { if (args.Action == NotifyCollectionChangedAction.Reset) resets++; };

        panel.CommandSearchText = "flag";
        panel.CommandSearchText = "flag";

        Assert.Same(collection, panel.CommandOptions);
        Assert.Equal(2, resets);
    }

    [Fact]
    public void ReentrantSelectionNotificationUsesTheNewSearchQuery()
    {
        var panel = CreatePanel();
        panel.Document = new() { Options = [Option("--first", "Group"), Option("--second", "Group")] };
        panel.RebuildCommandModules();
        panel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "SelectedCommandModule") panel.CommandSearchText = "second";
        };

        panel.SelectedCommandModule = panel.CommandModules[1];

        Assert.Equal("--second", Assert.Single(panel.CommandOptions).DisplayName);
    }

    private static ConfigMakerCommandHelpPanel CreatePanel() => new(Localize, () => false);

    private static ComponentCommandHelpOption Option(string name, string group = "", string syntax = "", string description = "", string placeholder = "", bool required = false) => new()
    {
        Names = [name], GroupName = group, Syntax = syntax, Description = description,
        ArgumentPlaceholder = placeholder, IsArgumentRequired = required,
    };

    private static string Localize(string key) => key switch
    {
        "ConfigMakerAllModules" => "All", "ConfigMakerGeneralModule" => "General",
        "ConfigMakerNoDescriptionMessage" => "No description",
        "ConfigMakerRequiredArgumentLabel" => "Required: {0}",
        "ConfigMakerOptionalArgumentLabel" => "Optional: {0}",
        _ => key,
    };
}
