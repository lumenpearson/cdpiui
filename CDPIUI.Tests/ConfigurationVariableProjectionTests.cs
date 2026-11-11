using System.Globalization;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Tests;

public class ConfigurationVariableProjectionTests
{
    [Fact]
    public void BooleanProjectionKeepsDictionaryOrderSpellingAndDefaultFields()
    {
        var item = new ConfigItem { jparams = new() { ["Zulu"] = false, ["alpha"] = true, [" %Raw% "] = true } };
        var result = ConfigurationService.GetVariables(item);
        Assert.Equal(new[] { "Zulu", "alpha", " %Raw% " }, result.Select(v => v.name));
        Assert.Equal(new[] { false, true, true }, result.Select(v => v.value));
        Assert.All(result, v => Assert.Null(v.variable_name));
    }

    [Fact]
    public void StaticAndInstanceBooleanProjectionsHaveEquivalentButIndependentResults()
    {
        var item = new ConfigItem { jparams = new() { ["flag"] = true } }; var service = Owned(item);
        var first = ConfigurationService.GetVariables(item); var second = service.GetVariables(item.file_name!, item.packId!);
        Assert.NotSame(first, second); Assert.NotSame(Assert.Single(first), Assert.Single(second));
        Assert.Equal(first[0].name, second[0].name); Assert.Equal(first[0].value, second[0].value);
        first[0].name = "changed"; first[0].value = false; first.Clear();
        Assert.True(item.jparams["flag"]); Assert.Equal("flag", second[0].name); Assert.True(second[0].value);
    }

    [Fact]
    public void LaterBooleanCallsReadCurrentDictionaryAndAllocateNewModels()
    {
        var item = new ConfigItem { jparams = new() { ["first"] = false } };
        var first = ConfigurationService.GetVariables(item); item.jparams["first"] = true; item.jparams["next"] = false;
        var second = ConfigurationService.GetVariables(item);
        Assert.NotSame(first, second); Assert.NotSame(first[0], second[0]); Assert.False(first[0].value);
        Assert.Equal(new[] { "first", "next" }, second.Select(v => v.name)); Assert.True(second[0].value);
    }

    [Fact]
    public void EmptyBooleanCallsStillOwnDifferentLists()
    {
        var item = new ConfigItem { jparams = [] };
        var first = ConfigurationService.GetVariables(item); var second = ConfigurationService.GetVariables(item);
        Assert.Empty(first); Assert.Empty(second); Assert.NotSame(first, second);
    }

    [Fact]
    public void StaticNullItemKeepsItsNullReferenceFailure()
    {
        Assert.Throws<NullReferenceException>(() => { ConfigurationService.GetVariables(null!); });
    }

    [Fact]
    public void StaticNullDictionaryKeepsItsNullReferenceFailure()
    {
        Assert.Throws<NullReferenceException>(() => { ConfigurationService.GetVariables(new ConfigItem()); });
    }

    [Fact]
    public void MissingInstanceItemReturnsFreshEmptyLists()
    {
        var service = new ConfigurationService("owned");
        var first = service.GetVariables("missing", "missing"); var second = service.GetVariables("missing", "missing");
        var commaFirst = service.GetCommaVariables("missing", "missing"); var commaSecond = service.GetCommaVariables("missing", "missing");
        Assert.Empty(first); Assert.Empty(second); Assert.NotSame(first, second);
        Assert.Empty(commaFirst); Assert.Empty(commaSecond); Assert.NotSame(commaFirst, commaSecond);
    }

    [Fact]
    public void InstanceNullDictionaryGuardsDoNotReadMalformedMetadata()
    {
        var item = new ConfigItem { availableCommaVarsValues = [null!] }; var service = Owned(item);
        Assert.Empty(service.GetVariables(item.file_name!, item.packId!));
        Assert.Empty(service.GetCommaVariables(item.file_name!, item.packId!));
    }

    [Theory]
    [InlineData("Fixture.json", "fixture-pack")]
    [InlineData("fixture.json", "Fixture-pack")]
    [InlineData("fixture.json ", "fixture-pack")]
    public void InstanceLookupRetainsOrdinalUntrimmedFileAndPackNames(string file, string pack)
    {
        var item = new ConfigItem { jparams = new() { ["flag"] = true }, commaVars = new() { ["choice"] = "one" } };
        var service = Owned(item);
        Assert.Empty(service.GetVariables(file, pack)); Assert.Empty(service.GetCommaVariables(file, pack));
    }

