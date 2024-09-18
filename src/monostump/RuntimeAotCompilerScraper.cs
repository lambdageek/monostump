using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;

/// <summary>
///  A scraper for projects that use the Mono AOT Compiler task
/// </summary>
public class RuntimeAotCompilerScraper : ITaskScraper
{
    private readonly ILogger _logger;
    private readonly AssetRepository _assets;
    public TreeNode Root { get; }
    public RuntimeAotCompilerScraper(ILogger logger, AssetRepository assets, TreeNode root)
    {
        _logger = logger;
        _assets = assets;
        Root = root;
    }

    public bool CollectAllAssets()
    {
        IReadOnlyList<Microsoft.Build.Logging.StructuredLogger.Task> aotTasks = Root.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "MonoAOTCompiler");

        foreach (var t in aotTasks)
        {
            if (!CollectAotTaskAssets(t))
            {
                return true;
            }
        }

        return true;
    }

    private bool CollectAotTaskAssets(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        if (!CollectAotTaskParams(task))
            return false;
        if (!CollectAotTaskMessages(task))
            return false;
        return true;
    }

    private bool CollectAotTaskParams(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
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
        return true;
    }

    private bool CollectAotTaskFolder (Folder folder)
    {
        if (folder.Name != "Parameters") {
            Console.WriteLine ("folder: {folder.Name}");
            return false;
        }
        foreach (var param in folder.Children)
        {
            if (param is Property property)
            {
                if (!CollectAotTaskProperty(property))
                    return false;
            } 
            else if (param is Microsoft.Build.Logging.StructuredLogger.Parameter items )
            {
                Console.WriteLine ($"Param: {items.Name}");
                foreach (var item in items.Children)
                {
                    if (item is Item itemProperty)
                    {
                        Console.WriteLine ($"  {itemProperty.Name} ");
                        _assets.AddInputAsset(itemProperty.Name, AssetRepository.AssetKind.InputAssembly);
                        foreach (var metadata in itemProperty.Children)
                        {
                            if (metadata is Metadata metadataProperty)
                            {
                                Console.WriteLine ($"    {metadataProperty.Name} = {metadataProperty.Value}");
                            }
                            else
                            {
                                Console.Error.WriteLine ($"unexpected child {metadata}");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine ($"unexpected child {item}");
                        return false;
                    }
                }
            }
            else {
                Console.Error.WriteLine ($"unexpected child {param} : {param.GetType()}");
                return false;
            }
        }
        return true;
    }

    internal static class TaskPropertyConstants
    {
        public const string Assembly = nameof(Assembly);

    }

    private bool CollectAotTaskProperty(Property property)
    {
        Console.WriteLine ($"Property: {property.Name} = {property.Value}");
        switch (property.Name)
        {
            case nameof(TaskPropertyConstants.Assembly): 
                _assets.AddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingAssembly);
                break;
            default:
                _logger.LogDebug("Skipping property {Property}", property.ToString());
                break;

        }
        return true;
    }

    private bool CollectAotTaskMessages(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
#if false 
        int foundExecMessage = 0;
        var messageScraper = new MonoAotCrossCommandLineScraper();
        foreach (var child in task.FindChildrenRecursive<Message>())
        {
            if (!messageScraper.ScrapeAotCompilerOutputMessage(child.Text))
            {
                continue;
            } else {
                foundExecMessage++;
            }
        }
        if (foundExecMessage == 0)
            return false;
#endif
        return true;
    }

}