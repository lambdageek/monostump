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
    
    public void GenerateTaskFragment(StringBuilder sb)
    {
        sb.AppendLine($"""
        <!-- Task {Name} -->
        <UsingTask TaskName="{Name}" AssemblyFile="{AssemblyPath.RelativePath}" />
        <Target Name="Replay{Name}" AfterTargets="Replay" >
        """);
        DumpModel(sb, indent: 2);
        sb.AppendLine("</Target>");
    }

    private StringBuilder DumpModel()
    {
        StringBuilder sb = new StringBuilder();
        DumpModel(sb);
        return sb;
    }

    private void DumpModel(StringBuilder sb, int indent = 0)
    {
        string pfx = new string(' ', indent);
        // dump the model as an ms build project xml element instance in a Target
        if (Parameters.Count > 0) {
            sb.Append(pfx);
            sb.AppendLine("<ItemGroup>");
            foreach (var param in Parameters)
            {
                foreach (var item in param.Items)
                {
                    if (item.AssetValue.HasValue)
                    {
                        sb.AppendLine($"{pfx}  <MyTask__{param.Name} Include=\"{item.AssetValue.Value.RelativePath}\" ");
                    }
                    else
                    {
                        sb.AppendLine($"{pfx}  <MyTask__{param.Name} Include=\"{item.StringValue}\" ");
                    }
                    foreach (var metadata in item.Metadata)
                    {
                        sb.AppendLine($"{pfx}    {metadata.Name}=\"{metadata.StringValue}\" ");
                    }
                    sb.AppendLine($"{pfx}  />");
                }
            }
            sb.AppendLine($"{pfx}</ItemGroup>");
        }

        sb.AppendLine($"{pfx}<{Name} ");
        foreach (var prop in Properties)
        {
            if (prop.AssetValue.HasValue)
            {
                sb.AppendLine($"{pfx}  {prop.Name}=\"{prop.AssetValue.Value.RelativePath}\" ");
            }
            else
            {
                sb.AppendLine($"{pfx}  {prop.Name}=\"{prop.StringValue}\" ");
            }
        }
        if (Parameters.Count > 0)
        {
            foreach (var param in Parameters)
            {
                sb.AppendLine($"""{pfx}  {param.Name} = "@(MyTask__{param.Name})" """);
            }
        }
        if (OutputItems.Count == 0)
        {
            sb.AppendLine($"{pfx}  />");
        }
        else
        {
            sb.AppendLine($"{pfx}  >");
            foreach (var output in OutputItems)
            {
                if (output.IsProperty)
                {
                    sb.AppendLine($"{pfx}    <Output TaskParameter=\"{output.Name}\" PropertyName=\"MyTask__out__{output.Name}\" />");
                }
                else
                {
                    sb.AppendLine($"{pfx}    <Output TaskParameter=\"{output.Name}\" ItemName=\"MyTask__out__{output.Name}\" />");
                }
            }
            sb.AppendLine($"{pfx}</{Name}>");
        }
    }
}