    [Fact]
    public void CommaProjectionKeepsDictionaryOrderAndRawNames()
    {
        var item = new ConfigItem { commaVars = new() { ["Zulu"] = "z", ["alpha"] = "a", ["%Raw%"] = "raw" } };
        var result = Project(item);
        Assert.Equal(new[] { "Zulu", "alpha", "%Raw%" }, result.Select(v => v.name));
        Assert.Equal(new[] { "z", "a", "raw" }, result.Select(v => v.value));
        Assert.All(result, v => { Assert.Null(v.comment); Assert.Empty(v.values); });
    }

    [Fact]
    public void FirstCaseInsensitiveMetadataMatchWinsAndKeepsRawCurrentValue()
    {
        var first = new AvailableVarValues { VarName = "CHOICE", Comment = "first", Values = ["first-option"], CurrentValueIndex = 41 };
        var second = new AvailableVarValues { VarName = "choice", Comment = "second", Values = ["second-option"], CurrentValueIndex = 42 };
        var item = new ConfigItem { commaVars = new() { ["Choice"] = "outside-options" }, availableCommaVarsValues = [first, second] };
        var result = Assert.Single(Project(item));
        Assert.Equal("Choice", result.name); Assert.Equal("first", result.comment); Assert.Equal("outside-options", result.value);
        Assert.Equal(new[] { "first-option" }, result.values); Assert.Equal(41, first.CurrentValueIndex); Assert.Equal(42, second.CurrentValueIndex);
    }

    [Fact]
    public void OptionsUseOrdinalStableDistinctAndKeepFirstStringReferences()
    {
        var first = new string(new[] { 'o', 'n', 'e' }); var duplicate = new string(new[] { 'o', 'n', 'e' });
        var item = Choice([first, "ONE", duplicate, "two", "ONE", "two"]);
        var result = Assert.Single(Project(item));
        Assert.Equal(new[] { "one", "ONE", "two" }, result.values); Assert.Same(first, result.values[0]);
        Assert.NotSame(item.availableCommaVarsValues![0].Values, result.values);
    }

    [Fact]
    public void ReturnedCommaModelsAndOptionListsAreIndependentAcrossCalls()
    {
        var item = Choice(["one", "two"]); var service = Owned(item);
        var first = service.GetCommaVariables(item.file_name!, item.packId!); var second = service.GetCommaVariables(item.file_name!, item.packId!);
        Assert.NotSame(first, second); Assert.NotSame(first[0], second[0]); Assert.NotSame(first[0].values, second[0].values);
        first[0].name = "changed"; first[0].value = "changed"; first[0].comment = "changed"; first[0].values.Clear(); first.Clear();
        Assert.Equal("choice", second[0].name); Assert.Equal("current", second[0].value); Assert.Equal("comment", second[0].comment);
        Assert.Equal(new[] { "one", "two" }, second[0].values); Assert.Equal(new[] { "one", "two" }, item.availableCommaVarsValues![0].Values);
        Assert.Equal("current", item.commaVars!["choice"]);
    }

