using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Shared;

public class StartupInitializationTests
{
    [Fact]
    public void ReadinessExistsBeforeInitializationCanReenterWindowCreation()
    {
        var startup = new StartupInitialization();
        Assert.False(startup.Ready.IsCompleted);
        Task? reentrantReadiness = null;
        startup.Start(() =>
        {
            reentrantReadiness = startup.Ready;
            Assert.False(reentrantReadiness.IsCompleted);
            return Task.CompletedTask;
        });
        Assert.Same(startup.Ready, reentrantReadiness);
        Assert.True(startup.Ready.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SynchronousFailureFaultsTheExistingReadinessTask()
    {
        var startup = new StartupInitialization();
        Task ready = startup.Ready;
        var failure = new DirectoryNotFoundException("Missing Strings");
        startup.Start(() => throw failure);
        Assert.Same(failure, await Assert.ThrowsAsync<DirectoryNotFoundException>(() => ready));
    }

    [Fact]
    public async Task AsynchronousFailureCannotReleaseWaitingWindows()
    {
        var startup = new StartupInitialization();
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        startup.Start(() => operation.Task);
        Assert.False(startup.Ready.IsCompleted);
        operation.SetException(new IOException("Unreadable resource"));
        await Assert.ThrowsAsync<IOException>(() => startup.Ready);
        Assert.False(startup.Ready.IsCompletedSuccessfully);
    }

    [Fact]
    public void ReentrantOrRepeatedStartsDoNotInitializeTwice()
    {
        var startup = new StartupInitialization();
        int calls = 0;
        startup.Start(() =>
        {
            calls++;
            startup.Start(() => { calls++; return Task.CompletedTask; });
            return Task.CompletedTask;
        });
        startup.Start(() => { calls++; return Task.CompletedTask; });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellationPreservesCanceledReadiness()
    {
        var startup = new StartupInitialization();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        startup.Start(() => Task.FromCanceled(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.Ready);
        Assert.True(startup.Ready.IsCanceled);
    }
}
