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
        CollectGeneratedAssets(builder.Model);
        return true;
    }

    private void CollectGeneratedAssets(TaskModel model)
    {
        AssetRepository.AssetPath replayProject = _assets.GetOrAddGeneratedAsset(AssetRepository.GeneratedProjectName, AssetRepository.AssetKind.GeneratedProject, out var generatedAsset);
        // FIXME: this is a hack - we should have a project model for the replay project
        generatedAsset.FragmentGenerators.Add((sb) => sb.AppendLine($$"""
            <Project DefaultTargets="Replay">
            <Target Name="Replay" >
              <Error Text="ReplayOutputPath not set" Condition="'{{BinlogScraper.ReplayOutputPathProperty}}' == ''" />
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
        return false;
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
        switch (property.Name)
        {
            case OutputDir:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-output""" });
                break;
            case IntermediateOutputPath:
                builder.AddTaskProperty(new () { Name = property.Name, StringValue = $"""{BinlogScraper.ReplayOutputPathProperty}\aot-in""" });
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
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = dedupPath });
                break;
            case LLVMPath:
                AssetRepository.AssetPath llvmPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingUnixyBinTree);
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = llvmPath });
                break;
            case CompilerBinaryPath:
                AssetRepository.AssetPath compilerPath = _assets.GetOrAddToolingAsset(property.Value, AssetRepository.AssetKind.ToolingBinary);
                builder.AddTaskProperty(new () { Name = property.Name, AssetValue = compilerPath });
                break;
            default:
                return false;
        }
        return true;
    }

}