using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.Text;

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

        if (aotTasks.Count == 0)
        {
            _logger.LogWarning("No MonoAOTCompiler tasks found");
            return false;
        }
        if (aotTasks.Count > 1)
        {
            _logger.LogWarning("Multiple MonoAOTCompiler tasks found");
            throw new NotImplementedException("TODO: multiple MonoAOTCompiler tasks");
        }
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
        TaskModel model = TaskModel.BuildFromObjectModel(_logger, task, _assets);
        return true;
    }

    private bool CollectAotTaskFolder (Folder folder)
    {
        if (folder.Name != "Parameters") {
            _logger.LogWarning ("skipping Folder: {Folder}", folder.Name);
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
                StringBuilder sb = new StringBuilder();
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    sb.Append ("Param: ");
                    sb.Append (items.Name);
                    sb.Append ("\n");
                }
                try {
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
                } finally {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(sb.ToString());
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