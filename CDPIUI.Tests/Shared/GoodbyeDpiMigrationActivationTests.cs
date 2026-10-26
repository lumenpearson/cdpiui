using System.Text;
using CDPIUI.Shared.Migration;

namespace CDPIUI.Tests.Shared;

public class GoodbyeDpiMigrationActivationTests
{
    private static readonly string Archive = Path.Combine(Path.GetTempPath(), "cdpiui-migration", "backup.zip");
    private static readonly string Sha = new('a', 64);
    private static readonly Guid Id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private const string Pipe = "GDPIUI-Updater-test_1";
    private static readonly string Token = Encode(new byte[32]);

    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    private static string Build(
        string version = "1",
        string? archive = null,
        string? sha = null,
        string? id = null,
        string? pipe = null,
        string? token = null) =>
        GoodbyeDpiMigrationActivation.ArgumentPrefix + string.Join('.',
            version,
            archive ?? Encode(Archive),
            sha ?? Sha,
            id ?? Id.ToString("N"),
            pipe ?? Encode(Pipe),
            token ?? Token);

    [Fact]
    public void CreatedArgumentIsParsedBack()
    {
        string argument = GoodbyeDpiMigrationActivation.CreateArgument(Archive, Sha.ToUpperInvariant(), Id, Pipe, Token);

        Assert.True(GoodbyeDpiMigrationActivation.TryParseArgument(argument, out var request));
        Assert.NotNull(request);
        Assert.Equal(GoodbyeDpiMigrationActivation.CurrentProtocolVersion, request.ProtocolVersion);
        Assert.Equal(Path.GetFullPath(Archive), request.ArchivePath);
        Assert.Equal(Sha, request.ArchiveSha256);
        Assert.Equal(Id, request.MigrationId);
        Assert.Equal(Pipe, request.ResponsePipeName);
        Assert.Equal(Token, request.SessionToken);
        Assert.Equal(argument, request.RawArgument);
    }

    [Fact]
    public void QuotesAndSpacesAroundTheArgumentAreIgnored()
    {
        Assert.True(GoodbyeDpiMigrationActivation.IsActivationArgument($"  \"{Build()}\" "));
    }

    [Fact]
    public void ArchivePathWithUnicodeIsDecoded()
    {
        string archive = Path.Combine(Path.GetTempPath(), "Резервная копия", "backup.ZIP");

        Assert.True(GoodbyeDpiMigrationActivation.TryParseArgument(Build(archive: Encode(archive)), out var request));
        Assert.Equal(Path.GetFullPath(archive), request!.ArchivePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--other=1")]
    [InlineData("--GDPIUI-MIGRATION=1.a.b.c.d.e")]
    [InlineData("--gdpiui-migration=")]
    [InlineData("--gdpiui-migration=1.2.3")]
    [InlineData("--gdpiui-migration=1.2.3.4.5.6.7")]
    [InlineData("--gdpiui-migration=1..3.4.5.6")]
    public void MalformedArgumentsAreRejected(string? argument)
    {
        Assert.False(GoodbyeDpiMigrationActivation.TryParseArgument(argument, out var request));
        Assert.Null(request);
    }

    [Fact]
    public void TooLongArgumentIsRejected()
    {
        string argument = Build(archive: Encode(new string('a', 20000) + ".zip"));

        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(argument));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("x")]
    public void OtherProtocolVersionsAreRejected(string version)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(version: version)));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void HashMustBe64LowerHexCharacters(string sha)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(sha: sha)));
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("not-a-guid")]
    public void MigrationIdMustBeAGuidWithoutDashes(string id)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(id: id)));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("YQ=")]
    [InlineData("YQ==")]
    [InlineData("Y+Q")]
    [InlineData("_w")]
    public void ArchivePathMustBeCanonicalBase64UrlOfUtf8(string archive)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(archive: archive)));
    }

    [Theory]
    [InlineData("backup.rar")]
    [InlineData("backup")]
    [InlineData("backup.zip\0.txt")]
    public void ArchiveMustBeAZipFile(string fileName)
    {
        string archive = Path.Combine(Path.GetTempPath(), fileName);

        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(archive: Encode(archive))));
    }

    [Theory]
    [InlineData("GDPIUI-Updater-")]
    [InlineData("gdpiui-updater-test")]
    [InlineData("Other-test")]
    [InlineData("GDPIUI-Updater-a b")]
    [InlineData("GDPIUI-Updater-..\\pipe")]
    public void PipeNameMustHaveThePrefixAndSafeCharacters(string pipe)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(pipe: Encode(pipe))));
    }

    [Fact]
    public void PipeNameLengthIsLimited()
    {
        Assert.True(GoodbyeDpiMigrationActivation.IsActivationArgument(
            Build(pipe: Encode(Pipe + new string('x', 180 - Pipe.Length)))));
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(
            Build(pipe: Encode(Pipe + new string('x', 181 - Pipe.Length)))));
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(32, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void SessionTokenLengthIsLimited(int length, bool valid)
    {
        Assert.Equal(valid, GoodbyeDpiMigrationActivation.IsActivationArgument(
            Build(token: Encode(new byte[length]))));
    }

    [Theory]
    [InlineData("!!!!")]
    [InlineData("A")]
    public void SessionTokenMustBeBase64Url(string token)
    {
        Assert.False(GoodbyeDpiMigrationActivation.IsActivationArgument(Build(token: token)));
    }

    [Fact]
    public void ArgumentIsFoundInAList()
    {
        Assert.True(GoodbyeDpiMigrationActivation.TryFindArgument(["--tray", Build()], out var request));
        Assert.Equal(Id, request!.MigrationId);

        Assert.False(GoodbyeDpiMigrationActivation.TryFindArgument(["--tray"], out request));
        Assert.Null(request);
        Assert.False(GoodbyeDpiMigrationActivation.TryFindArgument((IEnumerable<string>?)null, out _));
    }

    [Fact]
    public void ArgumentIsFoundInACommandLine()
    {
        Assert.True(GoodbyeDpiMigrationActivation.TryFindArgument($"--tray  {Build()}\t--x", out var request));
        Assert.Equal(Pipe, request!.ResponsePipeName);

        Assert.False(GoodbyeDpiMigrationActivation.TryFindArgument("  ", out _));
        Assert.False(GoodbyeDpiMigrationActivation.TryFindArgument((string?)null, out _));
    }

    [Fact]
    public void CreateArgumentRejectsInvalidData()
    {
        Assert.Throws<ArgumentException>(() =>
            GoodbyeDpiMigrationActivation.CreateArgument(Archive, Sha, Id, "bad pipe", Token));
        Assert.Throws<ArgumentException>(() =>
            GoodbyeDpiMigrationActivation.CreateArgument(Archive, null!, Id, Pipe, Token));
    }
}
