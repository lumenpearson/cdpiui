using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CDPIUI.Shared.Security;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class WindowsCatalogVerificationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-catalog-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "source");
    private string Catalog => Path.Combine(Source, "catalog.cat");
    public WindowsCatalogVerificationTests() => Directory.CreateDirectory(Source);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitlySkippedDataFileCanBeAnIntentionalLink(bool hardLink)
    {
        if (!OperatingSystem.IsWindows()) return;
        string data = Path.Combine(Source, "list.txt");
        File.WriteAllText(data, "original list");
        File.WriteAllText(Path.Combine(Source, "app.bin"), "signed app");
        string thumbprint = await CreateSignedCatalog(2);
        string outside = Path.Combine(_root, "user-list.txt");
        File.WriteAllText(outside, "user data");
        File.Delete(data);
        if (hardLink) Assert.True(CreateHardLinkW(data, outside, IntPtr.Zero));
        else File.CreateSymbolicLink(data, outside);
        using var unreadable = new FileStream(outside, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(CatalogVerification.Success, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint], ["list.txt"]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SignedCatalogMatchesTextEmptyAndPortableExecutableFiles(int version)
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "text[1].txt"), "данные");
        File.WriteAllBytes(Path.Combine(Source, "empty.bin"), []);
        Directory.CreateDirectory(Path.Combine(Source, "nested"));
        File.Copy(typeof(WindowsCatalogVerificationTests).Assembly.Location, Path.Combine(Source, "nested", "assembly.dll"));
        string thumbprint = await CreateSignedCatalog(version);
        Assert.Equal(CatalogVerification.Success, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint]));
        File.AppendAllText(Path.Combine(Source, "text[1].txt"), "tampered");
        Assert.Equal(CatalogVerification.FilesDoNotMatch, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint]));
    }

    [Fact]
    public async Task ExcludedFileNameDoesNotExcludeADirectorySubtree()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Path.Combine(Source, "list.txt");
        Directory.CreateDirectory(folder);
        string member = Path.Combine(folder, "app.bin");
        File.WriteAllText(member, "original");
        string thumbprint = await CreateSignedCatalog(2);
        File.WriteAllText(member, "tampered");
        Assert.Equal(CatalogVerification.FilesDoNotMatch, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint], ["list.txt"]));
    }

    [Fact]
    public async Task SkippedNamesAreLiteralAndDoNotExcludeSimilarNames()
    {
        if (!OperatingSystem.IsWindows()) return;
        string skipped = Path.Combine(Source, "data[1].txt"), checkedFile = Path.Combine(Source, "data1.txt");
        File.WriteAllText(skipped, "original");
        File.WriteAllText(checkedFile, "original");
        string thumbprint = await CreateSignedCatalog(2);
        File.WriteAllText(skipped, "local user data");
        Assert.Equal(CatalogVerification.Success, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint], ["data[1].txt"]));
        File.WriteAllText(checkedFile, "tampered");
        Assert.Equal(CatalogVerification.FilesDoNotMatch, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint], ["data[1].txt"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomCatalogNameCanBeInsideOrOutsideTheComparedDirectory(bool inside)
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "data.bin"), "signed");
        string thumbprint = await CreateSignedCatalog(2);
        string renamed = Path.Combine(inside ? Source : _root, "catalog[1].cat");
        File.Move(Catalog, renamed);
        Assert.Equal(CatalogVerification.Success, await CatalogVerifier.VerifyAsync(renamed, Source, [thumbprint]));
    }

    [Fact]
    public async Task VerificationUsesTheSameSnapshotThatWillBePromoted()
    {
        if (!OperatingSystem.IsWindows()) return;
        string member = Path.Combine(Source, "required.bin");
        File.WriteAllText(member, "signed");
        string thumbprint = await CreateSignedCatalog(2);
        File.Delete(member);
        using var snapshot = ReadOnlyDirectorySnapshot.Capture(Source);
        File.WriteAllText(member, "signed");
        Assert.Equal(CatalogVerification.FilesDoNotMatch, await CatalogVerifier.VerifySnapshotAsync(Catalog, snapshot, [thumbprint]));
        Assert.Equal(CatalogVerification.Success, await CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint]));
    }

    [Fact]
    public async Task VerificationDoesNotTraverseAnEmptyDirectoryChangedAfterCapture()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(Source, "app.bin"), "signed");
        string empty = Path.Combine(Source, "empty");
        Directory.CreateDirectory(empty);
        string thumbprint = await CreateSignedCatalog(2);
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        string secret = Path.Combine(outside, "must-not-open.bin");
        File.WriteAllText(secret, "not part of the package");
        using var unreadable = new FileStream(secret, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var attributes = CreateFileW(empty, 0x100, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        Assert.False(attributes.IsInvalid);
        // The async call captures the source and starts PowerShell before yielding.
        Task<CatalogVerification> verification = CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint]);
        byte[] tag = JunctionData(outside);
        Assert.True(DeviceIoControl(attributes, 0x900A4, tag, tag.Length, IntPtr.Zero, 0, out _, IntPtr.Zero));
        try { Assert.Equal(CatalogVerification.Success, await verification); }
        finally
        {
            byte[] header = new byte[8];
            BitConverter.GetBytes(0xA0000003u).CopyTo(header, 0);
            Assert.True(DeviceIoControl(attributes, 0x900AC, header, header.Length, IntPtr.Zero, 0, out _, IntPtr.Zero));
        }
    }

    [Fact]
    public async Task MissingCatalogMemberCannotBeFilledThroughALateJunction()
    {
        if (!OperatingSystem.IsWindows()) return;
        string empty = Path.Combine(Source, "empty");
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(empty, "required.bin"), "signed");
        string thumbprint = await CreateSignedCatalog(2);
        string outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.Move(Path.Combine(empty, "required.bin"), Path.Combine(outside, "required.bin"));
        using var attributes = CreateFileW(empty, 0x100, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        Task<CatalogVerification> verification = CatalogVerifier.VerifyAsync(Catalog, Source, [thumbprint]);
        byte[] tag = JunctionData(outside);
        Assert.True(DeviceIoControl(attributes, 0x900A4, tag, tag.Length, IntPtr.Zero, 0, out _, IntPtr.Zero));
        try { Assert.Equal(CatalogVerification.FilesDoNotMatch, await verification); }
        finally
        {
            byte[] header = new byte[8];
            BitConverter.GetBytes(0xA0000003u).CopyTo(header, 0);
            Assert.True(DeviceIoControl(attributes, 0x900AC, header, header.Length, IntPtr.Zero, 0, out _, IntPtr.Zero));
        }
    }

    private async Task<string> CreateSignedCatalog(int version)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$ErrorActionPreference='Stop'; Import-Module ($PSHOME + '\\Modules\\Microsoft.PowerShell.Security\\Microsoft.PowerShell.Security.psd1'); New-FileCatalog -Path $env:CDPIUI_TEST_SOURCE -CatalogFilePath $env:CDPIUI_TEST_CATALOG -CatalogVersion " + version + " | Out-Null");
        start.Environment["CDPIUI_TEST_SOURCE"] = Source;
        start.Environment["CDPIUI_TEST_CATALOG"] = Catalog;
        using var process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(process.ExitCode == 0, await stderr + await stdout);
        var original = new SignedCms();
        original.Decode(File.ReadAllBytes(Catalog));
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=CDPIUI local catalog test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var signed = new SignedCms(original.ContentInfo, false);
        signed.ComputeSignature(new CmsSigner(cert));
        File.WriteAllBytes(Catalog, signed.Encode());
        Assert.Equal(CatalogVerification.Success, CatalogVerifier.VerifySignature(signed.Encode(), [cert.Thumbprint]));
        return cert.Thumbprint;
    }

    private static byte[] JunctionData(string target)
    {
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + target), print = Encoding.Unicode.GetBytes(target);
        byte[] data = new byte[20 + substitute.Length + print.Length];
        using var writer = new BinaryWriter(new MemoryStream(data));
        writer.Write(0xA0000003u); writer.Write((ushort)(data.Length - 8)); writer.Write((ushort)0);
        writer.Write((ushort)0); writer.Write((ushort)substitute.Length);
        writer.Write((ushort)(substitute.Length + 2)); writer.Write((ushort)print.Length);
        writer.Write(substitute); writer.Write((ushort)0); writer.Write(print); writer.Write((ushort)0);
        return data;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string target, IntPtr attributes);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, IntPtr output, int outputSize, out uint returned, IntPtr overlapped);
}
