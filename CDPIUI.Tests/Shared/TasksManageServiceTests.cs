using CDPIUI.Shared.ComponentsTask;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Tests.Shared;

public class TasksManageServiceTests
{
    private sealed class FakeProcess : IProcessService
    {
        public string Id { get; set; } = string.Empty;
        public string ProcessName => Id;
        public bool IsProcessRunning { get; private set; }
        public bool IsErrorHappens => false;
        public ErrorModel? LastError => null;
        public int StartCount { get; private set; }

        // The fake raises no events; empty accessors satisfy the interface.
        public event Action<Tuple<string, bool>>? ProcessStateChanged { add { } remove { } }
        public event Action<ErrorModel>? ErrorHappens { add { } remove { } }
        public event Action<string>? OutputReceived { add { } remove { } }
        public event Action<string>? ProcessNameChanged { add { } remove { } }
        public event Action<string>? ShowErrorMessageWindow { add { } remove { } }

        public async Task StartProcess()
        {
            StartCount++;
            // Completes asynchronously, so concurrent callers interleave; no wall-clock wait.
            await Task.Yield();
            IsProcessRunning = true;
        }

        public Task StartProcess(string stringParameter1, string? stringParameter2) => StartProcess();

        public Task StopProcess(bool boolParameter = true)
        {
            IsProcessRunning = false;
            return Task.CompletedTask;
        }

        public Task RestartProcess() => StartProcess();
    }

    [Fact]
    public async Task ConcurrentCreationKeepsSingleTaskPerId()
    {
        var service = new TasksManageService<FakeProcess>();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.CreateAndRunNewTask("component")));

        var tasks = await service.GetTasksSnapshot();
        Assert.Single(tasks);
        Assert.True(tasks[0].ProcessManager.IsProcessRunning);
    }

    [Fact]
    public async Task StopAllTasksStopsEveryTask()
    {
        var service = new TasksManageService<FakeProcess>();
        await service.CreateAndRunNewTask("a");
        await service.CreateAndRunNewTask("b");

        await service.StopAllTasks();

        Assert.All(await service.GetTasksSnapshot(), task => Assert.False(task.ProcessManager.IsProcessRunning));
    }

    [Fact]
    public async Task ExistingTaskIsReusedOnRestart()
    {
        var service = new TasksManageService<FakeProcess>();
        await service.CreateAndRunNewTask("a");
        await service.CreateAndRunNewTask("a");

        var tasks = await service.GetTasksSnapshot();
        Assert.Single(tasks);
        Assert.Equal(2, tasks[0].ProcessManager.StartCount);
    }
}
