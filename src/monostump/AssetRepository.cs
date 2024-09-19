using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;

public class AssetRepository
{
    public enum AssetKind
    {
        InputAssembly,
        InputOther,
        ToolingAssembly,
        ToolingBinary,
        ToolingFullFolder,
        GeneratedOther,
    }

    private static bool IsToolingKind(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.ToolingAssembly => true,
            AssetKind.ToolingBinary => true,
            AssetKind.ToolingFullFolder => true,
            _ => false,
        };
    }

    private static bool IsInputKind(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.InputAssembly => true,
            AssetKind.InputOther => true,
            _ => false,
        };
    }

    private static bool IsGeneratedKind(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.GeneratedOther => true,
            _ => false,
        };
    }

    public readonly struct AssetPath : IEquatable<AssetPath>
    {
        public ImmutableList<string> Subfolders { get; init; }
        public string Filename { get; init; }

        public bool Equals(AssetPath other)
        {
            return EqualSubfolders(Subfolders, other.Subfolders) && Filename == other.Filename;
        }

        private static bool EqualSubfolders(ImmutableList<string> left, ImmutableList<string> right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null)
                return false;
            if (left.Count != right.Count)
                return false;

            foreach ((var first, var second) in left.Zip(right))
            {
                if (first != second)
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is AssetPath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashFolders(Subfolders), Filename);
        }

        private int HashFolders(ImmutableList<string> folders)
        {
            if (folders == null)
                return 0;
            int hash = 0;
            foreach (var folder in folders)
            {
                hash = HashCode.Combine(hash, folder);
            }
            return hash;
        }

        public static bool operator ==(AssetPath left, AssetPath right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssetPath left, AssetPath right)
        {
            return !left.Equals(right);
        }

        public string RelativePath {
            get {
                if (Subfolders == null)
                    return Filename;
                return Path.Combine(Subfolders.ToArray()) + Path.DirectorySeparatorChar + Filename;
            }
        }
    }

    public readonly struct Asset
    {
        public string OriginalPath { get; init; }
        public required AssetKind Kind { get; init; }
    }

    private readonly ILogger _logger;
    private readonly Dictionary<AssetPath, Asset> _assets = new Dictionary<AssetPath, Asset>();
    public AssetRepository(ILogger logger)
    {
        _logger = logger;
        _assets = new();
        CurrentProjectBaseDir = string.Empty;
    }

    public string CurrentProjectBaseDir { get; set; }

    public class ScopeToken : IDisposable
    {
        internal Action? OnDispose { get; init; }
        public void Dispose()
        {
            OnDispose?.Invoke();
        }
    }

    public ScopeToken BeginBuild(string rid, string frameworkVersion)
    {
        return new ScopeToken();
    }

    public ScopeToken BeginProject(string projectPath)
    {
        if (!Path.IsPathFullyQualified(projectPath))
        {
            _logger.LogError("Project path is not fully qualified: {ProjectPath}", projectPath);
            throw new InvalidOperationException("Project path must be fully qualified");
        }
        string prevProjectPath = CurrentProjectBaseDir;
        string? baseDir = Path.GetDirectoryName(projectPath);
        if (baseDir == null)
        {
            _logger.LogError("Failed to get directory name for project path {ProjectPath}", projectPath);
            throw new InvalidOperationException("Failed to get directory name for project path");
        }
        CurrentProjectBaseDir = baseDir;
        return new ScopeToken { OnDispose = () => CurrentProjectBaseDir = prevProjectPath };
    }

    public ScopeToken BeginAotCompilation(string taskName)
    {
        return new ScopeToken();
    }


    private string RootedPathToRelative(string onDiskPath)
    {
        string? root = Path.GetPathRoot(onDiskPath);
        if (string.IsNullOrEmpty(root))
        {
            _logger.LogError("Failed to get root for path {OnDiskPath}", onDiskPath);
            throw new InvalidOperationException("Failed to get root for path");
        }
        string relative = Path.GetRelativePath(relativeTo: root, path: onDiskPath);
        _logger.LogDebug("Rooted asset path {OnDiskPath} stored as relative {Relative}", onDiskPath, relative);
        return relative;
    }

    private AssetPath ToolingPathFromDiskPath(string onDiskPath, AssetKind kind)
    {
        if (!IsToolingKind(kind))
        {
            _logger.LogError("Asset kind {Kind}  for asset {OnDiskPath} is not a tooling kind", kind, onDiskPath);
            throw new InvalidOperationException($"Invalid asset kind {kind}");
        }
        if (!Path.IsPathRooted(onDiskPath))
        {
            _logger.LogError("Tooling asset path is not rooted: {OnDiskPath}", onDiskPath);
            throw new NotImplementedException("TODO: implement relative tool paths");
        }
        string relative = RootedPathToRelative(onDiskPath);
        (ImmutableList<string> subfolders, string filename) = SplitPath(relative);
        subfolders = subfolders.Insert(0, "tools");
        return new AssetPath { Subfolders = subfolders, Filename = filename };
    }
    public bool TryAddToolingAsset(string onDiskPath, AssetKind kind, [NotNullWhen(true)] out AssetPath? outAssetPath)
    {
        AssetPath result = ToolingPathFromDiskPath(onDiskPath, kind);
        if (!_assets.TryAdd(result, new Asset { OriginalPath = onDiskPath, Kind = kind }))
        {
            outAssetPath = null;
            return false;
        } 
        outAssetPath = result;
        return true;
    }

    public AssetPath GetOrAddToolingAsset(string onDiskPath, AssetKind kind)
    {
        AssetPath result = ToolingPathFromDiskPath(onDiskPath, kind);
        if (_assets.TryGetValue(result, out var asset))
        {
            if (asset.Kind != kind)
            {
                _logger.LogError("Asset kind mismatch for asset {OnDiskPath}: expected {ExpectedKind}, got {ActualKind}", onDiskPath, kind, asset.Kind);
                throw new InvalidOperationException($"Asset kind mismatch for asset {onDiskPath}");
            }
            return result;
        } else {
            _assets.Add(result, new Asset { OriginalPath = onDiskPath, Kind = kind });
            return result;
        }
    }

    private AssetPath InputPathFromOnDiskPath(string onDiskPath, AssetKind kind, out string absoluteOnDiskPath)
    {
        if (!IsInputKind(kind))
        {
            _logger.LogError("Asset kind {Kind} for asset {OnDiskPath} is not an input kind", kind, onDiskPath);
            throw new InvalidOperationException($"Invalid asset kind {kind}");
        }
        if (Path.IsPathRooted(onDiskPath))
        {
            string relative = RootedPathToRelative(onDiskPath);
            (ImmutableList<string> subfolders, string filename) = SplitPath(relative);
            absoluteOnDiskPath = onDiskPath;
            subfolders = subfolders.Insert(0, "input");
            return new AssetPath { Subfolders = subfolders, Filename = filename };
        }
        else
        {
            (ImmutableList<string> subfolders, string filename) = SplitPath(onDiskPath);
            absoluteOnDiskPath = Path.GetFullPath(onDiskPath, basePath: CurrentProjectBaseDir);
            subfolders = subfolders.Insert(0, "input");
            return new AssetPath { Subfolders = subfolders, Filename = filename };
        }
    }

    public bool TryAddInputAsset(string onDiskPath, AssetKind kind, [NotNullWhen(true)] out AssetPath? outAssetPath)
    {
        AssetPath result = InputPathFromOnDiskPath(onDiskPath, kind, out string absoluteOnDiskPath);
        if (!_assets.TryAdd(result, new Asset { OriginalPath = absoluteOnDiskPath, Kind = kind}))
        {
            outAssetPath = null;
            return false;
        }
        outAssetPath = result;
        return true;
    }

    public AssetPath GetOrAddInputAsset(string onDiskPath, AssetKind kind)
    {
        AssetPath result = InputPathFromOnDiskPath(onDiskPath, kind, out string absoluteOnDiskPath);
        if (_assets.TryGetValue(result, out var asset))
        {
            if (asset.Kind != kind)
            {
                _logger.LogError("Asset kind mismatch for asset {OnDiskPath}: expected {ExpectedKind}, got {ActualKind}", onDiskPath, kind, asset.Kind);
                throw new InvalidOperationException($"Asset kind mismatch for asset {onDiskPath}");
            }
            return result;
        } else {
            _assets.Add(result, new Asset { OriginalPath = absoluteOnDiskPath, Kind = kind });
            return result;
        }
    }

    private static (ImmutableList<string> subfolders, string filename) SplitPath(string relativePath)
    {
        string[] parts = relativePath.Split(Path.DirectorySeparatorChar);
        string[] subfolders = parts[..^1];
        string filename = parts[^1];
        return (ImmutableList.Create(subfolders), filename);
    }

    internal interface AssetArchiveItem
    {
        bool HasChildren { get; }
        string Name { get; }
        IEnumerable<AssetArchiveItem> Children { get; }
    }

    internal class AssetArchiveFolder : AssetArchiveItem
    {
        public bool HasChildren => _children.Count > 0;
        public string Name { get; }
        public IEnumerable<AssetArchiveItem> Children => _children;

        private readonly List<AssetArchiveItem> _children = new List<AssetArchiveItem>();
        public AssetArchiveFolder(string name)
        {
            Name = name;
        }
        public void Add(AssetArchiveItem item)
        {
            _children.Add(item);
        }
    }

    internal class AssetArchiveRoot : AssetArchiveFolder
    {
        private readonly List<AssetArchiveItem> _children = new List<AssetArchiveItem>();
        public AssetArchiveRoot() : base (string.Empty)
        {
        }

        public void AddRecursively(AssetPath assetPath, Asset asset)
        {
            AssetArchiveFolder current = this;
            if (assetPath.Subfolders != null) {
                foreach (var folder in assetPath.Subfolders)
                {
                    var next = current.Children.OfType<AssetArchiveFolder>().FirstOrDefault(f => f.Name == folder);
                    if (next == null)
                    {
                        next = new AssetArchiveFolder(folder);
                        current.Add(next);
                    }
                    current = next;
                }
            }
            current.Add(new AssetArchiveFile(assetPath.Filename));
        }
    }


    internal class AssetArchiveFile : AssetArchiveItem
    {
        public bool HasChildren => false;
        public string Name { get; }
        public IEnumerable<AssetArchiveItem> Children => Enumerable.Empty<AssetArchiveItem>();

        public AssetArchiveFile(string name)
        {
            Name = name;
        }
    }

    internal void Dump()
    {
        AssetArchiveRoot root = new AssetArchiveRoot();
        foreach (var (assetPath, asset) in _assets)
        {
            root.AddRecursively(assetPath, asset);
        }

        List<bool> indent = new List<bool>();
        StringBuilder sb = new();
        Dump(sb, root, indent);
        _logger.LogDebug(sb.ToString());
    }

    internal void FormatIndent(StringBuilder sb, IReadOnlyList<bool> isLast)
    {
        const string closeCorner = "└─";
        const string openCorner = "├─";
        const string verticalLine = "│";
        const string space = " ";
        const string dash = "─";
        if (isLast.Count == 0)
            return;
        for (int i = 0; i < isLast.Count - 1; i++)
        {
            sb.Append(isLast[i] ? space : verticalLine);
            sb.Append(space);
            sb.Append(space);
        }
        sb.Append(isLast[isLast.Count - 1] ? closeCorner : openCorner);
        sb.Append(dash);
    }

    internal void Dump(StringBuilder sb, AssetArchiveFolder folder, List<bool> level)
    {
        const string folderIcon = "📁 ";
        const string fileIcon = "📄 ";
        foreach (var child in folder.Children)
        {
            bool newLevelIsLast = child == folder.Children.Last(); // TODO: optimize
            level.Add(newLevelIsLast);
            FormatIndent(sb, level);
            if (child is AssetArchiveFolder childFolder)
            {
                sb.Append(folderIcon);
                sb.AppendLine(child.Name);
                Dump(sb, childFolder, level);
            }
            else if (child is AssetArchiveFile childFile)
            {
                sb.Append (fileIcon);
                sb.AppendLine(child.Name);
            } else {
                throw new InvalidOperationException("Unexpected child type");
            }
            level.RemoveAt(level.Count - 1);
        }
    }

    public bool Archive(string outputPath)
    {
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (assetPath, asset) in _assets)
        {
            ImmutableList<string> entryPath = assetPath.Subfolders.Add(assetPath.Filename);
            string entryName = string.Join(Path.DirectorySeparatorChar, entryPath);
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(asset.OriginalPath, FileMode.Open, FileAccess.Read);
            fileStream.CopyTo(entryStream);
        }
        return true;
    }
}