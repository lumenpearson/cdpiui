using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Tests.Core;

public class ConfigItemContentTests
{
    private static ConfigMakerVariableMetadata Variable(
        string? name, string? value = null, string? kind = null,
        string? onValue = null, string? offValue = null, bool enabled = false) => new()
        {
            name = name,
            value = value,
            kind = kind,
            onValue = onValue,
            offValue = offValue,
            isSwitchEnabled = enabled,
        };

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("no variables", "no variables")]
    [InlineData("%UNKNOWN%", "%UNKNOWN%")]
    [InlineData("%list%/a.txt", "lists/a.txt")]
    [InlineData("%LIST%-%LIST%", "lists-lists")]
    public void ExpandVariablesUsesDeclaredValuesAndIgnoresCase(string? text, string expected)
    {
        ConfigItem item = new() { variables = ["%LIST%=lists"] };

        Assert.Equal(expected, item.ExpandVariables(text));
    }

    [Fact]
    public void ExpandVariablesAppliesSourcesInOrder()
    {
        ConfigItem item = new()
        {
            configMaker = new ConfigMakerPresetMetadata
            {
                variables =
                [
                    Variable(null, "skipped"),
                    Variable(" ", "skipped"),
                    Variable("A", "maker"),
                    Variable("B", "maker"),
                    Variable("C", "maker"),
                    Variable("ON", kind: "switch", onValue: "yes", offValue: "no", enabled: true),
                    Variable("OFF", kind: "Switch", onValue: "yes", offValue: "no"),
                    Variable("NULL"),
                ],
            },
            commaVars = new Dictionary<string, string> { ["B"] = "comma", ["C"] = "comma" },
            variables = ["%C%=variable", "not an assignment", "%EMPTY%=$EMPTY", "%PART%=a$emptyb"],
        };

        Assert.Equal(
            "maker comma variable yes no [] [] ab",
            item.ExpandVariables("%A% %B% %C% %ON% %OFF% [%NULL%] [%EMPTY%] %PART%"));
    }

    [Theory]
    [InlineData(true, "on.txt")]
    [InlineData(false, "off.txt")]
    public void LocalConditionChoosesValueFromParameter(bool parameter, string expected)
    {
        ConfigItem item = new()
        {
            jparams = new Dictionary<string, bool> { ["useOn"] = parameter },
            variables = ["%FILE%=$LOCALCONDITION( useOn == true ? on.txt $SEPARATOR off.txt )"],
        };

        Assert.Equal(expected, item.ExpandVariables("%FILE%"));
    }

    [Fact]
    public void LocalConditionWithMissingParameterCountsAsFalse()
    {
        ConfigItem item = new()
        {
            variables = ["%FILE%=$LOCALCONDITION(useOn==FALSE?on.txt$SEPARATORoff.txt)"],
        };

        Assert.Equal("on.txt", item.ExpandVariables("%FILE%"));
    }

    [Fact]
    public void NestedVariablesAreExpanded()
    {
        ConfigItem item = new() { variables = ["%A%=%B%/a.txt", "%B%=%C%", "%C%=lists"] };

        Assert.Equal("lists/a.txt", item.ExpandVariables("%A%"));
    }

    [Fact]
    public void CyclicVariablesStop()
    {
        ConfigItem item = new() { variables = ["%A%=%B%", "%B%=%A%"] };

        Assert.Equal("%A%", item.ExpandVariables("%A%"));
    }

    [Fact]
    public void GrowingVariableStopsAtTheLengthLimit()
    {
        ConfigItem item = new() { variables = ["%A%=%A%%A%"] };

        string result = item.ExpandVariables("%A%");

        Assert.InRange(result.Length, 1, 64 * 1024);
    }

    [Fact]
    public void ExpandFileReferenceFollowsPresetAliases()
    {
        ConfigItem item = new()
        {
            variables = ["%DIR%=lists"],
            configMaker = new ConfigMakerPresetMetadata
            {
                resources =
                [
                    new ConfigMakerResourceMetadata { alias = "Hosts", path = "preset://inner" },
                    new ConfigMakerResourceMetadata { alias = "inner", path = "\"@%DIR%\\hosts.txt\"" },
                    new ConfigMakerResourceMetadata { alias = "empty", path = " " },
                ],
            },
        };

        Assert.Equal("lists/hosts.txt", item.ExpandFileReference("preset://hosts"));
        Assert.Equal("preset://empty", item.ExpandFileReference("preset://empty"));
        Assert.Equal("preset://missing", item.ExpandFileReference("preset://missing"));
        Assert.Equal("", item.ExpandFileReference(null));
    }

    [Fact]
    public void CyclicPresetAliasesStop()
    {
        ConfigItem item = new()
        {
            configMaker = new ConfigMakerPresetMetadata
            {
                resources =
                [
                    new ConfigMakerResourceMetadata { alias = "a", path = "preset://b" },
                    new ConfigMakerResourceMetadata { alias = "b", path = "preset://a" },
                ],
            },
        };

        Assert.Equal("preset://a", item.ExpandFileReference("preset://a"));
        Assert.Throws<FileNotFoundException>(() => item.ResolveFilePath("preset://a", "c", "p"));
    }

    [Fact]
    public void ResolveFilePathUsesTheRightRoot()
    {
        string component = Path.Combine(Path.GetTempPath(), "cdpiui-component");
        string preset = Path.Combine(Path.GetTempPath(), "cdpiui-preset");
        string absolute = Path.Combine(Path.GetTempPath(), "abs", "list.txt");
        ConfigItem item = new() { variables = ["%DIR%=lists"] };

        Assert.Equal(
            Path.Combine(component, "lists", "a.txt"),
            item.ResolveFilePath("%DIR%\\a.txt", component, preset));
        Assert.Equal(
            Path.Combine(preset, "lists", "b.txt"),
            item.ResolveFilePath("$GETCURRENTDIR()/lists/b.txt", component, preset));
        Assert.Equal(
            Path.Combine(preset, "lists", "b.txt"),
            item.ResolveFilePath("$getcurrentdir()lists/b.txt", component, preset));
        Assert.Equal(Path.GetFullPath(absolute), item.ResolveFilePath(absolute, "", ""));
    }

    [Fact]
    public void ResolveFilePathNeedsTheRoot()
    {
        ConfigItem item = new();

        Assert.Throws<DirectoryNotFoundException>(() => item.ResolveFilePath("lists/a.txt", " ", "p"));
        Assert.Throws<DirectoryNotFoundException>(() => item.ResolveFilePath("$GETCURRENTDIR()/a.txt", "c", ""));
    }

    [Fact]
    public void CandidateValuesIncludeEveryAlternative()
    {
        ConfigItem item = new()
        {
            commaVars = new Dictionary<string, string> { ["A"] = "a.txt", ["B"] = " " },
            availableCommaVarsValues =
            [
                new AvailableVarValues { VarName = "A", Values = ["a.txt", "b.txt"] },
                new AvailableVarValues { VarName = "C", Values = null },
            ],
            variables =
            [
                "%X%=x.txt",
                "%Y%=$LOCALCONDITION(p==true?on.lua$SEPARATORoff.lua)",
                "garbage",
            ],
            configMaker = new ConfigMakerPresetMetadata
            {
                variables =
                [
                    new ConfigMakerVariableMetadata
                    {
                        name = "M", value = "m.bin", onValue = "on.bin", offValue = null, values = ["v1", "a.txt"],
                    },
                ],
            },
        };

        Assert.Equal(
            ["a.txt", "b.txt", "x.txt", "on.lua", "off.lua", "m.bin", "on.bin", "v1"],
            item.GetVariableCandidateValues());
    }

    [Fact]
    public void CandidateValuesOfAnEmptyItemAreEmpty()
    {
        Assert.Empty(new ConfigItem().GetVariableCandidateValues());
    }

    [Fact]
    public void UsedFilesCombineResourcesArgumentsAndVariables()
    {
        ConfigItem item = new()
        {
            startup_string = "--hostlist=%LIST% --lua-init=@lib/init.lua --blob=tls:@preset://tls",
            variables = ["%LIST%=lists/hosts.txt"],
            commaVars = new Dictionary<string, string> { ["IPSET"] = "lists/ipset.txt", ["ARGS"] = "--ipset=lists/other.list" },
            configMaker = new ConfigMakerPresetMetadata
            {
                resources =
                [
                    new ConfigMakerResourceMetadata { alias = "tls", path = "files/tls.bin" },
                    new ConfigMakerResourceMetadata { alias = "notes", path = "docs/readme.md", kind = "Other" },
                    new ConfigMakerResourceMetadata { alias = "guess", path = "docs/readme.md2" },
                    new ConfigMakerResourceMetadata { alias = null, path = "skipped.txt" },
                ],
            },
        };

        var files = item.UsedFiles;

        Assert.Contains(files, file => file.Name == "tls" && file.IsAttachedResource &&
            file.Kind == ConfigFileKind.Payload && file.ExpandedPath == "files/tls.bin");
        Assert.Contains(files, file => file.Name == "notes" && file.Kind == ConfigFileKind.Other);
        Assert.Contains(files, file => file.Name == "guess" && file.Kind == ConfigFileKind.Other);
        Assert.Contains(files, file => file.ExpandedPath == "lists/hosts.txt" && file.Kind == ConfigFileKind.SiteList &&
            file.OptionName == "--hostlist");
        Assert.Contains(files, file => file.ExpandedPath == "lib/init.lua" && file.Kind == ConfigFileKind.Library);
        Assert.Contains(files, file => file.ExpandedPath == "lists/ipset.txt" && file.OptionName == "");
        Assert.Contains(files, file => file.ExpandedPath == "lists/other.list" && file.OptionName == "--ipset");
        Assert.DoesNotContain(files, file => file.ExpandedPath == "skipped.txt");
        Assert.Single(files, file => file.ExpandedPath == "files/tls.bin");
        Assert.Single(files, file => file.ExpandedPath == "lists/hosts.txt");
    }

    [Fact]
    public void NormalizeForStorageCleansEditorFormatting()
    {
        ConfigItem item = new()
        {
            name = "  My \n  preset\t",
            meta = " \r\n ",
            startup_string = "--a=1\r\n  --b=2\n",
            variables = ["%A%=1\r\n", "  ", "%B%=\n2"],
            commaVars = new Dictionary<string, string> { ["X"] = "--x\n--y" },
            availableCommaVarsValues =
            [
                new AvailableVarValues { Comment = " line1\r\nline2\r ", Values = ["--a\n--b"] },
                new AvailableVarValues { Comment = "   ", Values = null },
            ],
            configMaker = new ConfigMakerPresetMetadata
            {
                variables =
                [
                    new ConfigMakerVariableMetadata
                    {
                        value = "", description = "  text \r\n", onValue = "--on\n", offValue = "\n",
                        internalParameterName = " ", values = [],
                    },
                    new ConfigMakerVariableMetadata { values = ["a\nb"] },
                ],
                resources = [new ConfigMakerResourceMetadata { alias = " a ", path = "x\r\ny", kind = "" }],
            },
        };

        item.NormalizeForStorage();

        Assert.Equal("My preset", item.name);
        Assert.Equal("My preset", item.not_converted_name);
        Assert.Null(item.meta);
        Assert.Equal("--a=1 --b=2", item.startup_string);
        Assert.Equal(["%A%=1", "%B%= 2"], item.variables);
        Assert.Equal("--x --y", item.commaVars["X"]);
        Assert.Equal("line1\nline2", item.availableCommaVarsValues![0].Comment);
        Assert.Equal(["--a --b"], item.availableCommaVarsValues[0].Values);
        Assert.Equal("", item.availableCommaVarsValues[1].Comment);
        Assert.Null(item.availableCommaVarsValues[1].Values);

        ConfigMakerVariableMetadata first = item.configMaker!.variables![0];
        Assert.Null(first.value);
        Assert.Equal("text", first.description);
        Assert.Equal("--on", first.onValue);
        Assert.Null(first.offValue);
        Assert.Null(first.internalParameterName);
        Assert.Null(first.values);
        Assert.Equal(["a b"], item.configMaker.variables[1].values);

        ConfigMakerResourceMetadata resource = item.configMaker.resources![0];
        Assert.Equal("a", resource.alias);
        Assert.Equal("x y", resource.path);
        Assert.Null(resource.kind);
    }

    [Fact]
    public void NormalizeForStorageDropsEmptyCollections()
    {
        ConfigItem item = new()
        {
            name = null,
            variables = null,
            commaVars = [],
            availableCommaVarsValues = [],
        };

        item.NormalizeForStorage();

        Assert.Equal("", item.name);
        Assert.Equal("", item.startup_string);
        Assert.Empty(item.variables!);
        Assert.Null(item.commaVars);
        Assert.Null(item.availableCommaVarsValues);
    }
}
