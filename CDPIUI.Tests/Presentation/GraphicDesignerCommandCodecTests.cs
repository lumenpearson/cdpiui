using System.Collections.ObjectModel;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.Tests.Presentation;

public class GraphicDesignerCommandCodecTests
{
    [Theory]
    [InlineData("/Program Files/lists/hosts.txt", false, false)]
    [InlineData("/Program Files/lists/hosts.txt", true, false)]
    [InlineData("/Program Files/lists/hosts.txt", false, true)]
    [InlineData("/Program Files/lists/hosts.txt", true, true)]
    [InlineData("-team lists/hosts.txt", false, false)]
    [InlineData("-team lists/hosts.txt", true, false)]
    [InlineData("-team lists/hosts.txt", false, true)]
    [InlineData("-team lists/hosts.txt", true, true)]
    [InlineData("-team lists=2026/hosts.txt", false, false)]
    [InlineData("-team lists=2026/hosts.txt", true, false)]
    public void QuotedPathPrefixesKeepTheirSingleValueAcrossRepeatedDesignerVisits(string path, bool inline, bool known)
    {
        ObservableCollection<GraphicDesignerSettingItemModel> items = known ? [Item("--blacklist", "file_path")] : [];
        string command = "--blacklist" + (inline ? "=" : " ") + "\"" + path + "\"";
        string expected = "--blacklist" + (known ? "=" : " ") + "\"" + path + "\" ";

        for (int visit = 0; visit < 3; visit++)
        {
            string additional = GraphicDesignerCommandCodec.Parse(items, [], command);
            command = GraphicDesignerCommandCodec.Build(items, [], additional);
            Assert.Equal(expected, command);
        }
    }

    [Theory]
    [InlineData("--port=1443", "--port", "integer", 0)]
    [InlineData("--port=1443", "--port", "integer", 1)]
    [InlineData("--port=1443", "--port", "integer", 2)]
    [InlineData("/port=1443", "/port", "integer", 0)]
    [InlineData("/port=1443", "/port", "integer", 1)]
    [InlineData("/port=1443", "/port", "integer", 2)]
    [InlineData("-p", "-p", "flag", 0)]
    [InlineData("-p", "-p", "flag", 1)]
    [InlineData("-p", "-p", "flag", 2)]
    public void AnUnknownInlineFilenameNeverTurnsIntoAKnownOptionOnLaterVisits(string path, string option, string type, int syntax)
    {
        var other = Item(option, type);
        ObservableCollection<GraphicDesignerSettingItemModel> items = [other];
        string value = syntax == 0 ? "\"" + path + "\"" : path;
        string expected = "--blacklist=" + value + " ";
        string command = syntax == 1 ? "\"--blacklist=" + path + "\"" : "--blacklist=" + value;

        for (int visit = 0; visit < 3; visit++)
        {
            string additional = GraphicDesignerCommandCodec.Parse(items, [], command);
            command = GraphicDesignerCommandCodec.Build(items, [], additional);
            Assert.Equal(expected, command);
            Assert.False(other.IsChecked);
            Assert.Equal("", other.Value);
        }
    }

    [Theory]
    [InlineData("--port=1443", "--port", "integer", "1443", "-p --port=1443 ")]
    [InlineData("--file=/Program Files/lists/hosts.txt", "--file", "file_path", "\"/Program Files/lists/hosts.txt\"", "-p --file=\"/Program Files/lists/hosts.txt\" ")]
    public void AWholeQuotedRealOptionAfterABareFlagStillHasItsOwnModel(string argument, string name, string type, string value, string expected)
    {
        var flag = Item("-p");
        var option = Item(name, type);
        ObservableCollection<GraphicDesignerSettingItemModel> items = [flag, option];
        string command = "-p \"" + argument + "\"";

        for (int visit = 0; visit < 3; visit++)
        {
            string additional = GraphicDesignerCommandCodec.Parse(items, [], command);
            command = GraphicDesignerCommandCodec.Build(items, [], additional);
            Assert.Equal(expected, command);
            Assert.True(flag.IsChecked);
            Assert.True(option.IsChecked);
            Assert.Equal(value, option.Value);
        }
    }

