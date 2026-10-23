using CDPIUI.AddOns.ConfigImport;

namespace CDPIUI.Tests.AddOns;

public class ZapretConfigImportServiceTests
{
    [Theory]
    [InlineData("--a=1 --b=x^&y & echo done", "--a=1 --b=x&y ")]
    [InlineData("--a=1 ^| --b=2 > log.txt", "--a=1 | --b=2 ")]
    [InlineData("--hostlist=\"a^b & c.txt\" --c=^^", "--hostlist=\"a^b & c.txt\" --c=^")]
    public void ShellTailIsCutAndCaretsAreRemoved(string input, string expected)
    {
        Assert.Equal(expected, ZapretConfigImportService.TrimShellTail(input));
    }

    [Theory]
    [InlineData("dp")]
    [InlineData("nx")]
    [InlineData("f")]
    [InlineData("n")]
    [InlineData("x")]
    public void FileModifiersMatchCmd(string modifiers)
    {
        string file = Path.Combine(Path.GetTempPath(), "zapret", "general.bat");
        string expected = modifiers switch
        {
            "dp" => Path.GetDirectoryName(file) + Path.DirectorySeparatorChar,
            "nx" => "general.bat",
            "f" => file,
            "n" => "general",
            _ => ".bat",
        };

        string actual = ZapretConfigImportService.ExpandFileModifiers(modifiers, file);

        Assert.Equal(expected.Replace('\\', '/'), actual.Replace('\\', '/'));
    }

    [Fact]
    public void FileOutsideSourceFolderIsReported()
    {
        string root = Path.Combine(Path.GetTempPath(), "cdpiui-zapret-outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            string sourceFolder = Path.Combine(root, "A");
            string otherFolder = Path.Combine(root, "B");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(otherFolder);
            string inside = Path.Combine(sourceFolder, "inside.txt");
            string outside = Path.Combine(otherFolder, "outside.txt");
            File.WriteAllText(inside, "example.com");
            File.WriteAllText(outside, "example.org");

            ZapretConfigImportResult insideResult = new ZapretConfigImportService().Import(
                WriteWinwsBatch(sourceFolder, "inside.bat", inside));
            ZapretConfigImportResult outsideResult = new ZapretConfigImportService().Import(
                WriteWinwsBatch(sourceFolder, "outside.bat", outside));

            Assert.DoesNotContain(insideResult.Issues, i => i.Code == "REFERENCED_FILE_OUTSIDE_SOURCE");
            Assert.Contains(Path.GetFullPath(inside), insideResult.ReferencedFiles);
            Assert.Contains(outsideResult.Issues, i =>
                i.Code == "REFERENCED_FILE_OUTSIDE_SOURCE" &&
                i.Severity == ZapretConfigImportIssueSeverity.Warning);
            Assert.Contains(Path.GetFullPath(outside), outsideResult.ReferencedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteWinwsBatch(string folder, string name, string hostlist)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path,
            "@echo off\r\n" +
            $"start \"zapret\" /min \"%~dp0bin\\winws.exe\" --wf-tcp=443 --filter-tcp=443 --hostlist=\"{hostlist}\" --dpi-desync=fake\r\n");
        return path;
    }
}
