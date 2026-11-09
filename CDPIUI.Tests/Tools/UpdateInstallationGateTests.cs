using System.Runtime.InteropServices;
using Update;

namespace CDPIUI.Tests.Tools;

[Collection("Windows path leases")]
public sealed class UpdateInstallationGateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-update-gate-" + Guid.NewGuid().ToString("N"));
    public UpdateInstallationGateTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task SecondOperationCannotEnterUntilTheWholeFirstOperationEnds()
    {
        if (!OperatingSystem.IsWindows()) return;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string marker = Path.Combine(_root, "marker.txt");
        Task<bool> first = UpdateInstallationGate.RunAsync(_root, async () =>
        {
            File.WriteAllText(marker, "first operation");
            entered.SetResult();
            await release.Task;
        }, error => throw new Exception("The first operation must acquire its lock.", error));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            bool secondEntered = false;
            Exception? refusal = null;
            Assert.False(await UpdateInstallationGate.RunAsync(_root, () =>
            {
                secondEntered = true;
                File.WriteAllText(marker, "second operation");
                return Task.CompletedTask;
            }, error => refusal = error));
            Assert.False(secondEntered);
            Assert.NotNull(refusal);
            Assert.Equal("first operation", File.ReadAllText(marker));
        }
        finally { release.TrySetResult(); await first; }
        Assert.True(await UpdateInstallationGate.RunAsync(_root, () => Task.CompletedTask, error => throw error));
        Assert.False(File.Exists(Path.Combine(_root, UpdateInstallationGate.LockFileName)));
    }

    [Fact]
    public async Task FailedOperationReleasesTheLockAndKeepsItsOriginalError()
    {
        if (!OperatingSystem.IsWindows()) return;
        var expected = new IOException("operation failed");
        var actual = await Assert.ThrowsAsync<IOException>(() => UpdateInstallationGate.RunAsync(_root,
            () => throw expected, error => throw new Exception("Not an acquisition error", error)));
        Assert.Same(expected, actual);
        Assert.True(await UpdateInstallationGate.RunAsync(_root, () => Task.CompletedTask, error => throw error));
    }

    [Theory]
    [InlineData("symbolic")]
    [InlineData("hard")]
    [InlineData("nonempty")]
    public async Task UnsafeLockCannotStartTheOperationOrDeleteExistingData(string kind)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt"), lockPath = Path.Combine(_root, UpdateInstallationGate.LockFileName);
        File.WriteAllText(outside, "keep");
        if (kind == "symbolic") File.CreateSymbolicLink(lockPath, outside);
        else if (kind == "hard") Assert.True(CreateHardLinkW(lockPath, outside, IntPtr.Zero));
        else File.WriteAllText(lockPath, "keep");
        bool entered = false;
        Exception? refusal = null;
        Assert.False(await UpdateInstallationGate.RunAsync(_root, () => { entered = true; return Task.CompletedTask; }, error => refusal = error));
        Assert.False(entered);
        Assert.NotNull(refusal);
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal("keep", File.ReadAllText(lockPath));
    }

    [Fact]
    public async Task DifferentInstallDirectoriesHaveIndependentLocks()
    {
        if (!OperatingSystem.IsWindows()) return;
        string other = Path.Combine(_root, "other");
        bool nestedEntered = false;
        Assert.True(await UpdateInstallationGate.RunAsync(_root, async () =>
        {
            Assert.True(await UpdateInstallationGate.RunAsync(other, () => { nestedEntered = true; return Task.CompletedTask; }, error => throw error));
        }, error => throw error));
        Assert.True(nestedEntered);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string target, IntPtr attributes);
}
