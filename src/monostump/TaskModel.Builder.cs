using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.Text;

/// <summary>
/// Reconstructed <Task/> element from a .csproj file based on
/// available binlog data, with file-like attributes represented as assets
/// </summary>
public partial class TaskModel
{
    public interface IBuilderCallback
    {
        bool HandleSpecialTaskProperty(IBuilderCallbackCallback builder, Property property);
        bool HandleSpecialTaskParameter(IBuilderCallbackCallback builder, Parameter parameter);
        bool HandleSpecialTaskMetadata(IBuilderCallbackCallback builder, Metadata metadata, List<TaskMetadata> destMetadata);
    }

    public interface IBuilderCallbackCallback 
    {
        void AddTaskProperty(TaskProperty taskProperty);
        void AddTaskParameter(TaskParameter taskParameter);

        void PopulateParameterItem(Item item, List<TaskItem> items, AssetRepository.AssetKind? assetParm);

        void PopulateParameterItemMetadata(Metadata metadataProp, List<TaskMetadata> metadata);
    }

    public class Builder : IBuilderCallbackCallback
    {
        private readonly ILogger _logger;
        private readonly AssetRepository _assets;
        private TaskModel? _model;
        private readonly IBuilderCallback _callback;

        public Builder(ILogger logger, AssetRepository assets, IBuilderCallback callback)
        {
            _logger = logger;
            _assets = assets;
            _callback = callback;
        }

        public TaskModel Model => _model ?? throw new InvalidOperationException("Model not created");

        public void AddTaskProperty(TaskProperty taskProperty)
        {
            _model!.Properties.Add(taskProperty);
        }

        public void AddTaskParameter(TaskParameter taskParameter)
        {
            _model!.Parameters.Add(taskParameter);
        }

        public bool Create(Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            // Based on https://github.com/KirillOsenkov/MSBuildStructuredLog/tree/main/src/TaskRunner/Executor.cs
            Project? parentProject = task.GetNearestParent<Project>();
            if (parentProject == null)
            {
                _logger.LogError("Task {Task} has no parent project", task.ToString());
                throw new InvalidOperationException("Task has no parent project");
            }
            _logger.LogDebug("Setting parent project to {Project}", parentProject.ProjectFile);
            using var _ = _assets.BeginProject(parentProject.ProjectFile);
            if (task.IsDerivedTask)
            {
                _logger.LogWarning("Task {Task} is a derived task", task.ToString());
            }
            AssetRepository.AssetPath assemblyPath = _assets.GetOrAddToolingAsset(task.FromAssembly, AssetRepository.AssetKind.ToolingAssembly);
            using var _1 = _assets.BeginAotCompilation(task.Name);
            _model = new TaskModel(_assets, assemblyPath, task.Name);
            if (!PopulateParameters(task))
            {
                return false;
            }
            if (!PopulateOutputItems(task))
            {
                return false;
            }
            return true;
        }

        private bool PopulateParameters(Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            Folder? parametersFolder = task.FindChild<Folder>(nameof(Parameters));
            if (parametersFolder == null)
            {
                _logger.LogError("Task {Task} has no Parameters folder", task.ToString());
                throw new InvalidOperationException("Task has no Parameters folder");
            }
            for (int passes = 0; passes < 2; passes++)
            {
                // Have to do two passes because of DedupAssembly.
                // It appears both as an input property and as an Assemblies item
                // In the first pass just do all the properties and figure out what the DedupAssembly value is,
                // then in the second pass when we see it, replace it with our preferred generated path.
                foreach (var child in parametersFolder.Children)
                {
                    switch (child)
                    {
                        case Property property:
                            if (passes != 0)
                            {
                                continue;
                            }
                            PopulateProperty(property);
                            break;
                        case Parameter parameter:
                            if (passes != 1)
                            {
                                continue;
                            }
                            PopulateParameter(parameter);
                            break;
                        default:
                            _logger.LogError("Unexpected node {Node} type {NodeType}", child.ToString(), child.GetType());
                            throw new NotSupportedException($"Unexpected node type {child.GetType()}");
                    }
                }
            }
            return true;
        }

