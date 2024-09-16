using Microsoft.Build.Logging.StructuredLogger;

var build = BinaryLog.ReadBuild(args[0]);
BuildAnalyzer.AnalyzeBuild(build);
var scraper = new BinlogScraper(build);
if (!scraper.Scrape())
{
    Console.Error.WriteLine("Failed to scrape binlog");
    return 1;
}
Console.WriteLine (scraper.Flavor);
switch (scraper.Flavor)
{
    case BinlogScraper.BuildFlavor.AotCompilerTask:
    {
        var aotScraper = new AotCompilerScraper(scraper.Root);
        aotScraper.CollectAllAssets();
        break;
    }
    default:
    {
        Console.Error.WriteLine($"Unsupported build flavor {scraper.Flavor}");
        return 1;
    }
}
return 0;
