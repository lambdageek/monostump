using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.Text;

/// <summary>
/// Reconstructed <Task/> element from a .csproj file based on
/// available binlog data, with file-like attributes represented as assets
/// </summary>
public class TaskModel
{
    public readonly struct TaskProperty
    {
        public string Name {get; init; }
        public string? StringValue {get; init; }
        public AssetRepository.AssetPath? AssetValue {get; init; }
    }

    public readonly struct TaskParameter
    {
        public string Name {get; init; }
        public List<TaskItem> Items { get; init;}
    }

    public readonly struct TaskItem
    {
        public string? StringValue {get; init; }
        public AssetRepository.AssetPath? AssetValue {get; init;}
        public List<TaskMetadata> Metadata { get; init; }
    }

    public readonly struct TaskMetadata
    {
        public string Name {get; init; }
        public string? StringValue {get; init; }
        public AssetRepository.AssetPath? AssetValue {get; init; }
    }

    public readonly struct TaskOutputItem
    {
        public string Name {get; init; }
        public bool IsProperty { get; init; } // true if $(property), false if @(Item)
        // not going to save the actual outputs here
    }

    private readonly AssetRepository _assets;

    public List<TaskProperty> Properties {get; } = new List<TaskProperty>();
    public List<TaskParameter> Parameters {get; } = new List<TaskParameter>();

    public List<TaskOutputItem> OutputItems { get; } = new List<TaskOutputItem>();
    private AssetRepository.AssetPath AssemblyPath {get; }
    public string Name { get; }

    const string Condition = nameof(Condition);

