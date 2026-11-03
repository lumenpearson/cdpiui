using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.Tests.Shared;

public sealed class ConditionalTaskFileServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cdpiui-tasks-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private static ConditionalTask CreateTask(string name) => new()
    {
        Name = name,
        Triggers = [new ConditionalTrigger()],
        Actions = [new ConditionalAction()],
    };

    [Fact]
    public void SavedTaskLoadsBackWithoutTemporaryFiles()
    {
        ConditionalTask task = CreateTask("First");
        string path = ConditionalTaskFileService.Save(task, _directory);
        task.Name = "Renamed";
        ConditionalTaskFileService.Save(task, _directory);

        ConditionalTask loaded = ConditionalTaskFileService.Load(path);

        Assert.Equal("Renamed", loaded.Name);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void CopiedTaskFileIsSkippedAndReported()
    {
        ConditionalTask task = CreateTask("Task");
        string path = ConditionalTaskFileService.Save(task, _directory);
        File.Copy(path, Path.Combine(_directory, "copy" + ConditionalTaskFileService.FileExtension));
        var errors = new List<string>();

        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(_directory, (file, _) => errors.Add(file));

        Assert.Single(tasks);
        Assert.Single(errors);
    }

    [Fact]
    public void BrokenTaskFileIsReported()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "broken" + ConditionalTaskFileService.FileExtension), "<ConditionalTask");
        var errors = new List<string>();

        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(_directory, (file, _) => errors.Add(file));

        Assert.Empty(tasks);
        Assert.Single(errors);
    }

    [Fact]
    public void LoadingMissingDirectoryDoesNotCreateIt()
    {
        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(_directory);

        Assert.Empty(tasks);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void OversizedTaskFileIsRejectedBeforeDeserialization()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "large" + ConditionalTaskFileService.FileExtension);
        File.WriteAllText(path, new string('x', 1024 * 1024 + 1));

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Load(path));
    }

    [Fact]
    public void TooManyTaskFilesFailClosed()
    {
        Directory.CreateDirectory(_directory);
        for (var index = 0; index <= 256; index++)
            File.WriteAllText(Path.Combine(_directory, $"task-{index:D3}{ConditionalTaskFileService.FileExtension}"), "");
        var errors = new List<string>();

        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(
            _directory,
            (file, _) => errors.Add(file));

        Assert.Empty(tasks);
        Assert.Single(errors);
    }

    [Fact]
    public void DtdTaskFileIsRejected()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "dtd" + ConditionalTaskFileService.FileExtension);
        File.WriteAllText(path,
            "<!DOCTYPE ConditionalTask [<!ENTITY x \"value\">]><ConditionalTask>&x;</ConditionalTask>");

        Exception exception = Assert.ThrowsAny<Exception>(() => ConditionalTaskFileService.Load(path));

        Assert.Contains("DTD", exception.ToString());
    }

    [Fact]
    public void UnsupportedTriggerTypeIsRejected()
    {
        ConditionalTask task = CreateTask("Task");
        task.Triggers[0].Type = (ConditionalTriggerType)999;

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Validate(task));
    }

    [Fact]
    public void UnsupportedActionTypeIsRejected()
    {
        ConditionalTask task = CreateTask("Task");
        task.Actions[0].Type = (ConditionalActionType)999;

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Validate(task));
    }

    [Fact]
    public void NullActionIsRejected()
    {
        ConditionalTask task = CreateTask("Task");
        task.Actions.Add(null!);

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Validate(task));
    }

    [Fact]
    public void NullActionParameterIsRejected()
    {
        ConditionalTask task = CreateTask("Task");
        task.Actions[0].Parameters.Add(null!);

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Validate(task));
    }

    [Fact]
    public void NullTriggerParameterIsRejected()
    {
        ConditionalTask task = CreateTask("Task");
        task.Triggers[0].Parameters.Add(null!);

        Assert.Throws<InvalidDataException>(() => ConditionalTaskFileService.Validate(task));
    }
}
