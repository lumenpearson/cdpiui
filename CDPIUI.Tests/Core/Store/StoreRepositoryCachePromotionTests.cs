using CDPIUI.Core.Store.Repository;

namespace CDPIUI.Tests.Core.Store;

public class StoreRepositoryCachePromotionTests
{
    [Fact]
    public void ReplacementPublishesNewCacheAndRetainsOldCacheUntilCleanup()
    {
        using var scratch = new CacheScratchDirectory();
        scratch.CreateOldCache();

        string? backup = StoreRepositoryCachePromotion.Promote(scratch.Staged, scratch.Target);

        Assert.NotNull(backup);
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Target, "init.json")));
        Assert.Equal("old cache", File.ReadAllText(Path.Combine(backup, "init.json")));
        Assert.False(Directory.Exists(scratch.Staged));
    }

    [Fact]
    public void FailedPublicationRestoresOldCacheAndPreservesOriginalError()
    {
        using var scratch = new CacheScratchDirectory();
        scratch.CreateOldCache();
        var failure = new IOException("The fixture rejected publication.");

        Exception? actual = Record.Exception(() => StoreRepositoryCachePromotion.Promote(
            scratch.Staged, scratch.Target, (source, destination) =>
            {
                if (source == scratch.Staged)
                    throw failure;
                Directory.Move(source, destination);
            }));

        Assert.Same(failure, actual);
        Assert.True(Directory.Exists(scratch.Target), "A failed publication must retain the previous cache.");
        Assert.Equal("old cache", File.ReadAllText(Path.Combine(scratch.Target, "init.json")));
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Staged, "init.json")));
    }

    [Fact]
    public void FailureToRetainOldCacheDoesNotStartPublication()
    {
        using var scratch = new CacheScratchDirectory();
        scratch.CreateOldCache();
        var failure = new IOException("The fixture cannot rename the previous cache.");
        bool attemptedPublication = false;

        Exception? actual = Record.Exception(() => StoreRepositoryCachePromotion.Promote(
            scratch.Staged, scratch.Target, (source, destination) =>
            {
                if (source == scratch.Target)
                    throw failure;
                attemptedPublication = true;
                Directory.Move(source, destination);
            }));

        Assert.Same(failure, actual);
        Assert.False(attemptedPublication);
        Assert.Equal("old cache", File.ReadAllText(Path.Combine(scratch.Target, "init.json")));
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Staged, "init.json")));
    }

    [Fact]
    public void FailedRestorationRetainsBackupAndDoesNotRemoveUnexpectedTarget()
    {
        using var scratch = new CacheScratchDirectory();
        scratch.CreateOldCache();

        var error = Assert.Throws<RepositoryCacheRecoveryException>(() =>
            StoreRepositoryCachePromotion.Promote(scratch.Staged, scratch.Target, (source, destination) =>
            {
                if (source == scratch.Staged)
                    File.WriteAllText(destination, "unexpected target");
                Directory.Move(source, destination);
            }));

        Assert.Equal("old cache", File.ReadAllText(Path.Combine(error.PreviousCachePath, "init.json")));
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Staged, "init.json")));
        Assert.Equal("unexpected target", File.ReadAllText(scratch.Target));
        Assert.Equal(scratch.Target, error.TargetCachePath);
        Assert.Equal(scratch.Staged, error.ExtractedCachePath);
        var causes = Assert.IsType<AggregateException>(error.InnerException);
        Assert.Equal(2, causes.InnerExceptions.Count);
        Assert.All(causes.InnerExceptions, cause => Assert.IsAssignableFrom<IOException>(cause));
        Assert.Contains(error.PreviousCachePath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstCachePublishesWithoutCreatingBackup()
    {
        using var scratch = new CacheScratchDirectory();

        string? backup = StoreRepositoryCachePromotion.Promote(scratch.Staged, scratch.Target);

        Assert.Null(backup);
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Target, "init.json")));
        Assert.False(Directory.Exists(scratch.Staged));
        Assert.Single(Directory.EnumerateDirectories(scratch.Root));
    }

    [Fact]
    public void FirstPublicationFailureRetainsStagedCacheAndOriginalError()
    {
        using var scratch = new CacheScratchDirectory();
        var failure = new IOException("The fixture rejected the first cache.");

        Exception? actual = Record.Exception(() => StoreRepositoryCachePromotion.Promote(
            scratch.Staged, scratch.Target, (_, _) => throw failure));

        Assert.Same(failure, actual);
        Assert.False(Directory.Exists(scratch.Target));
        Assert.Equal("new cache", File.ReadAllText(Path.Combine(scratch.Staged, "init.json")));
    }

    private sealed class CacheScratchDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("cdpiui-cache-promotion-tests-");

        public string Root => _directory.FullName;
        public string Target => Path.Combine(Root, "cache");
        public string Staged => Path.Combine(Root, "cache.new");

        public CacheScratchDirectory()
        {
            Directory.CreateDirectory(Staged);
            File.WriteAllText(Path.Combine(Staged, "init.json"), "new cache");
        }

        public void CreateOldCache()
        {
            Directory.CreateDirectory(Target);
            File.WriteAllText(Path.Combine(Target, "init.json"), "old cache");
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
