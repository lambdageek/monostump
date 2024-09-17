using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var logger = MakeLogger();


var progress = new Progress();
progress.Updated += (update) => {
    logger.LogInformation("Reading Build {Ratio:f2}%", 100.0 * update.Ratio);
};
var build = BinaryLog.ReadBuild(args[0], progress);
BuildAnalyzer.AnalyzeBuild(build);

var scraper = new BinlogScraper(logger, build);
if (!scraper.Scrape())
{
    logger.LogError("Failed to scrape binlog");
    return 1;
}
logger.LogInformation (scraper.Flavor.ToString());

switch (scraper.Flavor)
{
    case BinlogScraper.BuildFlavor.RuntimeAot:
    {
        var aotScraper = new RuntimeAotCompilerScraper(logger, scraper.Root);
        aotScraper.CollectAllAssets();
        break;
    }
    case BinlogScraper.BuildFlavor.AppleLocal:
    {
        var appleScraper = new MaciosCompilerScraper(logger);
        appleScraper.CollectAllAssets();
        break;
    }
    default:
    {
        Console.Error.WriteLine($"Unsupported build flavor {scraper.Flavor}");
        return 1;
    }
}
return 0;

ILogger MakeLogger()
{
    using var factory = LoggerFactory.Create(builder => {
#if DEBUG
        builder.AddFilter(typeof(BinlogScraper).FullName, LogLevel.Debug);
#endif
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddConsole();
    });
    
    var logger = factory.CreateLogger<BinlogScraper>();
    return logger;
}