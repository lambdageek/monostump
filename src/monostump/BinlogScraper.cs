using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;

public class BinlogScraper
{
    private readonly ILogger _logger;
    private readonly AssetRepository _assetRepository;
    public BinlogScraper(ILogger logger)
    {
        _logger = logger;
        _assetRepository = new AssetRepository(_logger);
        Flavor = BuildFlavor.Unknown;
    }

    public bool CollectFromFile(string binlogPath)
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
        _logger.LogInformation (Flavor.ToString());

        switch (Flavor)
        {
            case BinlogScraper.BuildFlavor.RuntimeAot:
            {
                var aotScraper = new RuntimeAotCompilerScraper(_logger, _assetRepository, build);
                aotScraper.CollectAllAssets();
                break;
            }
            case BinlogScraper.BuildFlavor.AppleLocal:
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
        DumpAssetRepo();
        ArchiveAssetRepo();
        return true;
    }

    private Build? ReadBuild(string binlogPath)
    {
        var progress = new Progress();
        progress.Updated += (update) => {
            _logger.LogInformation("Reading Build {Ratio:f2}%", 100.0 * update.Ratio);
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

    private void ArchiveAssetRepo()
    {
        _logger.LogInformation("Archiving asset repository");
        const string archivePath = "./out/replay.zip";
        string? archiveDir = System.IO.Path.GetDirectoryName(archivePath);
        if (archiveDir != null && !System.IO.Directory.Exists(archiveDir))
            System.IO.Directory.CreateDirectory(archiveDir);
        _assetRepository.Archive(archivePath);
        _logger.LogInformation("Archived to {Archive}", archivePath);
    }

}