        private void PopulateProperty(Property property)
        {
            const string Assembly = nameof(Assembly);
            _logger.LogDebug("Property: {PropertyName} = {PropertyValue}", property.Name, property.Value);
            if (_callback.HandleSpecialTaskProperty(this, property))
            {
                return;
            }
            switch (property.Name)
            {
                case Assembly:
                    AssetRepository.AssetPath asmPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingAssembly);
                    AddTaskProperty(new () { Name = property.Name, AssetValue = asmPath });
                    break;
                default:
                    // TODO: handle other known properties
                    AddTaskProperty(new () { Name = property.Name, StringValue = property.Value });
                    break;

            }
        }

        private void PopulateParameter(Parameter parm)
        {
            _logger.LogDebug("Parameter: {ParameterName}", parm.Name);
            if (_callback.HandleSpecialTaskParameter(this, parm))
            {
                return;
            }
            else
            {
                AssetRepository.AssetKind? assetParm = parm.Name == "Assemblies" ? AssetRepository.AssetKind.InputAssembly : null; // TODO: handle other known parameters
                List<TaskItem> items = new List<TaskItem>();
                foreach (var child in parm.Children)
                {
                    if (child is Item item)
                        PopulateParameterItem(item, items, assetParm);
                    else
                    {
                        _logger.LogError("Unexpected node {Node} type {NodeType}", child.ToString(), child.GetType());
                        throw new NotSupportedException($"Unexpected node type {child.GetType()}");
                    }
                }
                AddTaskParameter(new () { Name = parm.Name, Items = items });
            }
        }

        public void PopulateParameterItem(Item item, List<TaskItem> items, AssetRepository.AssetKind? assetParm)
        {
            List<TaskMetadata> metadata = new List<TaskMetadata>();
            foreach (var metadataProperty in item.Children)
            {
                if (metadataProperty is Metadata metadataProp)
                {
                    if (_callback.HandleSpecialTaskMetadata(this, metadataProp, metadata))
                    {
                        continue;
                    }
                    PopulateParameterItemMetadata(metadataProp, metadata);
                }
                else
                {
                    _logger.LogError("Unexpected node {Node} type {NodeType}", metadataProperty.ToString(), metadataProperty.GetType());
                    throw new NotSupportedException($"Unexpected node type {metadataProperty.GetType()}");
                }
            }
            TaskItem taskItem;
            if (!assetParm.HasValue)
            {
                taskItem = new TaskItem { StringValue = item.Name, Metadata = metadata };
            }
            else
            {
                taskItem = new TaskItem
                {
                    AssetValue = _assets.GetOrAddInputAsset(item.Name, assetParm.Value),
                    Metadata = metadata,
                };
            }
            items.Add(taskItem);
        }

        public void PopulateParameterItemMetadata(Metadata metadataProp, List<TaskMetadata> metadata)
        {
            TaskMetadata taskMetadata = new TaskMetadata { Name = metadataProp.Name, StringValue = metadataProp.Value };
            metadata.Add(taskMetadata);
        }

        private bool PopulateOutputItems(Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            Folder? outputItemsFolder = task.FindChild<Folder>(nameof(OutputItems));
            if (outputItemsFolder == null)
            {
                _logger.LogDebug("Task {Task} has no OutputItems folder", task.ToString());
                return true;
            }
            foreach (var child in outputItemsFolder.Children)
            {
                if (child is TaskParameterItem taskParameterItem)
                {
                    _model!.OutputItems.Add(new TaskOutputItem { Name = taskParameterItem.ParameterName, IsProperty = false });
                }
                else
                if (child is AddItem addItem)
                {
                    string parameterName = addItem.Name;
                    _model!.OutputItems.Add(new TaskOutputItem { Name = parameterName, IsProperty = false });
                    _logger.LogDebug("AddItem: {ItemName} => {ParamName}", addItem.Name, parameterName);
                }
                else if (child is Property property)
                {
                    _model!.OutputItems.Add(new TaskOutputItem { Name = property.Name, IsProperty = true });
                }
                else if (child is Item item)
                {
                    _model!.OutputItems.Add(new TaskOutputItem { Name = item.Name, IsProperty = false });
                }
                else
                {
                    _logger.LogError("Unexpected node '{Node}' type {NodeType}", child.ToString(), child.GetType());
                    throw new NotSupportedException($"Unexpected node type {child.GetType()}");
                }
            }
            return true;
        }

    }
}