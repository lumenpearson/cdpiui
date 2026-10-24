using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Tests.AddOns;

/// <summary>
/// Imports Flowseal style batch files from TestData and compares the result with the expected
/// files next to them.
/// </summary>
public sealed class ConfigImportGoldenTests : IDisposable
{
    private static readonly ConfigImportTarget WinwsTarget = new("ASGKOI001", "zapret", "winws", "72.0");

    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), "cdpiui-import-golden-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDirectory))
            Directory.Delete(_workDirectory, recursive: true);
    }

    public static TheoryData<string, string> Cases => new()
    {
        { "FlowsealGeneral", "general.bat" },
        { "FlowsealAlt", "general (ALT).bat" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ZapretImporterMatchesExpectedFile(string folder, string batchFile)
    {
        GoldenFile expected = GoldenFile.Load("ConfigImport", folder, GetExpectedFileName(batchFile, "zapret"));
        string batchPath = PrepareCase(folder, batchFile, expected);

        ZapretConfigImportResult result = new ZapretConfigImportService().Import(batchPath);

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Equal(expected.Single("generation"), result.Generation.ToString());
        AssertConfig(expected, result.Config!);
        AssertIssues(expected, result.Issues.Select(i => i.Code));
        AssertReferencedFiles(expected, result.ReferencedFiles);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ConfigImportServiceMatchesExpectedFile(string folder, string batchFile)
    {
        GoldenFile expected = GoldenFile.Load("ConfigImport", folder, GetExpectedFileName(batchFile, "import"));
        string batchPath = PrepareCase(folder, batchFile, expected);

        ConfigImportResult result = new ConfigImportService().Import(batchPath, WinwsTarget);

        Assert.True(result.IsSuccessful, string.Join("; ", result.Issues.Select(i => i.Message)));
        AssertConfig(expected, result.Config!);
        AssertIssues(expected, result.Issues.Select(i => i.Code));
        AssertReferencedFiles(expected, result.ReferencedFiles);
    }

    private static string GetExpectedFileName(string batchFile, string importer) =>
        $"{Path.GetFileNameWithoutExtension(batchFile)}.{importer}.expected.txt";

    /// <summary>
    /// Copies the batch file into a work folder and creates the referenced resources next to it.
    /// </summary>
    private string PrepareCase(string folder, string batchFile, GoldenFile expected)
    {
        Directory.CreateDirectory(_workDirectory);
        string batchPath = Path.Combine(_workDirectory, batchFile);
        File.Copy(GoldenFile.GetTestDataPath("ConfigImport", folder, batchFile), batchPath);

        foreach (string resource in expected.Lines("referenced"))
        {
            string path = Path.Combine(_workDirectory, resource);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "test");
        }
        return batchPath;
    }

    private static void AssertConfig(GoldenFile expected, ConfigItem config)
    {
        Assert.NotNull(config);
        Assert.Equal(expected.Single("name"), config.name);
        Assert.Equal(expected.Lines("target"), config.target);
        Assert.NotNull(config.variables);
        Assert.Equal(
            expected.Lines("variables").Order(StringComparer.Ordinal),
            config.variables.Order(StringComparer.Ordinal));
        Assert.Equal(expected.CommandLine("startup"), config.startup_string);
    }

    // The batch files build resource paths from %~dp0 with backslashes, so the resources are found
    // only on Windows. On other systems the import reports them as missing.
    private static void AssertIssues(GoldenFile expected, IEnumerable<string> codes)
    {
        if (!OperatingSystem.IsWindows())
            codes = codes.Where(code => code != "REFERENCED_FILE_NOT_FOUND");
        Assert.Equal(expected.Lines("issues"), codes.Order(StringComparer.Ordinal));
    }

    private void AssertReferencedFiles(GoldenFile expected, IEnumerable<string> referencedFiles)
    {
        if (!OperatingSystem.IsWindows())
            return;
        Assert.Equal(
            expected.Lines("referenced"),
            referencedFiles
                .Select(path => Path.GetRelativePath(_workDirectory, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal));
    }
}
