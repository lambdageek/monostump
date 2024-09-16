using Microsoft.Build.Logging.StructuredLogger;

using var stream = File.OpenRead(args[0]);
var records = BinaryLog.ReadRecords(stream);
foreach (var record in records)
{
    Console.WriteLine(record);
}