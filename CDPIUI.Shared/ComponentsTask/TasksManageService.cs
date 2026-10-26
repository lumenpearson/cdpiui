using CDPIUI.Shared.PrettyErrorConversionService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Shared.ComponentsTask
{
    /// <summary>
    /// Default tasks management service for <see cref="IProcessService"/>
    /// </summary>
    /// <typeparam name="T">Process service</typeparam>
    public class TasksManageService<T> where T : IProcessService, new()
    {
        /// <summary>
        /// Tasks list
        /// </summary>
        public readonly List<TaskModel<T>> Tasks = [];

        /// <summary>
        /// Invokes where some task added or removed
        /// </summary>
        public Action? TaskListUpdated;

        /// <summary>
        /// Invokes when task with <see cref="string"/> Id changed <see cref="bool"/> State
        /// </summary>
        public Action<Tuple<string, bool>>? TaskStateUpdated;

        /// <summary>
        /// Invokes when task with <see cref="string"/> Id changes <see cref="bool"/> SetupState
        /// </summary>
        public Action<Tuple<string, bool>>? TaskSetupStateUpdated;

        /// <summary>
        /// Show error message for task id.
        /// </summary>
        public Action<string>? ShowErrorMessageForTaskId;

        public TasksManageService() { }

        private readonly SemaphoreSlim _taskOperationLock = new(1, 1);

        /// <summary>
        /// Set lock; get <see cref="TaskModel{T}"/> from <see cref="string"/> Id; then release lock
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns>Requested <see cref="TaskModel{T}"/> if task with <see cref="TaskModel{T}.Id"/> exist, otherwise <see cref="null"/></returns>
        public async Task<TaskModel<T>?> GetTaskFromId(string id)
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                var existTask = Tasks.FirstOrDefault(t => t.Id == id);
                if (existTask != null)
                {
                    return existTask;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Tasks is public, so it can be changed without the lock while it is searched.
                Trace.WriteLine($"[TasksManageService] Cannot find the task {id}: {ex.Message}");
            }
            finally
            {
                _taskOperationLock.Release();
            }
            return null;
        }

        private async Task<(TaskModel<T> Task, bool Created)> GetOrAddTask(string id, bool? isSetupComplete = null)
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                var existTask = Tasks.FirstOrDefault(t => t.Id == id);
                if (existTask != null)
                    return (existTask, false);

                T processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;

                var task = new TaskModel<T> { Id = id, ProcessManager = processManager, IsSetupComplete = isSetupComplete };
                Tasks.Add(task);
                return (task, true);
            }
            finally
            {
                _taskOperationLock.Release();
            }
        }

        /// <summary>
        /// Add new task to task list. Catches and logs its own errors, so a caller can start it without waiting for it.
        /// </summary>
        /// <param name="id">Task Id</param>
        public async Task AddNewTask(string id)
        {
            try
            {
                await AddNewTaskAsync(id);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TasksManageService] Cannot add the task {id}: {ex}");
            }
        }

        /// <summary>
        /// Add new task to task list
        /// </summary>
        /// <param name="id">Task Id</param>
        public async Task AddNewTaskAsync(string id)
        {
            var (_, created) = await GetOrAddTask(id);
            if (created)
                TaskListUpdated?.Invoke();
        }

        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/>
        /// </summary>
        /// <param name="id">Task Id</param>
        public async Task CreateAndRunNewTask(string id)
        {
            var (task, created) = await GetOrAddTask(id);
            if (created)
                TaskListUpdated?.Invoke();
            else
                await task.ProcessManager.StopProcess();

            await task.ProcessManager.StartProcess();
        }
        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/> with custom executable
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="executable">Executable</param>
        /// <param name="args">Command-line flags</param>
        public virtual async Task CreateAndRunNewTask(string id, string executable, string args)
        {
            var (task, created) = await GetOrAddTask(id);
            if (created)
                TaskListUpdated?.Invoke();
            else
                await task.ProcessManager.StopProcess();

            await task.ProcessManager.StartProcess(executable, args);
        }

        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/> only with Id
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="args">Command-line flags</param>
        public async Task CreateAndRunNewTask(string id, string args)
        {
            var (task, created) = await GetOrAddTask(id);
            if (created)
                TaskListUpdated?.Invoke();
            else
                await task.ProcessManager.StopProcess();

            await task.ProcessManager.StartProcess(args, null);
        }

        /// <summary>
        /// Set setup status for task
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="isSetupComplete">Is task with <see cref="string"/> Id ready to run</param>
        /// <remarks>Catches and logs its own errors, so a caller can start it without waiting for it.</remarks>
        public async Task SetTaskStatus(string id, bool isSetupComplete)
        {
            try
            {
                var (targetTask, created) = await GetOrAddTask(id, isSetupComplete);
                if (created)
                    TaskListUpdated?.Invoke();
                else
                    targetTask.IsSetupComplete = isSetupComplete;

                TaskSetupStateUpdated?.Invoke(Tuple.Create(id, isSetupComplete));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TasksManageService] Cannot set the setup status of the task {id}: {ex}");
            }
        }

        /// <summary>
        /// Apply setup status to all existed tasks
        /// </summary>
        /// <param name="status">Target status</param>
        /// <remarks>Catches and logs its own errors, so a caller can start it without waiting for it.</remarks>
        public async Task ApplyStatusToAllTasks(bool status)
        {
            try
            {
                foreach (var task in await GetTasksSnapshot())
                {
                    task.IsSetupComplete = status;
                    TaskSetupStateUpdated?.Invoke(Tuple.Create(task.Id, status));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TasksManageService] Cannot apply the setup status to the tasks: {ex}");
            }
        }

        /// <summary>
        /// Stop task asynchronously
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
        public async Task StopTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.StopProcess();
            }
        }

        /// <summary>
        /// Stop task asynchronously, then remove it from tasks list
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
        public async Task StopAndRemoveTaskAsync(string id)
        {
            TaskModel<T>? existTask;
            // Found and removed in one lock hold; the stop runs after it, so other task
            // operations do not wait for the process to exit.
            await _taskOperationLock.WaitAsync();
            try
            {
                existTask = Tasks.FirstOrDefault(t => t.Id == id);
                if (existTask != null)
                    Tasks.Remove(existTask);
            }
            finally { _taskOperationLock.Release(); }

            if (existTask == null)
                return;

            try
            {
                await existTask.ProcessManager.StopProcess();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TasksManageService] Cannot stop and remove the task {id}: {ex}");
            }
            TaskListUpdated?.Invoke();
        }

        /// <summary>
        /// Restart task asynchronously
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
        public async Task RestartTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.RestartProcess();
            }
        }


        /// <summary>
        /// Stop all tasks asynchronously
        /// </summary>
        public async Task StopAllTasks()
        {
            foreach (var task in await GetTasksSnapshot())
            {
                try
                {
                    await task.ProcessManager.StopProcess();
                }
                catch (Exception ex)
                {
                    // One failed task must not keep the other tasks running.
                    Trace.WriteLine($"[TasksManageService] Cannot stop the task {task.Id}: {ex}");
                }
            }
        }

        /// <summary>
        /// Get a copy of the tasks list that is safe to enumerate while tasks are added or removed
        /// </summary>
        public async Task<List<TaskModel<T>>> GetTasksSnapshot()
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                return [.. Tasks];
            }
            finally
            {
                _taskOperationLock.Release();
            }
        }

        /// <summary>
        /// Get a copy of the tasks list without awaiting, for callers that cannot await, such as UI code
        /// </summary>
        /// <remarks>Blocks while another task operation holds the lock; those operations do not wait on anything under it.</remarks>
        public List<TaskModel<T>> GetTasksSnapshotSync()
        {
            _taskOperationLock.Wait();
            try
            {
                return [.. Tasks];
            }
            finally
            {
                _taskOperationLock.Release();
            }
        }

        /// <summary>
        /// Is task wuth <see cref="string"/> Id running
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns>true if running, otherwise false</returns>
        public async Task<bool> IsTaskRunning(string id)
        {
            var task = await GetTaskFromId(id);
            if (task == null) return false;

            return task.ProcessManager.IsProcessRunning;
        }

        private void HandleProcessStateUpdate(Tuple<string, bool> tuple)
        {
            TaskStateUpdated?.Invoke(tuple);
        }
        private void HandleShowErrorMessageWindow(string id)
        {
            ShowErrorMessageForTaskId?.Invoke(id);
        }
    }
}
