using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.Text;

/// <summary>
///  A scraper for projects that use the Mono AOT Compiler task
/// </summary>
public class RuntimeAotCompilerScraper : ITaskScraper, TaskModel.IBuilderCallback
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
        var builder = new TaskModel.Builder(_logger, _assets, this);
        if (!builder.Create(task))
        {
            return false;
        }
        CreateGeneratedAssets(builder.Model);
        return true;
    }

    private void CreateGeneratedAssets(TaskModel model)
    {
        // TODO: generate a .csproj that calls the task
        return;
    }

    bool TaskModel.IBuilderCallback.HandleSpecialTaskParameter(TaskModel.IBuilderCallbackCallback builder, Parameter parameter)
    {
        return false;
    }

    bool TaskModel.IBuilderCallback.HandleSpecialTaskProperty(TaskModel.IBuilderCallbackCallback builder, Property property)
    {
        return false;
    }

}