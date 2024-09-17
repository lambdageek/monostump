using System;

// <summary>
// A scraper for projects that use an msbuild task to invoke the Mono AOT Compiler
// </summary>
public interface ITaskScraper
{
    bool CollectAllAssets();
}