using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace CDPIUI.Shared.ConditionalLaunch
{
    public enum ConditionalTaskPriority
    {
        Low = -1,
        Default = 0,
        High = 1
    }

    public enum ConditionalTriggerType
    {
        HotKey,
        ProcessStarted,
        ProcessStopped
    }

    [Flags]
    public enum ConditionalHotKeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    public enum ConditionalActionType
    {
        ApplyPreset,
        StartComponent,
        StopComponent,
        RestartComponent,
        StartAutorunComponents,
        StopAllComponents,
        StopNetworkService,
        CheckApplicationUpdates,
        CheckStoreUpdates,
        RunCompatibilityCheck,
        RunBasicDiagnostics,
        RunStoreDiagnostics,
        OpenMainPage,
        OpenStorePage,
        OpenTool,
        OpenHelp,
        Wait,
        ShowNotification,
        ToggleComponent
    }

    public sealed class ConditionalParameter
    {
        [XmlAttribute("Name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("Value")]
        public string Value { get; set; } = string.Empty;
    }

    public abstract class ConditionalParameterizedModel
    {
        [XmlElement("Parameter")]
        public List<ConditionalParameter> Parameters { get; set; } = [];

        public string? GetParameter(string name)
        {
            return Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public void SetParameter(string name, string? value)
        {
            var parameter = Parameters.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(value))
            {
                if (parameter != null)
                    Parameters.Remove(parameter);
                return;
            }

            if (parameter == null)
            {
                Parameters.Add(new ConditionalParameter
                {
                    Name = name,
                    Value = value
                });
                return;
            }

            parameter.Value = value;
        }
    }

    public sealed class ConditionalTrigger : ConditionalParameterizedModel
    {
        [XmlAttribute("Type")]
        public ConditionalTriggerType Type { get; set; }

        [XmlAttribute("DelaySeconds")]
        public int DelaySeconds { get; set; } = 30;
    }

    public sealed class ConditionalAction : ConditionalParameterizedModel
    {
        [XmlAttribute("Type")]
        public ConditionalActionType Type { get; set; }
    }

    [XmlRoot("ConditionalTask")]
    public sealed class ConditionalTask
    {
        public const int CurrentVersion = 3;

        [XmlAttribute("Version")]
        public int Version { get; set; } = CurrentVersion;

        [XmlAttribute("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("D");

        [XmlElement("Name")]
        public string Name { get; set; } = "New conditional task";

        [XmlElement("Enabled")]
        public bool IsEnabled { get; set; } = true;

        [XmlElement("StopAfterError")]
        public bool StopAfterError { get; set; } = true;

        [XmlIgnore]
        public ConditionalTaskPriority Priority { get; set; } = ConditionalTaskPriority.Default;

        [XmlElement("Priority")]
        public int SerializedPriority
        {
            get => (int)Priority;
            set => Priority = (ConditionalTaskPriority)value;
        }

        [XmlArray("Triggers")]
        [XmlArrayItem("Trigger")]
        public List<ConditionalTrigger> Triggers { get; set; } = [];

        // Version 1 stored a single Trigger directly under ConditionalTask.
        // It is consumed during normalization and is never written by version 2.
        [XmlElement("Trigger")]
        public ConditionalTrigger? LegacyTrigger { get; set; }

        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<ConditionalAction> Actions { get; set; } = [];

        [XmlIgnore]
        public string? FilePath { get; set; }
    }

    public static class ConditionalTaskFileService
    {
        public const string FileExtension = ".cdpitask";
        public const string DirectoryName = "ConditionalTasks";

        private static readonly XmlSerializer Serializer = new(typeof(ConditionalTask));

        public static string GetTasksDirectoryFromSettingsFile(string settingsFilePath)
        {
            var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFilePath));
            var dataDirectory = settingsDirectory == null
                ? null
                : Directory.GetParent(settingsDirectory)?.FullName;

            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new InvalidOperationException("Cannot resolve the conditional tasks directory.");

            return Path.Combine(dataDirectory, DirectoryName);
        }

        /// <param name="onError">Receives the path and the error of every file that is skipped</param>
        public static IReadOnlyList<ConditionalTask> LoadDirectory(string directoryPath, Action<string, Exception>? onError = null)
        {
            Directory.CreateDirectory(directoryPath);

            var tasks = new List<ConditionalTask>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Sorted, so the same copy of a duplicated task wins on every load.
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, $"*{FileExtension}")
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    ConditionalTask task = Load(filePath);
                    // A copied task file would otherwise run the same task twice.
                    if (!ids.Add(task.Id))
                        throw new InvalidDataException($"Another task file already has the identifier {task.Id}.");
                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    // Invalid task files are skipped. The editor reports import errors to the user separately.
                    onError?.Invoke(filePath, ex);
                }
            }

            return tasks;
        }

        public static ConditionalTask Load(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            if (Serializer.Deserialize(stream) is not ConditionalTask task)
                throw new InvalidDataException("The file does not contain a conditional task.");

            Normalize(task);
            Validate(task);
            task.FilePath = Path.GetFullPath(filePath);
            return task;
        }

        public static string Save(ConditionalTask task, string directoryPath, string? filePath = null)
        {
            Validate(task);
            task.Version = ConditionalTask.CurrentVersion;
            Directory.CreateDirectory(directoryPath);

            filePath ??= task.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) ||
                !string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.Combine(directoryPath, $"{task.Id}{FileExtension}");
            }

            filePath = Path.GetFullPath(filePath);
            var targetDirectory = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(targetDirectory, StringComparison.OrdinalIgnoreCase))
                filePath = Path.Combine(directoryPath, $"{task.Id}{FileExtension}");

            WriteTask(task, filePath);

            task.FilePath = filePath;
            return filePath;
        }

        // Writes next to the target and swaps the file in, so a crash never leaves a truncated task.
        private static void WriteTask(ConditionalTask task, string filePath)
        {
            string tempPath = filePath + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Serializer.Serialize(stream, task);
                    stream.Flush(true);
                }

                if (File.Exists(filePath))
                    File.Replace(tempPath, filePath, null);
                else
                    File.Move(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        public static void Export(ConditionalTask task, string filePath)
        {
            Validate(task);
            task.Version = ConditionalTask.CurrentVersion;
            if (!string.Equals(Path.GetExtension(filePath), FileExtension, StringComparison.OrdinalIgnoreCase))
                filePath += FileExtension;

            WriteTask(task, Path.GetFullPath(filePath));
        }

        public static void Validate(ConditionalTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (task.Version > ConditionalTask.CurrentVersion)
                throw new InvalidDataException("The conditional task file version is not supported.");
            if (!Guid.TryParse(task.Id, out _))
                throw new InvalidDataException("The conditional task identifier is invalid.");
            if (string.IsNullOrWhiteSpace(task.Name))
                throw new InvalidDataException("The conditional task name is required.");
            if ((int)task.Priority is < -1 or > 1)
                throw new InvalidDataException("The conditional task priority is invalid.");
            Normalize(task);
            if (task.Triggers.Count == 0)
                throw new InvalidDataException("At least one conditional task trigger is required.");
            if (task.Triggers.Any(trigger => trigger == null))
                throw new InvalidDataException("The conditional task contains an invalid trigger.");
            if (task.Triggers.Any(trigger => trigger.DelaySeconds is < 0 or > 86400))
                throw new InvalidDataException("The process trigger delay must be between 0 and 86400 seconds.");
            if (task.Actions == null || task.Actions.Count == 0)
                throw new InvalidDataException("At least one action is required.");
        }

        private static void Normalize(ConditionalTask task)
        {
            task.Triggers ??= [];
            if (task.Triggers.Count == 0 && task.LegacyTrigger != null)
                task.Triggers.Add(task.LegacyTrigger);
            task.LegacyTrigger = null;
        }
    }
}
