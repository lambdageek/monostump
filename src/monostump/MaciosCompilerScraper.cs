using System;
using Microsoft.Extensions.Logging;

public class MaciosCompilerScraper : ITaskScraper
{
    private readonly ILogger _logger;
    public MaciosCompilerScraper(ILogger logger)
    {
        _logger = logger;
    }

    public bool CollectAllAssets()
    {
        _logger.LogDebug("Scraping MaciosCompilerScraper");
        return true;
    }
}