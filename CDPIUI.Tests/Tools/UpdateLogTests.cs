using System.Runtime.InteropServices;
using Update;

namespace CDPIUI.Tests.Tools;

[Collection("Windows path leases")]
public sealed class UpdateLogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-update-log-" + Guid.NewGuid().ToString("N"));
    public UpdateLogTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoggingNeverAppendsThroughALink(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt");
        string log = Path.Combine(_root, "update.log");
        File.WriteAllText(outside, "keep");
        if (hardLink) Assert.True(CreateHardLinkW(log, outside, IntPtr.Zero));
        else File.CreateSymbolicLink(log, outside);
        UpdateLog.WriteTo(log, "injected log message");
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public void OrdinaryLogPreservesExistingTextAndAppends()
    {
        string log = Path.Combine(_root, "update.log");
        File.WriteAllText(log, "original\n");
        UpdateLog.WriteTo(log, "new entry");
        string content = File.ReadAllText(log);
        Assert.StartsWith("original\n", content);
        Assert.Contains("new entry", content);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr attributes);
}
