using CDPIUI.Core.Store.Repository;

namespace CDPIUI.Tests.Core;

public class StoreLoadGateTests
{
    [Fact]
    public async Task ConcurrentCallersShareOneLoad()
    {
        StoreLoadGate<bool> gate = new();
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        Task<bool> Load()
        {
            Interlocked.Increment(ref started);
            return release.Task;
        }

        Task<bool>[] calls = Enumerable.Range(0, 4).Select(_ => gate.Run(false, Load)).ToArray();
        release.SetResult(true);

        Assert.All(await Task.WhenAll(calls), Assert.True);
        Assert.Equal(1, started);
    }

    [Fact]
    public async Task CallAfterFinishStartsNewLoad()
    {
        StoreLoadGate<bool> gate = new();
        int started = 0;
        async Task<bool> Load()
        {
            Interlocked.Increment(ref started);
            await Task.Yield();
            return true;
        }

        await gate.Run(false, Load);
        await gate.Run(false, Load);
        // Finished at once: it must not be kept either.
        await gate.Run(false, () => { Interlocked.Increment(ref started); return Task.FromResult(true); });
        await gate.Run(false, Load);

        Assert.Equal(4, started);
    }

    [Fact]
    public async Task OtherKeyWaitsForRunningLoad()
    {
        StoreLoadGate<bool> gate = new();
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool secondStarted = false;

        Task<bool> first = gate.Run(false, () => release.Task);
        Task<bool> second = gate.Run(true, () => { secondStarted = true; return Task.FromResult(true); });

        Assert.False(secondStarted);
        release.SetResult(false);

        Assert.False(await first);
        Assert.True(await second);
        Assert.True(secondStarted);
    }

    [Fact]
    public async Task FailedLoadDoesNotBlockNextOne()
    {
        StoreLoadGate<bool> gate = new();
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> first = gate.Run(false, () => release.Task);
        Task<bool> second = gate.Run(true, () => Task.FromResult(true));
        release.SetException(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        Assert.True(await second);
    }
}
