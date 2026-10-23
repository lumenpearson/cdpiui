using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.Tests.Shared;

public sealed class ConditionalTaskFileServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cdpiui-tasks-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(directory, true);

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
        string path = ConditionalTaskFileService.Save(task, directory);
        task.Name = "Renamed";
        ConditionalTaskFileService.Save(task, directory);

        ConditionalTask loaded = ConditionalTaskFileService.Load(path);

        Assert.Equal("Renamed", loaded.Name);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void CopiedTaskFileIsSkippedAndReported()
    {
        ConditionalTask task = CreateTask("Task");
        string path = ConditionalTaskFileService.Save(task, directory);
        File.Copy(path, Path.Combine(directory, "copy" + ConditionalTaskFileService.FileExtension));
        var errors = new List<string>();

        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(directory, (file, _) => errors.Add(file));

        Assert.Single(tasks);
        Assert.Single(errors);
    }

    [Fact]
    public void BrokenTaskFileIsReported()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "broken" + ConditionalTaskFileService.FileExtension), "<ConditionalTask");
        var errors = new List<string>();

        IReadOnlyList<ConditionalTask> tasks = ConditionalTaskFileService.LoadDirectory(directory, (file, _) => errors.Add(file));

        Assert.Empty(tasks);
        Assert.Single(errors);
    }
}
