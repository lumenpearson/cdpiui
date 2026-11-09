using CDPIUI.Core.Store;
using CDPIUI.Shared;

namespace CDPIUI.Tests.Core;

[Collection("Windows path leases")]
public sealed class StoreItemFileSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-store-files-" + Guid.NewGuid().ToString("N"));
    private string Items => Path.Combine(_root, "Items");
    public StoreItemFileSafetyTests() => Directory.CreateDirectory(Items);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("LPT0")]
    [InlineData("localuserstorage")]
    [InlineData("cdpiuiappst")]
    [InlineData("component\n")]
    public void DeviceNamesAndCaseAliasesOfReservedIdsAreNotValidIds(string id)
    {
        Assert.False(StoreItemIds.IsValid(id));
    }

    [Theory]
    [InlineData("component-1")]
    [InlineData("component_2")]
    [InlineData("CDPIUIAppSt")]
    [InlineData("LocalUserStorage")]
    public void CanonicalOrdinaryIdsRemainValid(string id) => Assert.True(StoreItemIds.IsValid(id));

    [Fact]
    public void ItemFolderMustBeTheMatchingImmediateOrdinaryChild()
    {
        Assert.True(StoreItemIds.MatchesItemFolder(Items, "component", Path.Combine(Items, "component")));
        Assert.False(StoreItemIds.MatchesItemFolder(Items, "component", Path.Combine(Items, "another")));
        Assert.False(StoreItemIds.MatchesItemFolder(Items, "component", Path.Combine(Items, "component", "nested")));
        Assert.False(StoreItemIds.MatchesItemFolder(Items, "component", Items));
        Assert.False(StoreItemIds.MatchesItemFolder(Items, "component", Path.Combine(_root, "Items-other", "component")));
        Assert.False(StoreItemIds.MatchesItemFolder(Items, "../outside", Path.Combine(_root, "outside")));
    }

    [Theory]
    [InlineData("localuserstorage")]
    [InlineData("LOCALUSERSTORAGE")]
    [InlineData("CDPIUIAPPST")]
    public void CleanupRefusesProtectedIdsEvenThroughCaseAliases(string id)
    {
        if (!OperatingSystem.IsWindows()) return;
        string canonical = id.Contains("LOCAL", StringComparison.OrdinalIgnoreCase) ? SharedConstants.LocalUserItemsId : SharedConstants.ApplicationStoreId;
        string protectedDirectory = Path.Combine(Items, canonical);
        Directory.CreateDirectory(protectedDirectory);
        File.WriteAllText(Path.Combine(protectedDirectory, "keep.bin"), "keep");
        Assert.Throws<UnauthorizedAccessException>(() => StoreItemDirectoryOperations.Delete(Items, id, Path.Combine(Items, id)));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(protectedDirectory, "keep.bin")));
    }

    [Fact]
    public void CleanupRejectsDirectoryLinksBeforeRemovingAnyInstalledFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        string item = Path.Combine(Items, "component");
        Directory.CreateDirectory(item);
        File.WriteAllText(Path.Combine(item, "first.bin"), "installed");
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.bin"), "keep");
        Directory.CreateSymbolicLink(Path.Combine(item, "nested-link"), outside);
        Assert.Throws<UnauthorizedAccessException>(() => StoreItemDirectoryOperations.Delete(Items, "component", item));
        Assert.Equal("installed", File.ReadAllText(Path.Combine(item, "first.bin")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "keep.bin")));
    }

    [Fact]
    public void CleanCreationPreservesBusyTreeAndAllowsAReplacementAfterRelease()
    {
        if (!OperatingSystem.IsWindows()) return;
        string item = Path.Combine(Items, "component");
        Directory.CreateDirectory(item);
        string file = Path.Combine(item, "data.bin");
        File.WriteAllText(file, "installed");
        using (var busy = File.OpenRead(file))
            Assert.Throws<IOException>(() => StoreItemDirectoryOperations.Create(Items, "component", item, clean: true));
        Assert.Equal("installed", File.ReadAllText(file));
        StoreItemDirectoryOperations.Create(Items, "component", item, clean: true);
        Assert.True(Directory.Exists(item));
        Assert.Empty(Directory.EnumerateFileSystemEntries(item));
    }
}