    [Theory]
    [InlineData("--port", "integer", "--port=\"1443\"")]
    [InlineData("--file", "file_path", "--file=\"old.txt\"")]
    public void ClearingAParsedQuotedValueRetainsTheLegacyBareFlag(string name, string type, string input)
    {
        var item = Item(name, type);
        GraphicDesignerCommandCodec.Parse([item], [], input);

        item.Value = "";

        Assert.Equal(name + " ", GraphicDesignerCommandCodec.Build([item], [], ""));
    }

    [Fact]
    public void ReassigningTheSameParsedEmptyValueKeepsItsExplicitEmptyArgument()
    {
        var item = Item("--value", "string");
        GraphicDesignerCommandCodec.Parse([item], [], "--value \"\"");

        item.Value = item.Value;

        Assert.Equal("--value=\"\" ", GraphicDesignerCommandCodec.Build([item], [], ""));
    }

    [Fact]
    public void EditingAwayAndBackDoesNotRestoreAnOldGroupingHint()
    {
        var item = Item("--value", "string");
        GraphicDesignerCommandCodec.Parse([item], [], "--value \"\"");

        item.Value = "other";
        item.Value = "";

        Assert.Equal("--value ", GraphicDesignerCommandCodec.Build([item], [], ""));
    }

    [Fact]
    public void ValueNotificationsObserveTheCorrectGroupingForParsingAndExternalEdits()
    {
        var item = Item("--value", "string");
        List<string> commands = [];
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Value") commands.Add(GraphicDesignerCommandCodec.Build([item], [], ""));
        };

        GraphicDesignerCommandCodec.Parse([item], [], "--value \"alpha beta\"");
        item.Value = "";