    public TaskModel(AssetRepository assets, AssetRepository.AssetPath assemblyPath, string name)
    {
        _assets = assets;
        AssemblyPath = assemblyPath;
        Name = name;
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
        var model = new TaskModel(assets, assemblyPath, task.Name);
        model.PopulateParameters(logger, task);
        model.PopulateOutputItems(logger, task);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(model.DumpModel().ToString());
        }
        return model;
    }

    
    
    private void PopulateParameters(ILogger logger, Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        Folder? parametersFolder = task.FindChild<Folder>(nameof(Parameters));
        if (parametersFolder == null)
        {
            logger.LogError("Task {Task} has no Parameters folder", task.ToString());
            throw new InvalidOperationException("Task has no Parameters folder");
        }
        foreach (var child in parametersFolder.Children)
        {
            switch (child)
            {
                case Property property:
                    PopulateProperty(logger, property);
                    break;
                case Parameter parameter:
                    PopulateParameter(logger, parameter);
                    break;
                default:
                    logger.LogError("Unexpected node {Node} type {NodeType}", child.ToString(), child.GetType());
                    throw new NotSupportedException($"Unexpected node type {child.GetType()}");
            }
        }
    }

    private void PopulateProperty(ILogger logger, Property property)
    {
        const string Assembly = nameof(Assembly);
        logger.LogDebug ("Property: {PropertyName} = {PropertyValue}", property.Name, property.Value);
        switch (property.Name)
        {
            case Assembly: 
                AssetRepository.AssetPath asmPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingAssembly);
                Properties.Add(new TaskProperty { Name = property.Name, AssetValue = asmPath });
                break;
            default:
                // TODO: handle other known properties
                Properties.Add(new TaskProperty { Name = property.Name, StringValue = property.Value });
                break;

        }

    }

    private void PopulateParameter(ILogger logger, Parameter parm)
    {
        logger.LogDebug ("Parameter: {ParameterName}", parm.Name);
        AssetRepository.AssetKind? assetParm = parm.Name == "Assemblies" ? AssetRepository.AssetKind.InputAssembly : null; // TODO: handle other known parameters
        List<TaskItem> items = new List<TaskItem>();
        foreach (var item in parm.Children)
        {
            if (item is Item itemProperty)
            {
                List<TaskMetadata> metadata = new List<TaskMetadata>();
                foreach (var metadataProperty in itemProperty.Children)
                {
                    if (metadataProperty is Metadata metadataProp)
                    {
                        TaskMetadata taskMetadata = new TaskMetadata { Name = metadataProp.Name, StringValue = metadataProp.Value };
                        metadata.Add(taskMetadata);
                    }
                    else
                    {
                        logger.LogError("Unexpected node {Node} type {NodeType}", metadataProperty.ToString(), metadataProperty.GetType());
                        throw new NotSupportedException($"Unexpected node type {metadataProperty.GetType()}");
                    }
                }
                TaskItem taskItem;
                if (!assetParm.HasValue)
                {
                    taskItem = new TaskItem { StringValue = itemProperty.Name , Metadata = metadata};
                }
                else
                {
                    taskItem = new TaskItem {
                        AssetValue = _assets.GetOrAddInputAsset(itemProperty.Name, assetParm.Value),
                        Metadata = metadata,
                    };
                }
                items.Add(taskItem);
            }
            else
            {
                logger.LogError("Unexpected node {Node} type {NodeType}", item.ToString(), item.GetType());
                throw new NotSupportedException($"Unexpected node type {item.GetType()}");
            }
        }
        Parameters.Add(new TaskParameter { Name = parm.Name, Items = items });
    }

    private void PopulateOutputItems(ILogger logger, Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        Folder? outputItemsFolder = task.FindChild<Folder>(nameof(OutputItems));
        if (outputItemsFolder == null)
        {
            logger.LogDebug("Task {Task} has no OutputItems folder", task.ToString());
            return;
        }
        foreach (var child in outputItemsFolder.Children)
        {
            if (child is Property property)
            {
                OutputItems.Add(new TaskOutputItem { Name = property.Name, IsProperty = true });
            }
            else if (child is Item item)
            {
                OutputItems.Add(new TaskOutputItem { Name = item.Name, IsProperty = false });
            }
            else if (child is AddItem addItem)
            {
                string parameterName = GetParameterNameFromItemName(addItem.Name);
                OutputItems.Add(new TaskOutputItem { Name = parameterName, IsProperty = false });
                logger.LogDebug("AddItem: {ItemName} => {ParamName}", addItem.Name, parameterName);
            }
            else
            {
                logger.LogError("Unexpected node '{Node}' type {NodeType}", child.ToString(), child.GetType());
                throw new NotSupportedException($"Unexpected node type {child.GetType()}");
            }
        }
    }

    private static string GetParameterNameFromItemName(string itemName)
    {
        // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/817
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"^\s*(.*)\s+from parameter\s+(.*)\s*$");
        System.Text.RegularExpressions.Match match = regex.Match(itemName);
        if (match.Success)
        {
            return match.Groups[2].Value;
        }
        return itemName;  
    }

    private StringBuilder DumpModel()
    {
        StringBuilder sb = new StringBuilder();
        // dump the model as an ms build project xml element instance in a Target
        if (Parameters.Count > 0) {
            sb.AppendLine("<ItemGroup>");
            foreach (var param in Parameters)
            {
                foreach (var item in param.Items)
                {
                    if (item.AssetValue.HasValue)
                    {
                        sb.Append($"  <MyTask__{param.Name} Include=\"{item.AssetValue.Value}\" ");
                    }
                    else
                    {
                        sb.Append($"<MyTask__{param.Name} Include=\"{item.StringValue}\" ");
                    }
                    foreach (var metadata in item.Metadata)
                    {
                        sb.Append($"{metadata.Name}=\"{metadata.StringValue}\" ");
                    }
                    sb.AppendLine("/>");
                }
            }
            sb.AppendLine("</ItemGroup>");
        }

        sb.Append($"<{Name} ");
        foreach (var prop in Properties)
        {
            if (prop.AssetValue.HasValue)
            {
                sb.Append($"{prop.Name}=\"{prop.AssetValue.Value}\" ");
            }
            else
            {
                sb.Append($"{prop.Name}=\"{prop.StringValue}\" ");
            }
        }
        if (Parameters.Count > 0)
        {
            foreach (var param in Parameters)
            {
                sb.Append($"""{param.Name} = "@(MyTask__{param.Name})" """);
            }
        }
        if (OutputItems.Count == 0)
        {
            sb.AppendLine("/>");
        }
        else
        {
            sb.AppendLine(">");
            foreach (var output in OutputItems)
            {
                if (output.IsProperty)
                {
                    sb.AppendLine($"  <Output TaskParameter=\"{output.Name}\" PropertyName=\"MyTask__out__{output.Name}\" />");
                }
                else
                {
                    sb.AppendLine($"  <Output TaskParameter=\"{output.Name}\" ItemName=\"MyTask__out__{output.Name}\" />");
                }
            }
            sb.AppendLine($"</{Name}>");
        }
        return sb;
    }
}