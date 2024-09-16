using System;
using System.Text.RegularExpressions;

public partial class MonoAotCrossCommandLineScraper
{
    public MonoAotCrossCommandLineScraper()
    {
    }

    [GeneratedRegex("""^\[(.*)\]\s+Exec \(with response file contents expanded\) in (.*):\s+((\S+)=([^= ]*) )*(.*)$""")]
    private partial Regex AotCompilerOutputMessageRegex();


    public bool ScrapeAotCompilerOutputMessage(string input)
    {
        var match = AotCompilerOutputMessageRegex().Match(input);
        if (match.Success)
        {
            var prefix = match.Groups[1].Value;
            var workingDir = match.Groups[2].Value;
            var env = match.Groups[3];
            var envVars = new Dictionary<string, string>();
            for (int i = 1; i < match.Groups.Count; i++)
            {
                Console.WriteLine ($"Group {i}: @@{match.Groups[i].Value}@@");
            }
            var cli = match.Groups[match.Groups.Count].Value;
            Console.WriteLine ("Prefix: " + prefix);
            Console.WriteLine ("WorkingDir: " + workingDir);
            Console.WriteLine ("CLI: " + cli);
            foreach (var kvp in envVars)
            {
                Console.WriteLine ($"  <{kvp.Key}> = <{kvp.Value}>");
            }
            return true;
        }
        return false;

    }
}