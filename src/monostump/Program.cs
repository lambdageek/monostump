using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.CommandLine;
using System.ComponentModel;


CliRootCommand rootCommand = new () {
    Description = "Scrapes an MSBuild binlog file for Mono AOT compiler invocations and creates a replayable build.",
};
CliArgument<string> inputfile = new CliArgument<string>("binlog") {
    Description = "The path to the binlog file to scrape.", 
    Arity = ArgumentArity.ExactlyOne,
};
CliOption<bool> verbose = new CliOption<bool>("--verbose", "-v") {
    Description = "Set the log level to verbose.",
    Arity = ArgumentArity.Zero,
};
rootCommand.Add(inputfile);
rootCommand.Add(verbose);
rootCommand.SetAction (RunScraper);

return rootCommand.Parse(args).Invoke();

int RunScraper(ParseResult opts)
{

    var input = opts.GetValue(inputfile);
    if (string.IsNullOrEmpty(input)) {
        Console.Error.WriteLine("No input file specified.");
        return 1;
    }

    var logLevel = opts.GetValue(verbose) ? LogLevel.Trace : LogLevel.Information;

    var logger = MakeLogger(logLevel);

    if (!new BinlogScraper(logger).CollectFromFile(input))
        return 1;

    return 0;

    ILogger MakeLogger(LogLevel logLevel)
    {
        string filter = typeof(BinlogScraper).Assembly.GetName().Name!;
        using var factory = LoggerFactory.Create(builder => {
            builder.AddFilter(filter, logLevel);
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        
        var logger = factory.CreateLogger(filter);
        return logger;
    }
}