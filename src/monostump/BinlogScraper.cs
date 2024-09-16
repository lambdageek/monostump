using System;
using Microsoft.Build.Logging.StructuredLogger;

public class BinlogScraper
{
    private readonly Build _build;
    public BinlogScraper(Build build)
    {
        _build = build;
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
        AotCompilerTask,
        Android,
        AppleLocal,
        AppleRemote,
    }

    public void DetectFlavor()
    {
        // TODO: multi-flavor projects?
        // Something like MAUI could conceivably build for Android and Apple in the same binlog
        if (Root.FindFirstDescendant<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "MonoAOTCompiler") != null)
        {
            Flavor = BuildFlavor.AotCompilerTask;
            return;
        }
    }

}