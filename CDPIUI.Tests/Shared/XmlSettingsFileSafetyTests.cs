using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using CDPIUI.Shared.Basic;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class XmlSettingsFileSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-settings-safety-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_root, "Settings.xml");
    public XmlSettingsFileSafetyTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedSettingsAreNeitherReadNorWritten(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.xml");
        new XMLSettingsService(outside).SetValue("SYSTEM", "secret", 42);
        string original = File.ReadAllText(outside);
        Link(SettingsPath, outside, hardLink);
        var settings = new XMLSettingsService(SettingsPath);
        Assert.NotEqual(42, settings.GetValue<int>("SYSTEM", "secret"));
        Assert.Equal(original, File.ReadAllText(outside));
        Assert.Equal(original, File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedLockNeverDeletesItsTargetOrPermitsSaving(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue("APPEARANCE", "Theme", "Dark");
        string original = File.ReadAllText(SettingsPath);
        string outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "keep");
        Link(SettingsPath + ".lock", outside, hardLink);
        settings.SetValue("APPEARANCE", "Theme", "Light");
        Assert.True(File.Exists(outside));
        Assert.Equal("keep", File.ReadAllText(outside));
        Assert.Equal(original, File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryRefusesLinkedBackups(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.xml");
        new XMLSettingsService(outside).SetValue("SYSTEM", "secret", 42);
        string original = File.ReadAllText(outside);
        File.WriteAllText(SettingsPath, "<Settings><broken>");
        Link(SettingsPath + ".bak", outside, hardLink);
        var settings = new XMLSettingsService(SettingsPath);
        Assert.True(settings.HasUnrecoverableLoadError);
        Assert.NotEqual(42, settings.GetValue<int>("SYSTEM", "secret"));
        Assert.Equal(original, File.ReadAllText(outside));
    }

    [Fact]
    public void RecoveryNoticeNeverWritesThroughALink()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "keep");
        File.WriteAllText(SettingsPath, "<Settings><broken>");
        File.CreateSymbolicLink(SettingsPath + ".recovery-notice", outside);
        var settings = new XMLSettingsService(SettingsPath);
        Assert.True(settings.HasUnrecoverableLoadError);
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public void InitialCreationRespectsAnAlreadyHeldLock()
    {
        using (new FileStream(SettingsPath + ".lock", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            var settings = new XMLSettingsService(SettingsPath);
            Assert.False(File.Exists(SettingsPath));
        }
    }

    [Fact]
    public void RecoveryDoesNotWriteWhileAnotherProcessHoldsTheLock()
    {
        new XMLSettingsService(SettingsPath).SetValue("SYSTEM", "value", 1);
        File.WriteAllText(SettingsPath, "<Settings><broken>");
        string original = File.ReadAllText(SettingsPath);
        using (new FileStream(SettingsPath + ".lock", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            var settings = new XMLSettingsService(SettingsPath);
            Assert.Equal(original, File.ReadAllText(SettingsPath));
            Assert.Empty(Directory.EnumerateFiles(_root, "Settings.xml.corrupt-*"));
            Assert.False(File.Exists(SettingsPath + ".recovery-notice"));
        }
    }

    [Fact]
    public void SaveDoesNotReplaceTheLastGoodBackupWithMalformedSettings()
    {
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue("SYSTEM", "value", 1);
        settings.SetValue("SYSTEM", "value", 2);
        string backup = File.ReadAllText(SettingsPath + ".bak");
        File.WriteAllText(SettingsPath, "<Settings><broken>");
        settings.SetValue("SYSTEM", "value", 3);
        Assert.Equal(backup, File.ReadAllText(SettingsPath + ".bak"));
        Assert.Equal(3, new XMLSettingsService(SettingsPath).GetValue<int>("SYSTEM", "value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsAndBackupKeepTheExistingAccessRules(bool protect)
    {
        if (!OperatingSystem.IsWindows()) return;
        var settings = new XMLSettingsService(SettingsPath);
        settings.SetValue("SYSTEM", "value", 1);
        var file = new FileInfo(SettingsPath);
        if (protect)
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            file.SetAccessControl(security);
        }
        string[] original = AccessRules(file);
        bool originallyProtected = file.GetAccessControl().AreAccessRulesProtected;

        for (int i = 2; i < 5; i++) settings.SetValue("SYSTEM", "value", i);

        Assert.Equal(original, AccessRules(new FileInfo(SettingsPath)));
        Assert.Equal(original, AccessRules(new FileInfo(SettingsPath + ".bak")));
        Assert.Equal(originallyProtected, new FileInfo(SettingsPath).GetAccessControl().AreAccessRulesProtected);
        Assert.Equal(originallyProtected, new FileInfo(SettingsPath + ".bak").GetAccessControl().AreAccessRulesProtected);
    }

    [Fact]
    public void LinkedParentNeverCreatesSettingsInItsTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(_root, "outside");
        string link = Path.Combine(_root, "link");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(link, outside);
        new XMLSettingsService(Path.Combine(link, "Settings.xml")).SetValue("SYSTEM", "key", "value");
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    private static void Link(string path, string target, bool hardLink)
    {
        if (hardLink) Assert.True(CreateHardLinkW(path, target, IntPtr.Zero));
        else File.CreateSymbolicLink(path, target);
    }

    private static string[] AccessRules(FileInfo file) => file.GetAccessControl()
        .GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
        .Select(rule => $"{rule.IdentityReference.Value}:{rule.AccessControlType}:{rule.FileSystemRights}:{rule.InheritanceFlags}:{rule.PropagationFlags}:{rule.IsInherited}")
        .Order(StringComparer.Ordinal).ToArray();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr attributes);
}
