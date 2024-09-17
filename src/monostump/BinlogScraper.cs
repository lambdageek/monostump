using System;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Logging.StructuredLogger;

public class BinlogScraper
{
    private readonly Build _build;
    private readonly ILogger _logger;
    public BinlogScraper(ILogger logger, Build build)
    {
        _build = build;
        _logger = logger;
        Flavor = BuildFlavor.Unknown;
    }

    internal TreeNode Root => _build;

    public BuildFlavor Flavor { get; private set; }

    public bool Scrape()
    {
        DetectFlavor();
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

    public void DetectFlavor()
    {
        // TODO: multi-flavor projects?
        // Something like MAUI could conceivably build for Android and Apple in the same binlog
        if (Root.FindFirstDescendant<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "MonoAOTCompiler") != null)
        {
            _logger.LogDebug("Detected MonoAOTCompiler task");
            Flavor = BuildFlavor.RuntimeAot;
            return;
        }
        if (Root.FindFirstDescendant<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "AOTCompile") != null)
        {
            _logger.LogDebug("Detected Apple AOT build");
            Flavor = BuildFlavor.AppleLocal; // TODO: detect AppleRemote
            return;
        }
        _logger.LogDebug("Unknown build flavor");
    }

}