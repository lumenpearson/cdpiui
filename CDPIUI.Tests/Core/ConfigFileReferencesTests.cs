using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Tests.Core;

public class ConfigFileReferencesTests
{
    [Fact]
    public void BlobOffsetIsNotPartOfThePath()
    {
        var files = ConfigFileReferences.ExtractFromText("--blob=tls:+4@files/tls.bin");

        var file = Assert.Single(files);
        Assert.Equal("files/tls.bin", file.Path);
    }

    [Fact]
    public void BlobReplacementKeepsNameAndOffset()
    {
        string result = ConfigFileReferences.ReplaceFileInText(
            "--blob=tls:+4@files/tls.bin", "files/tls.bin", ConfigFileKind.Payload, "C:/new/tls.bin");

        Assert.Equal("--blob=tls:+4@C:/new/tls.bin", result);
    }

    [Fact]
    public void LuaFileWithSemicolonInNameIsAFile()
    {
        var file = Assert.Single(ConfigFileReferences.ExtractFromText("--lua-init=@lua/a;b.lua"));
        Assert.Equal("lua/a;b.lua", file.Path);
        Assert.Empty(ConfigFileReferences.ExtractFromText("--lua-init=x=1;y=2"));
    }

    [Fact]
    public void NegativeNumberIsAValue()
    {
        Assert.False(ConfigCommandLine.IsOption("-10000"));
        Assert.True(ConfigCommandLine.IsOption("-p"));
    }

    [Theory]
    [InlineData("lists/hosts.txt", ConfigFileKind.SiteList)]
    [InlineData("C:\\Lists\\HOSTS.TXT", ConfigFileKind.SiteList)]
    [InlineData("ipset.list", ConfigFileKind.SiteList)]
    [InlineData("lua/init.LUA", ConfigFileKind.Library)]
    [InlineData("tls.bin", ConfigFileKind.Payload)]
    [InlineData("quic.dat", ConfigFileKind.Payload)]
    [InlineData("cert.der", ConfigFileKind.Payload)]
    [InlineData("cert.Pem", ConfigFileKind.Payload)]
    public void InferKindUsesTheExtension(string path, ConfigFileKind kind)
    {
        Assert.Equal(kind, ConfigFileReferences.InferKind(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("readme")]
    [InlineData("hosts.txt.bak")]
    [InlineData("archive.zip")]
    [InlineData("lists.txt/")]
    public void InferKindReturnsNullForOtherFiles(string path)
    {
        Assert.Null(ConfigFileReferences.InferKind(path));
    }
}
