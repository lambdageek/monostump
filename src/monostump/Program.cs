using System.Diagnostics;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var logger = MakeLogger(trace: false);

if (!new BinlogScraper(logger).CollectFromFile(args[0]))
    return 1;

return 0;

ILogger MakeLogger(bool trace)
{
    using var factory = LoggerFactory.Create(builder => {
#if DEBUG
        builder.AddFilter(typeof(BinlogScraper).FullName, trace ? LogLevel.Trace : LogLevel.Debug);
#endif
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddConsole();
    });
    
    var logger = factory.CreateLogger<BinlogScraper>();
    return logger;
}