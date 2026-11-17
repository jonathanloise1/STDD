using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Pack;

public sealed class PackFileEntry
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class PackManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string PackVersion { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Profile { get; set; } = "";
    public SortedDictionary<string, int> Contracts { get; set; } = new(StringComparer.Ordinal);
    public List<PackFileEntry> Files { get; set; } = [];
}

/// <summary>Contract versions this monitor build was made for (docs/contracts.json). A pack built for other versions is refused.</summary>
public static class PackContracts
{
    public const int PackFormat = 1;

    public static readonly IReadOnlyDictionary<string, int> Expected = new SortedDictionary<string, int>(StringComparer.Ordinal)
    {
        ["area-index"] = 1,
        ["area-layout"] = 1,
        ["baseline"] = 1,
        ["code-tags"] = 1,
        ["identifiers"] = 1,
        ["roadmap-features"] = 1,
        ["test-case"] = 1,
        ["test-suite"] = 1,
        ["user-story"] = 1,
    };
}

/// <summary>The files of a pack by relative path (forward slashes), read from a directory or a zip.</summary>
public sealed class PackFiles : Dictionary<string, byte[]>
{
    public PackFiles() : base(StringComparer.Ordinal) { }

    public string? Text(string path) => TryGetValue(path, out var b) ? new UTF8Encoding(false).GetString(b) : null;

    public static PackFiles Read(string path)
    {
        var files = new PackFiles();
        if (Directory.Exists(path))
        {
            var root = Path.GetFullPath(path);
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                files[Path.GetRelativePath(root, f).Replace('\\', '/')] = File.ReadAllBytes(f);
        }
        else if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(path);
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                files[e.FullName] = ms.ToArray();
            }
        }
        else throw new MonitorException($"operations pack not found: {path}");
        return files;
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

/// <summary>A loaded, verified operations pack (operations.md section 13).</summary>
public sealed class OperationsPack
{
    public required PackManifest Manifest { get; init; }
    public required IReadOnlyList<Signature> Signatures { get; init; }
    public required ToolCatalog Tools { get; init; }
    public required EntityCatalog Entities { get; init; }
    public required MaskingPolicy Masking { get; init; }
    public required ConstraintSet Constraints { get; init; }
    public required JsonObject ProfileRaw { get; init; }
    public required string ProtocolText { get; init; }
    public required IReadOnlyDictionary<string, string> Templates { get; init; }
    public required IReadOnlyDictionary<string, string> Runbooks { get; init; }
    public required JsonNode SignatureSchema { get; init; }
    public required JsonNode IncidentReportSchema { get; init; }
    public string? Location { get; init; }

    public string Version => Manifest.PackVersion;

    /// <summary>The profile with <c>${setting:...}</c> resolved from the monitor's configuration.</summary>
    public Profile GetProfile(IConfiguration? settings) => ProfileLoader.Bind(ProfileRaw, settings);

    public Signature? FindByLogKey(string key) => Signatures.FirstOrDefault(s => s.IsKeyed && s.Id == key);
    public Signature? FindByTemplateId(string id) => Signatures.FirstOrDefault(s => !s.IsKeyed && s.Id == id);
}

/// <summary>Where the current pack is held; the monitor replaces it between batches when a new valid pack appears (13.3).</summary>
public interface IPackProvider
{
    OperationsPack Current { get; }
}

public sealed class StaticPackProvider(OperationsPack pack) : IPackProvider
{
    public OperationsPack Current { get; } = pack;
}

public sealed class PackException(string message, IReadOnlyList<Finding> findings) : MonitorException(message)
{
    public IReadOnlyList<Finding> Findings { get; } = findings;
}

public static class PackLoader
{
    /// <summary>Loads and verifies a pack (directory or zip): hashes, contract versions, formats. Refuses an invalid pack.</summary>
    public static OperationsPack Load(string path)
    {
        var files = PackFiles.Read(path);
        var result = PackVerifier.Verify(files, new PackVerifier.Options(DateOnly.FromDateTime(DateTime.UtcNow), CheckNoiseExpiry: false, CheckTemplates: false) { Location = path });
        if (result.Pack is null || result.Findings.Count > 0)
            throw new PackException($"operations pack refused ({result.Findings.Count} problem(s)): " + string.Join("; ", result.Findings.Take(5).Select(f => f.ToString())), result.Findings);
        return result.Pack;
    }
}

internal static class PackJson
{
    public static byte[] ToBytes(JsonNode node) => new UTF8Encoding(false).GetBytes(node.ToJsonString(MonitorJson.Snake) + "\n");
    public static byte[] ToBytes<T>(T value) => new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(value, MonitorJson.Snake) + "\n");
}
