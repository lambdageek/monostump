using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.Text;
using System.Reflection.Metadata.Ecma335;

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
        CollectGeneratedAssets(builder.Model);
        return true;
    }

    private void CollectGeneratedAssets(TaskModel model)
    {
        AssetRepository.AssetPath replayProject = _assets.GetOrAddGeneratedAsset(AssetRepository.GeneratedProjectName, AssetRepository.AssetKind.GeneratedProject, out var generatedAsset);
        // FIXME: this is a hack - we should have a project model for the replay project
        generatedAsset.FragmentGenerators.Add((sb) => sb.AppendLine($$"""
            <Project DefaultTargets="Replay">
            <PropertyGroup>
                <ReplayRootDir>$(MSBuildThisFileDirectory)</ReplayRootDir>
            </PropertyGroup>
            <Target Name="Replay" >
              <Error Text="ReplayOutputPath not set" Condition="'{{BinlogScraper.ReplayOutputPathProperty}}' == ''" />
              <PropertyGroup>
                <ReplayOutputPath>$([System.IO.Path]::GetFullPath('{{BinlogScraper.ReplayOutputPathProperty}}'))</ReplayOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <ReplayEnsureOutputExists Include="{{BinlogScraper.ReplayOutputPathProperty}}\aot-in" />
                <ReplayEnsureOutputExists Include="{{BinlogScraper.ReplayOutputPathProperty}}\aot-output" />
                <ReplayEnsureOutputExists Include="{{BinlogScraper.ReplayOutputPathProperty}}\aot-cache" />
                <ReplayEnsureOutputExists Include="{{BinlogScraper.ReplayOutputPathProperty}}\aot-tokens" />
              </ItemGroup>
              <MakeDir Directories="@(ReplayEnsureOutputExists)" />
            </Target>
            """));

        generatedAsset.FragmentGenerators.Add(model.GenerateTaskFragment);
        generatedAsset.FragmentGenerators.Add((sb) => sb.AppendLine("</Project>"));
        return;
    }

    bool TaskModel.IBuilderCallback.HandleSpecialTaskParameter(TaskModel.IBuilderCallbackCallback builder, Parameter parameter)
    {
        const string Assemblies = nameof(Assemblies);
        const string AdditionalAssemblySearchPaths = nameof(AdditionalAssemblySearchPaths);
        switch (parameter.Name)
        {
            case Assemblies:
                HandleAssemblies(builder, parameter);
                break;
            case AdditionalAssemblySearchPaths:
                HandleAdditionalAssemblySearchPaths(builder, parameter);
                break;
            default:
                return false;
        }
        return true;
    }

    private void HandleAssemblies(TaskModel.IBuilderCallbackCallback builder, Parameter parm)
    {
        AssetRepository.AssetKind assetParm = AssetRepository.AssetKind.InputAssembly;
        List<TaskModel.TaskItem> items = new List<TaskModel.TaskItem>();
        foreach (var child in parm.Children)
        {
            if (child is Item item)
            {
                    builder.PopulateParameterItem(item, items, assetParm);
            }
            else
            {
                _logger.LogError("Unexpected node {Node} type {NodeType}", child.ToString(), child.GetType());
                throw new NotSupportedException($"Unexpected node type {child.GetType()}");
            }
        }
        builder.AddTaskParameter(new () { Name = parm.Name, Items = items });
    }

    private void HandleAdditionalAssemblySearchPaths(TaskModel.IBuilderCallbackCallback builder, Parameter parm)
    {
        AssetRepository.AssetKind assetParm = AssetRepository.AssetKind.InputManagedAssemblyDirectory;
        List<TaskModel.TaskItem> items = new List<TaskModel.TaskItem>();
        foreach (var child in parm.Children)
        {
            if (child is Item item)
            {
                builder.PopulateParameterItem(item, items, assetParm);
            }
            else
            {
                _logger.LogError("Unexpected node {Node} type {NodeType}", child.ToString(), child.GetType());
                throw new NotSupportedException($"Unexpected node type {child.GetType()}");
            }
        }
        builder.AddTaskParameter(new () { Name = parm.Name, Items = items });
    }

    bool TaskModel.IBuilderCallback.HandleSpecialTaskProperty(TaskModel.IBuilderCallbackCallback builder, Property property)
    {
        const string OutputDir = nameof(OutputDir);
        const string IntermediateOutputPath = nameof(IntermediateOutputPath);
        const string DedupAssembly = nameof(DedupAssembly);
        const string CompilerBinaryPath = nameof(CompilerBinaryPath);
        const string CacheFilePath = nameof(CacheFilePath);
        const string AotModulesTablePath = nameof(AotModulesTablePath);
        const string TrimmingEligibleMethodsOutputDirectory = nameof(TrimmingEligibleMethodsOutputDirectory);
        const string LLVMPath = nameof(LLVMPath);
        const string WorkingDirectory = nameof(WorkingDirectory);
        const string ToolPrefix = nameof(ToolPrefix);
        switch (property.Name)
        {
            case OutputDir:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-output""" });
                break;
            case IntermediateOutputPath:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}""" });
                break;
            case CacheFilePath:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-cache\aot_compiler_cache.json""" });
                break;
            case AotModulesTablePath:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-output\driver-gen.c""" });
                break;
            case TrimmingEligibleMethodsOutputDirectory:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-tokens""" });
                break;
            case DedupAssembly:
                AssetRepository.AssetPath dedupPath = _assets.GetOrAddInputAsset(property.Value, AssetRepository.AssetKind.InputAssembly);
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = dedupPath});
                break;
            case LLVMPath:
                AssetRepository.AssetPath llvmPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingUnixyBinTree);
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = llvmPath });
                break;
            case CompilerBinaryPath:
                AssetRepository.AssetPath compilerPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingBinary);
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = compilerPath });
                break;
            case WorkingDirectory:
                // FIXME: this is a hack that happens to work for Android
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = "$(MSBuildThisProjectDirectory)"});
                break;
            case ToolPrefix:
                {
                    var toolPrefixIn = property.Value;
                    var toolPrefixBareName = Path.GetFileNameWithoutExtension(toolPrefixIn);
                    var toolDir = Path.GetDirectoryName(toolPrefixIn);
                    var toolDirAsset = _assets.GetOrAddToolingAsset(toolDir, AssetRepository.AssetKind.ToolingUnixyBinTree);
                    builder.AddTaskProperty(new () {Name = property.Name, SpecialValue = () => $"$(ReplayRootDir){Path.DirectorySeparatorChar}{_assets.GetAssetRelativePath(toolDirAsset)}{Path.DirectorySeparatorChar}{toolPrefixBareName}"});
                    break;
                }
            default:
                return false;
        }
        return true;
    }

    bool TaskModel.IBuilderCallback.HandleSpecialTaskMetadata(TaskModel.IBuilderCallbackCallback builder, Metadata metadata, List<TaskModel.TaskMetadata> destMetadata)
    {
        const string AotArguments = nameof(AotArguments);
        if (metadata.Name == AotArguments) {
            Item? item = metadata.Parent as Item;
            if (item == null)
            {
                _logger.LogError("AotArguments metadata not attached to an Item, but to {ParentType}", metadata.Parent.GetType());
                throw new InvalidOperationException("AotArguments metadata not attached to an Item");
            }
            var parser = new MonoAotArgumentsParser(metadata.Value);
            var opts = parser.Parse();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var opt in opts)
                {
                    sb.AppendLine($"<{opt}>");
                }
                _logger.LogDebug("Parsed AOT arguments: {AotArguments}", sb.ToString());
            }
            var rewritternOpts = HandleAotArguments(item.Name, opts);
            var taskMetadata = new TaskModel.TaskMetadata() {
                Name = "AotArguments",
                SpecialValue = () => 
                    StringifyRewrittenOpts(rewritternOpts),
                };
            destMetadata.Add(taskMetadata);
            return true;
        }
        return false;
    }

    internal readonly struct AotItemOption
    {
        public string Name {get; init;}
        public string? StringValue {get; init;}
        public AssetRepository.AssetPath? AssetValue {get; init;}
    }

    IReadOnlyList<AotItemOption> HandleAotArguments(string itemName, IReadOnlyList<string> opts)
    {
        if (!itemName.EndsWith(".dll"))
        {
            _logger.LogWarning("AOT input assembly name does not end with .dll: {ItemName}", itemName);
        }
        string itemBareName = Path.GetFileNameWithoutExtension(itemName);
        List<AotItemOption> aotOpts = new List<AotItemOption>();
        foreach (var opt in opts)
        {
            if (!opt.Contains("="))
            {
                aotOpts.Add(new AotItemOption() { Name = opt });
                continue;
            }
            var parts = opt.Split('=', 2);
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid AOT option: {AotOption}", opt);
                continue;
            }
            switch (parts[0]) {
            case "temp-path":
                // add a temp path in the replay output directory, based on the input assembly name
                aotOpts.Add(new () { Name = parts[0], StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-temp\{itemBareName}""" });
                break;
            case "profile":
                aotOpts.Add(new ()  { Name = parts[0], AssetValue = _assets.GetOrAddInputAsset(parts[1], AssetRepository.AssetKind.InputOther) });
                break;
            default:
                aotOpts.Add(new () { Name = parts[0], StringValue = parts[1] });
                break;
            }
        }
        return aotOpts;
    }

    string StringifyRewrittenOpts(IReadOnlyList<AotItemOption> opts)
    {
        StringBuilder sb = new StringBuilder();
        foreach (var opt in opts)
        {
            if (opt.StringValue != null)
            {
                sb.Append($"{opt.Name}={opt.StringValue}");
            }
            else if (opt.AssetValue != null)
            {
                sb.Append($"{opt.Name}=$(ReplayRootDir){Path.DirectorySeparatorChar}{_assets.GetAssetRelativePath(opt.AssetValue.Value)}");
            }
            else
            {
                sb.Append(opt.Name);
            }
            sb.Append(',');
        }
        return sb.ToString();
    }
}