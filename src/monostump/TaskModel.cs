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
                        sb.Append($"  <MyTask__{param.Name} Include=\"{item.AssetValue.Value.RelativePath}\" ");
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
                sb.Append($"{prop.Name}=\"{prop.AssetValue.Value.RelativePath}\" ");
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