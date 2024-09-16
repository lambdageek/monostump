using Microsoft.Build.Logging.StructuredLogger;

using var stream = File.OpenRead(args[0]);
var build = BinaryLog.ReadBuild(args[0]);
BuildAnalyzer.AnalyzeBuild(build);
var root = build.GetRoot();
if (root is TreeNode node)
{
    Console.WriteLine(node.ToString());
    foreach (var child in node.Children)
    {
        Console.WriteLine(child.ToString());
    }
}
