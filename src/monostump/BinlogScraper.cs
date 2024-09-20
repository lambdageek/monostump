using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;

public class BinlogScraper
{
    private const string ReplayOutputPath = nameof(ReplayOutputPath);
    public const string ReplayOutputPathProperty = $"$({ReplayOutputPath})";
    private readonly ILogger _logger;
    private readonly AssetRepository _assetRepository;
    private readonly bool _noOutput;
    public BinlogScraper(ILogger logger, bool noOutput)
    {
        _logger = logger;
        _assetRepository = new AssetRepository(_logger);
        Flavor = BuildFlavor.Unknown;
        _noOutput = noOutput;
    }

    public bool CollectFromFile(string binlogPath, string outputPath)
    {
        var build = ReadBuild(binlogPath);
        if (build == null)
        {
            _logger.LogError("Failed to read build");
            return false;
        }
        if (!Scrape(build))
        {
            _logger.LogError("Failed to scrape binlog");
            return false;
        }
        _logger.LogInformation ("Detected AOT compiler flavor: {Flavor}", Flavor.ToString());


        switch (Flavor)
        {
            case BuildFlavor.RuntimeAot:
            {
                var aotScraper = new RuntimeAotCompilerScraper(_logger, _assetRepository, build);
                aotScraper.CollectAllAssets();
                break;
            }
            case BuildFlavor.AppleLocal:
            {
                var appleScraper = new MaciosCompilerScraper(_logger);
                appleScraper.CollectAllAssets();
                break;
            }
            default:
            {
                Console.Error.WriteLine($"Unsupported build flavor {Flavor}");
                return false;
            }
        }
        _assetRepository.Freeze();
        _assetRepository.CreateGeneratedAssets();
        DumpAssetRepo();
        if (!_noOutput)
        {
            ArchiveAssetRepo(outputPath);
        } else
        {
            _logger.LogInformation("Dry run, not writing output to {OutputPath}", outputPath);
        }
        return true;
    }

    private Build? ReadBuild(string binlogPath)
    {
        var progress = new Progress();
        progress.Updated += (update) => {
            _logger.LogInformation("Reading Binlog {Ratio:f2}% ...", 100.0 * update.Ratio);
        };
        var build = BinaryLog.ReadBuild(binlogPath, progress);
        BuildAnalyzer.AnalyzeBuild(build);
        return build;
    }

    public BuildFlavor Flavor { get; private set; }

    private bool Scrape(Build build)
    {
        DetectFlavor(build);
        if (Flavor == BuildFlavor.Unknown)
        {
            return false;
        }
        return true;
    }

    public enum BuildFlavor
    {
        Unknown = 0,
        RuntimeAot,
        AppleLocal,
        AppleRemote,
    }

    public void DetectFlavor(Build build)
    {
        // TODO: multi-flavor projects?
        // Something like MAUI could conceivably build for Android and Apple in the same binlog
        if (build.FindFirstDescendant<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "MonoAOTCompiler") != null)
        {
            _logger.LogDebug("Detected MonoAOTCompiler task");
            Flavor = BuildFlavor.RuntimeAot;
            return;
        }
        if (build.FindFirstDescendant<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "AOTCompile") != null)
        {
            _logger.LogDebug("Detected Apple AOT build");
            Flavor = BuildFlavor.AppleLocal; // TODO: detect AppleRemote
            return;
        }
        _logger.LogDebug("Unknown build flavor");
    }

    private void DumpAssetRepo()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Dumping asset repository");
            _assetRepository.Dump();
        }
    }

    private void ArchiveAssetRepo(string archivePath)
    {
        _logger.LogInformation("Archiving asset repository to {Archive}", archivePath);
        //const string archivePath = "./out/replay.zip";
        string? archiveDir = System.IO.Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrEmpty(archiveDir) && !System.IO.Directory.Exists(archiveDir))
            System.IO.Directory.CreateDirectory(archiveDir);
        _assetRepository.Archive(archivePath, new Progress<float>((completion) => {
            _logger.LogInformation("Archiving {completion:f2}% ...", completion*100.0f);
        }));
        _logger.LogInformation("Archived to {Archive}", archivePath);
    }

}