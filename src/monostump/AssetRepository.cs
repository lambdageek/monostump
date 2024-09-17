using System;

public class AssetRepository
{
    public AssetRepository()
    {
    }

    public class ScopeToken : IDisposable
    {
        public void Dispose()
        {
        }
    }

    public ScopeToken BeginBuild(string rid, string frameworkVersion)
    {
        return new ScopeToken();
    }

    public ScopeToken BeginProject(string projectPath)
    {
        return new ScopeToken();
    }

    public ScopeToken BeginAotCompilatoin()
    {
        return new ScopeToken();
    }

    public bool AddToolingAsset(string onDiskPath)
    {
        return true;
    }
}