    [Fact]
    public void CaseVariantKeysSharingMetadataStillGetIndependentOptionLists()
    {
        var item = Choice(["one"]); item.commaVars!["CHOICE"] = "other";
        var result = Project(item); Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "choice", "CHOICE" }, result.Select(v => v.name)); Assert.NotSame(result[0].values, result[1].values);
        result[0].values.Add("local"); Assert.Equal(new[] { "one" }, result[1].values); Assert.Equal(new[] { "one" }, item.availableCommaVarsValues![0].Values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" outside ")]
    public void RawCurrentValuesAreNotValidatedOrNormalized(string? value)
    {
        var item = Choice(["one"]); item.commaVars!["choice"] = value!;
        Assert.Equal(value, Assert.Single(Project(item)).value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" comment ")]
    public void NullOptionsBecomeFreshEmptyListsWhileCommentsStayRaw(string? comment)
    {
        var item = Choice(null); item.availableCommaVarsValues![0].Comment = comment!;
        var first = Assert.Single(Project(item)); var second = Assert.Single(Project(item));
        Assert.Equal(comment, first.comment); Assert.Empty(first.values); Assert.Empty(second.values); Assert.NotSame(first.values, second.values);
    }

    [Fact]
    public void MissingMetadataHasNullCommentAndAnOwnedEmptyList()
    {
        var item = Choice(["unused"]); item.availableCommaVarsValues![0].VarName = "different";
        var first = Assert.Single(Project(item)); var second = Assert.Single(Project(item));
        Assert.Null(first.comment); Assert.Empty(first.values); Assert.NotSame(first.values, second.values);
    }

    [Fact]
    public void EmptyDictionaryDoesNotInspectRuntimeNullMetadataEntries()
    {
        var item = new ConfigItem { commaVars = [], availableCommaVarsValues = [null!] };
        var first = Project(item); var second = Project(item);
        Assert.Empty(first); Assert.Empty(second); Assert.NotSame(first, second);
    }

    [Fact]
    public void NullMetadataBeforeAMatchRetainsOriginalFailure()
    {
        var item = Choice(["one"]); item.availableCommaVarsValues!.Insert(0, null!);
        Assert.Throws<NullReferenceException>(() => { Project(item); });
    }

    [Fact]
    public void MatchingMetadataStopsBeforeALaterRuntimeNullEntry()
    {
        var item = Choice(["one"]); item.availableCommaVarsValues!.Add(null!);
        Assert.Equal(new[] { "one" }, Assert.Single(Project(item)).values);
    }

    [Fact]
    public void NullMetadataNameIsSkippedWithoutRepairingOtherMetadata()
    {
        var item = Choice(["one"]); item.availableCommaVarsValues!.Insert(0, new AvailableVarValues { VarName = null, Comment = "wrong" });
        Assert.Equal("comment", Assert.Single(Project(item)).comment);
    }

    [Fact]
    public void RuntimeNullOptionsAreRetainedOnceByOrdinalDistinct()
    {
        var item = Choice([null!, "one", null!, "ONE", "one"]);
        var result = Assert.Single(Project(item)); Assert.Equal(3, result.values.Count);
        Assert.Null(result.values[0]); Assert.Equal("one", result.values[1]); Assert.Equal("ONE", result.values[2]);
    }

    [Fact]
    public void LaterCallsReadReplacedSourceDictionaryAndMetadata()
    {
        var item = Choice(["old"]); var service = Owned(item); var first = service.GetCommaVariables(item.file_name!, item.packId!);
        item.commaVars = new() { ["next"] = "new-current" };
        item.availableCommaVarsValues = [new AvailableVarValues { VarName = "NEXT", Comment = "new-comment", Values = ["new-option"] }];
        var second = Assert.Single(service.GetCommaVariables(item.file_name!, item.packId!));
        Assert.Equal("choice", first[0].name); Assert.Equal(new[] { "old" }, first[0].values);
        Assert.Equal("next", second.name); Assert.Equal("new-current", second.value); Assert.Equal("new-comment", second.comment);
        Assert.Equal(new[] { "new-option" }, second.values);
    }

    [Fact]
    public void MetadataMatchingAndOptionDistinctRemainOrdinalUnderTurkishCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var item = new ConfigItem { commaVars = new() { ["I"] = "raw" }, availableCommaVarsValues =
                [new AvailableVarValues { VarName = "İ", Comment = "wrong" }, new AvailableVarValues { VarName = "i", Comment = "right", Values = ["i", "I", "i"] }] };
            var result = Assert.Single(Project(item)); Assert.Equal("right", result.comment); Assert.Equal(new[] { "i", "I" }, result.values);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static ConfigItem Choice(List<string>? options) => new()
    {
        commaVars = new() { ["choice"] = "current" },
        availableCommaVarsValues = [new AvailableVarValues { VarName = "choice", Comment = "comment", Values = options }]
    };

    private static ConfigurationService Owned(ConfigItem item)
    {
        item.file_name ??= "fixture.json"; item.packId ??= "fixture-pack";
        var service = new ConfigurationService("owned"); service.SetConfigItem(item); return service;
    }

    private static List<CommaVariableItem> Project(ConfigItem item)
    {
        var service = Owned(item); return service.GetCommaVariables(item.file_name!, item.packId!);
    }
}
