using CDPIUI.AddOns.ConfigImport;

namespace CDPIUI.Tests.AddOns;

public class ConfigImportServiceTests
{
    [Theory]
    [InlineData("set LIST=--hostlist=\"%~dp0lists\\list.txt\"", "LIST", "--hostlist=\"%~dp0lists\\list.txt\"")]
    [InlineData("set \"BIN=%~dp0bin\\\"", "BIN", "%~dp0bin\\")]
    [InlineData("set \"ARGS=--hostlist=\"a b.txt\"\"", "ARGS", "--hostlist=\"a b.txt\"")]
    [InlineData("@SET PORT=443", "PORT", "443")]
    public void TryParseSetKeepsQuotesInsideValue(string line, string expectedName, string expectedValue)
    {
        Assert.True(ConfigImportService.TryParseSet(line, out string name, out string value));
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void FileOutsideSourceFolderIsReported()
    {
        string root = Path.Combine(Path.GetTempPath(), "cdpiui-import-outside-" + Guid.NewGuid().ToString("N"));
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
            var target = new ConfigImportTarget("TEST001", "ByeDPI", "ciadpi", "1.0");

            ConfigImportResult insideResult = new ConfigImportService().Import(
                WriteBatch(sourceFolder, "inside.bat", inside), target);
            ConfigImportResult outsideResult = new ConfigImportService().Import(
                WriteBatch(sourceFolder, "outside.bat", outside), target);

            Assert.DoesNotContain(insideResult.Issues, i => i.Code == "REFERENCED_FILE_OUTSIDE_SOURCE");
            Assert.Contains(Path.GetFullPath(inside), insideResult.ReferencedFiles);
            Assert.Contains(outsideResult.Issues, i =>
                i.Code == "REFERENCED_FILE_OUTSIDE_SOURCE" &&
                i.Severity == ConfigImportIssueSeverity.Warning);
            Assert.Contains(Path.GetFullPath(outside), outsideResult.ReferencedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteBatch(string folder, string name, string hostsFile)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path,
            "@echo off\r\n" +
            $"start \"\" \"%~dp0ciadpi.exe\" --hosts \"{hostsFile}\" --split 1\r\n");
        return path;
    }
}
