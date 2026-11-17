using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Pack;

public sealed record PackBuildRequest(string RepositoryRoot, string Profile, string? Version, string OutputDirectory)
{
    /// <summary>Folder name inside the output directory. Default: the profile name; <c>run</c> uses <c>local</c>.</summary>
    public string? DirectoryName { get; init; }
    public bool WriteZip { get; init; } = true;
    /// <summary>A profile file outside docs/support/profiles (local use, <c>Monitor:Profile</c> as a path).</summary>
    public string? ProfileFile { get; init; }
}

public sealed class PackBuildResult
{
    public FindingList Findings { get; } = [];
    public bool Succeeded => Findings.Count == 0;
    public string? PackDirectory { get; set; }
    public string? ZipPath { get; set; }
    public PackManifest? Manifest { get; set; }
    public int SignatureCount { get; set; }
}

/// <summary>The pack builder (operations.md 13.2): generate signatures, copy files, hash, write the manifest, then verify.</summary>
public sealed class PackBuilder(TimeProvider time)
{
    public const string ProtocolBegin = "<!-- protocol:begin -->";
    public const string ProtocolEnd = "<!-- protocol:end -->";
    private const string AgentFile = ".github/agents/support.agent.md";

    public PackBuildResult Build(PackBuildRequest request)
    {
        var result = new PackBuildResult();
        var findings = result.Findings;
        var repo = Path.GetFullPath(request.RepositoryRoot);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        string Support(string name) => Path.Combine(repo, "docs", "support", name);
        byte[]? Read(string path, string display)
        {
            if (File.Exists(path)) return File.ReadAllBytes(path);
            findings.Add("PACK-FILE", $"{display} not found", display);
            return null;
        }

        var toolsBytes = Read(Support("tools.json"), "docs/support/tools.json");
        var entitiesBytes = Read(Support("entities.json"), "docs/support/entities.json");
        var maskingBytes = Read(Support("masking-policy.json"), "docs/support/masking-policy.json");
        var constraintsBytes = Read(Support("constraints.json"), "docs/support/constraints.json");
        var sigSchemaBytes = Read(Support("signature.schema.json"), "docs/support/signature.schema.json");
        var incSchemaBytes = Read(Support("incident-report.schema.json"), "docs/support/incident-report.schema.json");
        var profileBytes = request.ProfileFile is not null
            ? Read(request.ProfileFile, request.ProfileFile)
            : Read(Path.Combine(Support("profiles"), request.Profile + ".yaml"), $"docs/support/profiles/{request.Profile}.yaml");
        var contractsBytes = Read(Path.Combine(repo, "docs", "contracts.json"), "docs/contracts.json");
        if (findings.Count > 0) return result;

        var utf8 = new UTF8Encoding(false);
        JsonNode? toolsNode = null, signatureSchema = null;
        try
        {
            toolsNode = MonitorJson.Parse(utf8.GetString(toolsBytes!));
            signatureSchema = MonitorJson.Parse(utf8.GetString(sigSchemaBytes!));
        }
        catch (System.Text.Json.JsonException ex) { findings.Add("O08", $"a support file is not valid JSON: {ex.Message}"); return result; }

        var catalog = ToolCatalog.Parse(toolsNode, findings);
        if (findings.Count > 0) return result;

        // profile -> the tools the pack offers
        JsonObject profileRaw;
        try { profileRaw = ProfileLoader.ParseYaml(utf8.GetString(profileBytes!)); }
        catch (Exception ex) when (ex is MonitorException or YamlDotNet.Core.YamlException)
        {
            findings.Add("O08", $"profile '{request.Profile}' is not valid: {ex.Message}", $"docs/support/profiles/{request.Profile}.yaml");
            return result;
        }
        var profileTools = ((profileRaw["tool_sources"] as JsonObject)?.Select(kv => kv.Key) ?? []).ToHashSet(StringComparer.Ordinal);

        // contracts
        var contracts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (MonitorJson.Parse(utf8.GetString(contractsBytes!)) is JsonObject cj && cj["contracts"] is JsonObject co)
            foreach (var kv in co)
                if (kv.Value?["version"] is JsonValue v && v.TryGetValue<int>(out var ver)) contracts[kv.Key] = ver;
        foreach (var (key, expected) in PackContracts.Expected)
            if (!contracts.TryGetValue(key, out var actual) || actual != expected)
                findings.Add("PACK-CONTRACT", $"docs/contracts.json has '{key}' at version {(contracts.TryGetValue(key, out var a) ? a : 0)}; this monitor was built for version {expected}", "docs/contracts.json");

        // protocol block (O09)
        var protocol = ReadProtocol(repo, findings);

        // signatures (5.2)
        var generation = SignatureGenerator.Generate(repo, today, catalog.Names, signatureSchema);
        findings.AddRange(generation.Findings);
        result.SignatureCount = generation.Table.Signatures.Count;
        if (findings.Count > 0) return result;

        // assemble files
        var files = new PackFiles();
        files["signatures.json"] = PackJson.ToBytes(generation.Table);
        foreach (var (anchor, markdown) in generation.RunbookSections) files[$"runbooks/{anchor}.md"] = utf8.GetBytes(markdown);
        files["tools.json"] = PackJson.ToBytes(ToolCatalog.Reduce(toolsNode!, profileTools));
        files["entities.json"] = entitiesBytes!;
        files["masking-policy.json"] = maskingBytes!;
        files["constraints.json"] = constraintsBytes!;
        files["profile.yaml"] = profileBytes!;
        files["protocol.md"] = utf8.GetBytes(protocol! + "\n");
        files["incident-report.schema.json"] = incSchemaBytes!;
        files["signature.schema.json"] = sigSchemaBytes!;
        var templatesDir = Support("templates");
        if (Directory.Exists(templatesDir))
            foreach (var f in Directory.GetFiles(templatesDir).OrderBy(f => f, StringComparer.Ordinal))
                files["templates/" + Path.GetFileName(f)] = File.ReadAllBytes(f);

        var version = request.Version ?? DefaultVersion(repo);
        var manifest = new PackManifest
        {
            SchemaVersion = PackContracts.PackFormat,
            PackVersion = version,
            CreatedAt = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            Profile = request.Profile,
            Contracts = contracts,
            Files = files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new PackFileEntry { Path = f.Key, Sha256 = PackFiles.Sha256Hex(f.Value) }).ToList(),
        };
        files["manifest.json"] = PackJson.ToBytes(manifest);
        result.Manifest = manifest;

