using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;

/// <summary>
/// Reconstructed <Task/> element from a .csproj file based on
/// available binlog data, with file-like attributes represented as assets
/// </summary>
public class TaskModel
{
    internal readonly struct TaskProperty
    {
        public string Name {get; set; }
        public string? StringValue {get; set; }
        public AssetRepository.AssetPath? AssetValue {get; set; }
    }

    private readonly AssetRepository _assets;

    private AssetRepository.AssetPath AssemblyPath {get; }

    public TaskModel(AssetRepository assets, AssetRepository.AssetPath assemblyPath)
    {
        _assets = assets;
        AssemblyPath = assemblyPath;
    }

    public static TaskModel BuildFromObjectModel(ILogger logger, Microsoft.Build.Logging.StructuredLogger.Task task, AssetRepository assets)
    {
        // Based on https://github.com/KirillOsenkov/MSBuildStructuredLog/tree/main/src/TaskRunner/Executor.cs
        Project? parentProject = task.GetNearestParent<Project>();
        if (parentProject == null)
        {
            logger.LogError("Task {Task} has no parent project", task.ToString());
            throw new InvalidOperationException("Task has no parent project");
        }
        logger.LogDebug("Setting parent project to {Project}", parentProject.ProjectFile);
        using var _ = assets.BeginProject(parentProject.ProjectFile);
        if (task.IsDerivedTask)
        {
            logger.LogWarning("Task {Task} is a derived task", task.ToString());
        }
        AssetRepository.AssetPath assemblyPath = assets.GetOrAddToolingAsset(task.FromAssembly, AssetRepository.AssetKind.ToolingAssembly);
        using var _1 = assets.BeginAotCompilation(task.Name);
        var model = new TaskModel(assets, assemblyPath);
        return model;
    }

    #if false
            Project? parentProject = task.GetNearestParent<Project>();
        if (parentProject == null)
        {
            _logger.LogError("Task {Task} has no parent project", task.ToString());
            return false;
        }
        _logger.LogDebug("Setting parent project to {Project}", parentProject.ProjectFile);
        using var _ = _assets.BeginProject(parentProject.ProjectFile);
        foreach (var taskChild in task.Children)
        {
            if (taskChild is Property property)
            {
                if (!CollectAotTaskProperty(property))
                    return false;
            }
            else if (taskChild is Folder folder)
            {
                if (!CollectAotTaskFolder(folder))
                    return false;
            }
            else if (taskChild is Message message) {
                _logger.LogTrace ("Skipping message {Message}", message.ToString());
            }
            else
            {
                Console.Error.WriteLine ($"unexpected task child {taskChild} : {taskChild.GetType()}");
                return false;
            }
        }
#endif
}