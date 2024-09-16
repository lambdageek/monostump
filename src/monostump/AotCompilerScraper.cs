using System;
using Microsoft.Build.Logging.StructuredLogger;

/// <summary>
///  A scraper for projects that use the Mono AOT Compiler task
/// </summary>
public class AotCompilerScraper
{
    public TreeNode Root { get; }
    public AotCompilerScraper(TreeNode root)
    {
        Root = root;
    }

    public bool CollectAllAssets()
    {
        IReadOnlyList<Microsoft.Build.Logging.StructuredLogger.Task> aotTasks = Root.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task>(t => t.Name == "MonoAOTCompiler");

        foreach (var t in aotTasks)
        {
            if (!CollectAotTaskAssets(t))
            {
                return true;
            }
        }

        return true;
    }

    private bool CollectAotTaskAssets(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        if (!CollectAotTaskParams(task))
            return false;
        if (!CollectAotTaskMessages(task))
            return false;
        return true;
    }

    private bool CollectAotTaskParams(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        var parameters = task.FindFirstChild<Folder>(f => f.Name == "Parameters");
        if (parameters == null)
        {
            return false;
        }
        foreach (var param in parameters.Children)
        {
            if (param is Property property)
            {
                if (!CollectAotTaskProperty(property))
                    return false;
            } 
            else if (param is Microsoft.Build.Logging.StructuredLogger.Parameter items )
            {
                Console.WriteLine ($"Param: {items.Name}");
                foreach (var item in items.Children)
                {
                    if (item is Item itemProperty)
                    {
                        Console.WriteLine ($"  {itemProperty.Name} ");
                        Console.WriteLine ($"    TODO: metadata");
                    }
                    else
                    {
                        Console.Error.WriteLine ($"unexpected child {item}");
                        return false;
                    }
                }
            }
            else {
                Console.Error.WriteLine ($"unexpected child {param} : {param.GetType()}");
                return false;
            }
        }
        return true;
    }

    private bool CollectAotTaskProperty(Property property)
    {
        Console.WriteLine ($"Property: {property.Name} = {property.Value}");
        return true;
    }

    private bool CollectAotTaskMessages(Microsoft.Build.Logging.StructuredLogger.Task task)
    {
        int foundExecMessage = 0;
        var messageScraper = new MonoAotCrossCommandLineScraper();
        foreach (var child in task.FindChildrenRecursive<Message>())
        {
            if (!messageScraper.ScrapeAotCompilerOutputMessage(child.Text))
            {
                continue;
            } else {
                foundExecMessage++;
            }
        }
        if (foundExecMessage == 0)
            return false;
        return true;
    }

}