        Assert.Equal(["--value=\"alpha beta\" ", "--value "], commands);
    }

    [Fact]
    public void AWholeQuotedOptionAfterAValuelessFlagIsNotConsumedAsTheFlagValue()
    {
        var flag = Item("-p");
        var port = Item("--port", "integer");

        string extra = GraphicDesignerCommandCodec.Parse([flag, port], [], "-p \"--port=1443\"");

        Assert.True(flag.IsChecked);
        Assert.True(port.IsChecked);
        Assert.Equal("1443", port.Value);
        Assert.Equal("-p --port=1443 ", GraphicDesignerCommandCodec.Build([flag, port], [], extra));
    }

    [Fact]
    public void DynamicFactoriesReceiveDecodedTelegramScalars()
    {
        Dictionary<string, string> values = [];

        GraphicDesignerCommandCodec.Parse([], [], "--host \"127.0.0.1\" --port \"1443\" --secret \"abcdef\"", false,
            (name, value) => { values[name] = value.Text; return true; });

        Assert.Equal("127.0.0.1", values["--host"]);
        Assert.Equal("1443", values["--port"]);
        Assert.True(int.TryParse(values["--port"], out int port));
        Assert.Equal(1443, port);
        Assert.Equal("abcdef", values["--secret"]);
    }

    [Theory]
    [InlineData("fast", "--mode=fast ")]
    [InlineData("very fast", "--mode=\"very fast\" ")]
    public void QuotedEnumValuesStillMatchTheirActualValue(string value, string expected)
    {
        var item = Item("--mode", "enum");
        item.AvailableEnumValues = [new() { DisplayName = "Choice", ActualValue = value }];

        string extra = GraphicDesignerCommandCodec.Parse([item], [], $"--mode \"{value}\"");

        Assert.Equal(value, item.Value);
        Assert.NotNull(item.AvailableEnumValues.FirstOrDefault(choice => choice.ActualValue == item.Value));
        Assert.Equal(expected, GraphicDesignerCommandCodec.Build([item], [], extra));
    }

    [Fact]
    public void UnknownInlineQuotedFileRetainsItsClosingQuoteThroughTheDesignerRoundTrip()
    {
        ObservableCollection<GraphicDesignerSettingItemModel> items = [];

        string additional = GraphicDesignerCommandCodec.Parse(items, [], "--blacklist=\"C:\\smoke\\fixture-list.txt\"");
        string result = GraphicDesignerCommandCodec.Build(items, [], additional);

        Assert.Equal("--blacklist \"C:\\smoke\\fixture-list.txt\" ", result);
    }

    [Theory]
    [InlineData("--file", "--file=\"C:\\my files\\list.txt\"", true, "--file=\"C:\\my files\\list.txt\" ")]
    [InlineData("--file", "--file \"C:\\my files\\list.txt\"", true, "--file=\"C:\\my files\\list.txt\" ")]
    [InlineData("--file", "--file=\"C:\\my files\\list.txt\"", false, "--file \"C:\\my files\\list.txt\" ")]
    [InlineData("--file", "--file \"C:\\my files\\list.txt\"", false, "--file \"C:\\my files\\list.txt\" ")]
    [InlineData("--file", "\"--file=C:\\my files\\list.txt\"", true, "--file=\"C:\\my files\\list.txt\" ")]
    [InlineData("--file", "\"--file=C:\\my files\\list.txt\"", false, "--file \"C:\\my files\\list.txt\" ")]
    [InlineData("-f", "-f=\"C:\\my files\\list.txt\"", true, "-f=\"C:\\my files\\list.txt\" ")]
    [InlineData("-f", "-f=\"C:\\my files\\list.txt\"", false, "-f \"C:\\my files\\list.txt\" ")]
    [InlineData("/file", "/file=\"C:\\my files\\list.txt\"", false, "/file \"C:\\my files\\list.txt\" ")]
    [InlineData("--value", "--value=\"a=b c=d\"", true, "--value=\"a=b c=d\" ")]
    [InlineData("--value", "--value \"a=b c=d\"", true, "--value=\"a=b c=d\" ")]
    [InlineData("--value", "--value=\"\"", true, "--value=\"\" ")]
    [InlineData("--value", "--value \"\"", false, "--value \"\" ")]
    [InlineData("--value", "\"--value=\"", true, "--value=\"\" ")]
    [InlineData("--value", "\"--value=\"", false, "--value \"\" ")]
    public void QuotedValuesSurviveTheDesignerRoundTrip(string name, string input, bool known, string expected)
    {
        ObservableCollection<GraphicDesignerSettingItemModel> items = known ? [Item(name, "string")] : [];

        string additional = GraphicDesignerCommandCodec.Parse(items, [], input);
        string result = GraphicDesignerCommandCodec.Build(items, [], additional);

        Assert.Equal(expected, result);
        Assert.Equal(expected.Trim(), ComponentCommandLineFormatter.ToSingleLine(ComponentCommandLineFormatter.FormatByFlags(result.Trim())));
    }

    [Fact]
    public void DynamicModelModeReceivesTheCompleteQuotedValue()
    {
        ObservableCollection<GraphicDesignerSettingItemModel> items = [];
        string additional = GraphicDesignerCommandCodec.Parse(items, [], "--value=\"quoted value\"", false,
            (name, value) =>
            {
                var item = Item(name, "string");
                item.IsChecked = true;
                item.SetParsedValue(value.Text, value.PreserveGrouping);
                items.Add(item);
                return true;
            });

        Assert.Equal("--value=\"quoted value\" ", GraphicDesignerCommandCodec.Build(items, [], additional));
    }

    [Fact]
    public void ExistingRepairOfAnIncompleteKnownQuotedValueIsRetained()
    {
        var item = Item("--value", "string");

        GraphicDesignerCommandCodec.Parse([item], [], "--value=\"unfinished");

        Assert.Equal("\"unfinished\"", item.Value);
    }

    [Fact]
    public void ParseSelectsKnownFlagsAndKeepsTheRestInInputOrderWithTrailingSpaces()
    {
        var flag = Item("-p");
        var text = Item("--port", "integer");
        ObservableCollection<GraphicDesignerSettingItemModel> items = [flag, text];

        string extra = GraphicDesignerCommandCodec.Parse(items, [], "-p --unknown=value --port 443 --other raw");

        Assert.True(flag.IsChecked);
        Assert.Equal("", flag.Value);
        Assert.True(text.IsChecked);
        Assert.Equal("443", text.Value);
        Assert.Equal("--unknown value --other raw ", extra);
    }

    [Fact]
    public void ParseClearsChecksButRetainsUnmentionedValuesAndTheSelectedExclusiveGuid()
    {
        var ordinary = Item("--ordinary");
        var child = Item("--child", guid: "selected");
        ordinary.IsChecked = child.IsChecked = true;
        ordinary.Value = child.Value = "previous";
        var group = new GraphicDesignerExclusiveSettingItemModel { Guid = "group", SelectedItemGuid = "selected", Items = [child] };

        GraphicDesignerCommandCodec.Parse([ordinary], [group], "--unknown");

        Assert.False(ordinary.IsChecked);
        Assert.False(child.IsChecked);
        Assert.Equal("previous", ordinary.Value);
        Assert.Equal("previous", child.Value);
        Assert.Equal("selected", group.SelectedItemGuid);
    }

    [Fact]
    public void OrdinaryMatchingIsCaseSensitiveAndTheLastOccurrenceWins()
    {
        var item = Item("--value", "string");

        string extra = GraphicDesignerCommandCodec.Parse([item], [], "--value=first --value=last --VALUE=unknown");

        Assert.Equal("last", item.Value);
        Assert.Equal("--VALUE unknown ", extra);
    }

    [Fact]
    public void OrdinaryItemsTakePrecedenceOverExclusiveGroups()
    {
        var ordinary = Item("--same", "string");
        var child = Item("--same", "string", "child");
        var group = new GraphicDesignerExclusiveSettingItemModel { Items = [child], SelectedItemGuid = "old" };

        GraphicDesignerCommandCodec.Parse([ordinary], [group], "--same=value");

        Assert.True(ordinary.IsChecked);
        Assert.False(child.IsChecked);
        Assert.Equal("old", group.SelectedItemGuid);
    }

    [Fact]
    public void OnlyTheFirstMatchingExclusiveGroupIsSelected()
    {
        var first = Item("--mode", "string", "first");
        var second = Item("--mode", "string", "second");
        var group1 = new GraphicDesignerExclusiveSettingItemModel { Items = [first] };
        var group2 = new GraphicDesignerExclusiveSettingItemModel { Items = [second], SelectedItemGuid = "old" };

        Assert.Equal("", GraphicDesignerCommandCodec.Parse([], [group1, group2], "--mode=fast"));

        Assert.True(first.IsChecked);
        Assert.Equal("fast", first.Value);
        Assert.Equal("first", group1.SelectedItemGuid);
        Assert.False(second.IsChecked);
        Assert.Equal("old", group2.SelectedItemGuid);
    }

    [Fact]
    public void DynamicModelModeCallsTheFactoryInOrderEvenForExistingNames()
    {
        ObservableCollection<GraphicDesignerSettingItemModel> items = [Item("--known")];
        List<(string Name, string Value)> calls = [];

        string extra = GraphicDesignerCommandCodec.Parse(items, [], "--known=a=b --other=c --known=d", false,
            (name, value) =>
            {
                calls.Add((name, value.Text));
                if (name != "--known") return false;
                items.Add(Item(name));
                return true;
            });

        Assert.Equal(new[] { ("--known", "a=b"), ("--other", "c"), ("--known", "d") }, calls);
        Assert.Equal(3, items.Count);
        Assert.Equal("--other c ", extra);
    }

    [Fact]
    public void ExistingModelModeDoesNotCallADynamicFactory()
    {
        string extra = GraphicDesignerCommandCodec.Parse([], [], "--unknown", true,
            (_, _) => throw new InvalidOperationException("must remain lazy"));

        Assert.Equal("--unknown ", extra);
    }

    [Fact]
    public void SeparateValuesStartingWithADashOrSlashRetainTheLegacyOptionBoundary()
    {
        var amount = Item("--amount", "integer");
        var path = Item("--path", "file_path");
        var plain = Item("--plain", "string");

        string extra = GraphicDesignerCommandCodec.Parse([amount, path, plain], [], "--amount -2 --path /root --plain value");

        Assert.Equal("", amount.Value);
        Assert.Equal("", path.Value);
        Assert.Equal("value", plain.Value);
        Assert.Equal("-2 /root ", extra);
    }

    [Fact]
    public void ValuesContainingEqualsAreAlreadyPreservedAfterTheFirstSeparator()
    {
        var item = Item("--known", "string");

        string extra = GraphicDesignerCommandCodec.Parse([item], [], "--known=a=b== --unknown=c=d==");

        Assert.Equal("a=b==", item.Value);
        Assert.Equal("--unknown c=d== ", extra);
    }

    [Fact]
    public void AWholeQuotedOptionWithoutSpacesRetainsItsRecognizedName()
    {
        var item = Item("--known", "string");

        Assert.Equal("", GraphicDesignerCommandCodec.Parse([item], [], "\"--known=value\""));
        Assert.Equal("value", item.Value);
    }

    [Fact]
    public void KnownInlineQuotedValuesKeepTheirQuotePair()
    {
        var item = Item("--file", "file_path");

        Assert.Equal("", GraphicDesignerCommandCodec.Parse([item], [], "--file=\"C:\\my files\\list.txt\""));

        Assert.Equal("\"C:\\my files\\list.txt\"", item.Value);
        Assert.Equal("--file=\"C:\\my files\\list.txt\" ", GraphicDesignerCommandCodec.Build([item], [], ""));
    }

    [Theory]
    [InlineData("string", true, false, "--value=42 ")]
    [InlineData("integer", true, false, "--value=42 ")]
    [InlineData("file_path", true, false, "--value=42 ")]
    [InlineData("enum", false, false, "--value=42 ")]
    [InlineData("custom", true, false, "--value ")]
    [InlineData("custom", true, true, "--value=42 ")]
    public void BuildRetainsTypeSpecificValueAndEnumRules(string type, bool isChecked, bool textInput, string expected)
    {
        var item = Item("--value", type);
        item.IsChecked = isChecked;
        item.EnableTextInput = textInput;
        item.Value = "42";

        Assert.Equal(expected, GraphicDesignerCommandCodec.Build([item], [], ""));
    }

    [Fact]
    public void BuildUsesCollectionOrderThenExactSelectedGuidThenAdditionalTextVerbatim()
    {
        var first = Item("--first");
        var second = Item("--second", "string");
        var selected = Item("--selected", "string", "chosen");
        var differentCase = Item("--different-case", "enum", "CHOSEN");
        first.IsChecked = second.IsChecked = selected.IsChecked = true;
        second.Value = "text";
        selected.Value = "group";
        var group = new GraphicDesignerExclusiveSettingItemModel { Items = [differentCase, selected], SelectedItemGuid = "chosen" };

        Assert.Equal("--first --second=text --selected=group   extra  ",
            GraphicDesignerCommandCodec.Build([first, second], [group], "  extra  "));
    }

    [Fact]
    public void BuildSkipsUncheckedNonEnumAndAnUnselectedExclusiveGroup()
    {
        var child = Item("--child");
        child.IsChecked = true;
        var group = new GraphicDesignerExclusiveSettingItemModel { Items = [child], SelectedItemGuid = "other" };

        Assert.Equal("extra", GraphicDesignerCommandCodec.Build([Item("--unchecked")], [group], "extra"));
    }

    private static GraphicDesignerSettingItemModel Item(string name, string type = "flag", string? guid = null) => new()
    {
        Guid = guid ?? name, DisplayName = name, Type = type,
    };
}