        // verify before anything is written
        var verify = PackVerifier.Verify(files, new PackVerifier.Options(today) { Location = "(built pack)" });
        findings.AddRange(verify.Findings);
        if (findings.Count > 0) return result;

        // write
        var outRoot = Path.GetFullPath(request.OutputDirectory);
        var dir = Path.Combine(outRoot, request.DirectoryName ?? request.Profile);
        PrepareDirectory(dir);
        foreach (var (path, bytes) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var target = Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
        }
        result.PackDirectory = dir;

        if (request.WriteZip)
        {
            var zipPath = Path.Combine(outRoot, $"{ProjectName(repo)}-ops-pack-{version}-{request.Profile}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                foreach (var (path, bytes) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                    using var s = entry.Open();
                    s.Write(bytes);
                }
            result.ZipPath = zipPath;
        }
        return result;
    }

    private static void PrepareDirectory(string dir)
    {
        if (Directory.Exists(dir))
        {
            // only replace a directory that is empty or already a pack
            if (Directory.EnumerateFileSystemEntries(dir).Any() && !File.Exists(Path.Combine(dir, "manifest.json")))
                throw new MonitorException($"{dir} exists and is not an operations pack; choose another --out");
            Directory.Delete(dir, recursive: true);
        }
        Directory.CreateDirectory(dir);
    }

    private static string? ReadProtocol(string repo, FindingList findings)
    {
        var path = Path.Combine(repo, ".github", "agents", "support.agent.md");
        if (!File.Exists(path)) { findings.Add("O09", $"{AgentFile} not found", AgentFile); return null; }
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var begins = CountOccurrences(text, ProtocolBegin);
        var ends = CountOccurrences(text, ProtocolEnd);
        if (begins != 1 || ends != 1)
        {
            findings.Add("O09", $"the protocol markers must exist exactly once (found {begins} begin, {ends} end)", AgentFile);
            return null;
        }
        var start = text.IndexOf(ProtocolBegin, StringComparison.Ordinal) + ProtocolBegin.Length;
        var end = text.IndexOf(ProtocolEnd, StringComparison.Ordinal);
        if (end < start) { findings.Add("O09", "the protocol end marker comes before the begin marker", AgentFile); return null; }
        return text[start..end].Trim();
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary><c>{project version}+{short commit}</c> (13.1). The project version is read from a <c>Version</c> element under src/backend, else 0.0.0.</summary>
    public static string DefaultVersion(string repo) => $"{ProjectVersion(repo)}+{ShortCommit(repo)}";

    private static string ProjectVersion(string repo)
    {
        foreach (var name in new[] { "Directory.Build.props", Path.Combine("src", "backend", "Directory.Build.props") })
        {
            var path = Path.Combine(repo, name);
            if (!File.Exists(path)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path), @"<Version>\s*([^<\s]+)\s*</Version>");
            if (m.Success) return m.Groups[1].Value;
        }
        return "0.0.0";
    }

    private static string ShortCommit(string repo)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            });
            if (p is null) return "nogit";
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return p.ExitCode == 0 && output.Length > 0 ? output : "nogit";
        }
        catch (Exception) { return "nogit"; }
    }

    public static string ProjectName(string repo)
    {
        var sln = Directory.Exists(Path.Combine(repo, "src", "backend"))
            ? Directory.GetFiles(Path.Combine(repo, "src", "backend"), "*.sln").FirstOrDefault()
            : null;
        return sln is null ? "myapp" : Path.GetFileNameWithoutExtension(sln).ToLowerInvariant();
    }
}
