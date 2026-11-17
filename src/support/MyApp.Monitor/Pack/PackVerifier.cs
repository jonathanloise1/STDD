using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Pack;

public sealed record VerifyResult(FindingList Findings, OperationsPack? Pack);

/// <summary>
/// <c>pack verify</c> (operations.md 13.2, 16): manifest, hashes and contract versions (13.3), then the checks
/// O06 to O10 and the constraints enforced by <c>pack_verify</c> (C01, C04, C05, C07, C10) on a built pack.
/// O04 and O05 need the log definitions, which a pack does not carry: they run when the pack is built.
/// </summary>
public static class PackVerifier
{
    public sealed record Options(DateOnly Today, bool CheckNoiseExpiry = true, bool CheckTemplates = true)
    {
        public string? Location { get; init; }
    }

    private static readonly string[] RequiredFiles =
    [
        "signatures.json", "tools.json", "entities.json", "masking-policy.json", "constraints.json", "profile.yaml",
        "protocol.md", "incident-report.schema.json", "signature.schema.json",
    ];

    private static readonly Regex[] ConnectionStringPatterns =
    [
        new(@"(?i)\b(server|data source|host)\s*=[^;\n]+;[^\n]*\b(password|pwd|user id|uid|integrated security)\s*=", RegexOptions.CultureInvariant),
        new(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", RegexOptions.CultureInvariant),
        new(@"(?i)\b(password|pwd)\s*=\s*[^\s;,]+", RegexOptions.CultureInvariant),
    ];

    public static VerifyResult Verify(PackFiles files, Options options)
    {
        var findings = new FindingList();
        var loc = options.Location;

        // ---- manifest, hashes, contracts (13.3) ----
        PackManifest? manifest = null;
        if (!files.TryGetValue("manifest.json", out var manifestBytes)) findings.Add("PACK-MANIFEST", "manifest.json is missing", loc);
        else
        {
            try { manifest = JsonSerializer.Deserialize<PackManifest>(manifestBytes, MonitorJson.Snake); }
            catch (JsonException ex) { findings.Add("PACK-MANIFEST", $"manifest.json is not valid: {ex.Message}", loc); }
        }
        if (manifest is not null) CheckManifest(manifest, files, findings, loc);

        foreach (var required in RequiredFiles)
            if (!files.ContainsKey(required)) findings.Add("PACK-FILE", $"{required} is missing", loc);

        if (RequiredFiles.Any(r => !files.ContainsKey(r)) || manifest is null) return new VerifyResult(findings, null);

        // ---- JSON documents ----
        JsonNode? Json(string name)
        {
            try { return MonitorJson.Parse(files.Text(name)!); }
            catch (JsonException ex) { findings.Add("O08", $"{name} is not valid JSON: {ex.Message}", name); return null; }
        }

        var toolsNode = Json("tools.json");
        var entitiesNode = Json("entities.json");
        var maskingNode = Json("masking-policy.json");
        var constraintsNode = Json("constraints.json");
        var signaturesNode = Json("signatures.json");
        var signatureSchema = Json("signature.schema.json");
        var incidentSchema = Json("incident-report.schema.json");
        if (new[] { toolsNode, entitiesNode, maskingNode, constraintsNode, signaturesNode, signatureSchema, incidentSchema }.Any(n => n is null))
            return new VerifyResult(findings, null);

        foreach (var (name, schema) in new[] { ("signature.schema.json", signatureSchema), ("incident-report.schema.json", incidentSchema) })
            foreach (var p in SchemaSubsetValidator.CheckSubset(schema!)) findings.Add("O08", p, name);

        var catalog = ToolCatalog.Parse(toolsNode, findings);
        var entities = EntityCatalog.Parse(entitiesNode, findings);
        var masking = MaskingPolicyLoader.Parse(maskingNode, findings);
        var constraints = ConstraintSet.Parse(constraintsNode, findings);

        // profile
        JsonObject? profileRaw = null;
        try { profileRaw = ProfileLoader.ParseYaml(files.Text("profile.yaml")!); }
        catch (Exception ex) when (ex is MonitorException or YamlDotNet.Core.YamlException)
        {
            findings.Add("O08", $"profile.yaml is not valid: {ex.Message}", "profile.yaml");
        }
        if (profileRaw is not null && MonitorJson.ScalarText(profileRaw["name"]) != manifest.Profile)
            findings.Add("O08", $"profile.yaml name must equal the manifest profile '{manifest.Profile}'", "profile.yaml");
        if (profileRaw is not null) ProfileLoader.Validate(profileRaw, manifest.Profile, findings, catalog, "profile.yaml");

        // signatures
        var signatures = new List<Signature>();
        if (signaturesNode is JsonObject sigObj)
        {
            if (MonitorJson.ScalarText(sigObj["schema_version"]) != "1") findings.Add("O08", "schema_version must be 1", "signatures.json");
            var validator = new SchemaSubsetValidator(signatureSchema!);
            foreach (var item in (sigObj["signatures"] as JsonArray) ?? [])
            {
                var errors = validator.Validate(item);
                var id = MonitorJson.ScalarText(item?["id"]) ?? "?";
                foreach (var e in errors) findings.Add("O08", $"signature '{id}' violates signature.schema.json: {e}", "signatures.json");
                if (errors.Count == 0)
                    signatures.Add(item.Deserialize<Signature>(MonitorJson.Snake)!);
            }
        }
        else findings.Add("O08", "signatures.json must be an object", "signatures.json");
        for (var i = 1; i < signatures.Count; i++)
            if (string.CompareOrdinal(signatures[i - 1].Id, signatures[i].Id) >= 0)
                findings.Add("O06", $"signatures must be ordered by unique id: '{signatures[i].Id}' after '{signatures[i - 1].Id}'", "signatures.json");

        CrossChecks(catalog, entities, masking, constraints, profileRaw, findings);
        CheckRunbooks(files, signatures, catalog, options, findings);
        CheckTemplatesAndExample(files, incidentSchema!, options, findings);
        CheckSecrets(files, findings);

        var protocol = files.Text("protocol.md")!;
        if (string.IsNullOrWhiteSpace(protocol)) findings.Add("O09", "protocol.md is empty", "protocol.md");
        if (protocol.Contains("<!-- protocol:begin -->") || protocol.Contains("<!-- protocol:end -->"))
            findings.Add("O09", "protocol.md must hold the block without the markers", "protocol.md");

        if (findings.Count > 0) return new VerifyResult(findings, null);

        var pack = new OperationsPack
        {
            Manifest = manifest,
            Signatures = signatures,
            Tools = catalog,
            Entities = entities,
            Masking = masking,
            Constraints = constraints,
            ProfileRaw = profileRaw!,
            ProtocolText = protocol,
            Templates = files.Where(f => f.Key.StartsWith("templates/", StringComparison.Ordinal))
                .ToDictionary(f => f.Key["templates/".Length..], f => files.Text(f.Key)!, StringComparer.Ordinal),
            Runbooks = files.Where(f => f.Key.StartsWith("runbooks/", StringComparison.Ordinal))
                .ToDictionary(f => f.Key["runbooks/".Length..], f => files.Text(f.Key)!, StringComparer.Ordinal),
            SignatureSchema = signatureSchema!,
            IncidentReportSchema = incidentSchema!,
            Location = options.Location,
        };
        return new VerifyResult(findings, pack);
    }

    private static void CheckManifest(PackManifest manifest, PackFiles files, FindingList findings, string? loc)
    {
        if (manifest.SchemaVersion != PackContracts.PackFormat)
            findings.Add("PACK-CONTRACT", $"pack format {manifest.SchemaVersion} differs from the format {PackContracts.PackFormat} this monitor reads", loc);
        if (string.IsNullOrWhiteSpace(manifest.PackVersion)) findings.Add("PACK-MANIFEST", "pack_version is empty", loc);
        if (string.IsNullOrWhiteSpace(manifest.Profile)) findings.Add("PACK-MANIFEST", "profile is empty", loc);
        if (!DateTimeOffset.TryParse(manifest.CreatedAt, out _)) findings.Add("PACK-MANIFEST", "created_at is not a timestamp", loc);

        foreach (var (key, expected) in PackContracts.Expected)
        {
            if (!manifest.Contracts.TryGetValue(key, out var actual))
                findings.Add("PACK-CONTRACT", $"contract '{key}' is missing from the manifest (this monitor expects version {expected})", loc);
            else if (actual != expected)
                findings.Add("PACK-CONTRACT", $"contract '{key}' is version {actual} in the pack; this monitor was built for version {expected}", loc);
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Files)
        {
            if (!listed.Add(entry.Path)) findings.Add("PACK-HASH", $"{entry.Path} is listed twice in the manifest", loc);
            if (!files.TryGetValue(entry.Path, out var bytes)) findings.Add("PACK-HASH", $"{entry.Path} is listed in the manifest but missing", loc);
            else if (!string.Equals(PackFiles.Sha256Hex(bytes), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                findings.Add("PACK-HASH", $"{entry.Path} does not match its hash in the manifest", loc);
        }
        foreach (var path in files.Keys)
            if (path != "manifest.json" && !listed.Contains(path)) findings.Add("PACK-HASH", $"{path} is not listed in the manifest", loc);
    }

    private static void CrossChecks(
        ToolCatalog catalog, EntityCatalog entities, MaskingPolicy masking, ConstraintSet constraints, JsonObject? profile, FindingList findings)
    {
        // O08: masking and entities agree; every column is a plain identifier
        foreach (var entity in entities.Entities)
            if (!masking.Entities.ContainsKey(entity.Name))
                findings.Add("O08", $"entity '{entity.Name}' has no section in the masking policy", "masking-policy.json");
        foreach (var (name, section) in masking.Entities)
        {
            if (entities.Find(name) is null) findings.Add("O08", $"masking policy has a section for unknown entity '{name}'", "masking-policy.json");
            foreach (var column in section.Keys)
                if (!EntityCatalog.Identifier.IsMatch(column))
                    findings.Add("O08", $"entities.{name}.{column} is not a plain identifier and cannot be selected", "masking-policy.json");
            var key = entities.Find(name)?.Key;
            if (key is not null && !section.Keys.Any(c => c.Equals(key, StringComparison.OrdinalIgnoreCase)))
                findings.Add("O08", $"entities.{name} must include the key column '{key}'", "masking-policy.json");
        }

        // O08: every tool output field has a class
        foreach (var tool in catalog.Tools)
        {
            if (tool.MaskingSection.StartsWith("tools.", StringComparison.Ordinal))
            {
                if (!masking.Tools.TryGetValue(tool.Name, out var section) || section.Count == 0)
                {
                    findings.Add("O08", $"tool '{tool.Name}' has no masking section 'tools.{tool.Name}'", "masking-policy.json");
                    continue;
                }
                if (ToolOutputFields.ByTool.TryGetValue(tool.Name, out var fields))
                    foreach (var field in fields)
                        if (!section.ContainsKey(field))
                            findings.Add("O08", $"output field '{field}' of tool '{tool.Name}' has no class", "masking-policy.json");
            }
            // entity bound tools need an implementation for every entity source kind
            if (tool.Family is "entity" or "audit")
                foreach (var entity in entities.Entities)
                    if (tool.ImplementationFor(entity.SourceKind) is null)
                        findings.Add("O08", $"tool '{tool.Name}' has no implementation for source kind '{entity.SourceKind}' of entity '{entity.Name}'", "tools.json");
        }

        // constraints enforced by pack_verify
        var allowed = constraints.AllowedAccess;
        foreach (var tool in catalog.Tools)
        {
            if (!allowed.Contains(tool.Access)) findings.Add("C01", $"tool '{tool.Name}' has access '{tool.Access}'; only {string.Join(", ", allowed)} is allowed", "tools.json");
            if (constraints.MaxLookbackHours is { } max && tool.Limits.LookbackHours is { } hours && hours > max)
                findings.Add("C04", $"tool '{tool.Name}' looks back {hours} hours; the limit is {max}", "tools.json");
        }
        if (profile?["tool_sources"] is JsonObject bound && profile["sources"] is JsonArray)
            foreach (var kv in bound)
                if (catalog.Find(kv.Key) is null)
                    findings.Add("C10", $"profile binds tool '{kv.Key}', which the pack does not offer", "profile.yaml");
        foreach (var tool in catalog.Tools)
            if (profile?["tool_sources"] is JsonObject b && !b.ContainsKey(tool.Name))
                findings.Add("C10", $"tool '{tool.Name}' is in the pack but not bound by the profile", "tools.json");
    }

    private static void CheckRunbooks(PackFiles files, List<Signature> signatures, ToolCatalog catalog, Options options, FindingList findings)
    {
        var parserOptions = new RunbookParser.Options(options.Today, catalog.Names);
        foreach (var sig in signatures)
        {
            var path = $"runbooks/{sig.Runbook.Anchor}.md";
            if (sig.Runbook.Anchor != sig.Id && !sig.IsKeyed) findings.Add("O07", $"template signature '{sig.Id}' must use its id as runbook anchor", "signatures.json");
            var text = files.Text(path);
            if (text is null) { findings.Add(sig.IsKeyed ? "O06" : "O07", $"runbook file {path} is missing", path); continue; }

            var local = new FindingList();
            RunbookDocument doc;
            if (sig.IsKeyed) doc = RunbookParser.ParseArea("# Area - Operations\n\n" + text, path, parserOptions, local);
            else doc = RunbookParser.ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\n" + text, path, parserOptions, local);
            foreach (var f in local)
            {
                if (f.Check == "O10" && !options.CheckNoiseExpiry) continue;
                findings.Add(f);
            }
            if (doc.Entries.Count == 1)
            {
                if (doc.Entries[0].Text != sig.Runbook.Text)
                    findings.Add(sig.IsKeyed ? "O06" : "O07", $"signature '{sig.Id}': runbook text differs from {path}", path);
                if (doc.Entries[0].Anchor != sig.Runbook.Anchor)
                    findings.Add(sig.IsKeyed ? "O06" : "O07", $"signature '{sig.Id}': runbook anchor differs from {path}", path);
            }
            else if (local.Count == 0) findings.Add(sig.IsKeyed ? "O06" : "O07", $"{path} must hold exactly one runbook section", path);
        }
    }

    private static void CheckTemplatesAndExample(PackFiles files, JsonNode incidentSchema, Options options, FindingList findings)
    {
        if (!options.CheckTemplates) return;
        var exampleText = ReadEmbeddedExample();
        if (exampleText is null) { findings.Add("O10", "the example incident report is not embedded in this monitor build"); return; }
        var example = MonitorJson.Parse(exampleText)!;
        foreach (var error in new SchemaSubsetValidator(incidentSchema).Validate(example))
            findings.Add("O08", $"the example incident report violates incident-report.schema.json: {error}", "incident-report.example.json");

        var dashboardUrl = "http://localhost:5310/incidents/" + MonitorJson.ScalarText(example["id"]);
        foreach (var (path, bytes) in files.Where(f => f.Key.StartsWith("templates/", StringComparison.Ordinal)).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var rendered = TemplateRenderer.Render(new System.Text.UTF8Encoding(false).GetString(bytes), example, dashboardUrl);
            foreach (var unknown in rendered.UnknownPaths) findings.Add("O10", $"template uses unknown path '{unknown}'", path);
            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                try { JsonNode.Parse(rendered.Text); }
                catch (JsonException ex) { findings.Add("O10", $"the rendered ticket template is not valid JSON: {ex.Message}", path); }
            }
        }
    }

    public static string? ReadEmbeddedExample()
    {
        using var s = typeof(PackVerifier).Assembly.GetManifestResourceStream("incident-report.example.json");
        if (s is null) return null;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private static void CheckSecrets(PackFiles files, FindingList findings)
    {
        // C07: no credentials, connection strings or tokens inside the pack
        foreach (var (path, bytes) in files)
        {
            if (path.EndsWith(".zip", StringComparison.Ordinal)) continue;
            var text = new System.Text.UTF8Encoding(false).GetString(bytes);
            foreach (var pattern in ConnectionStringPatterns)
                if (pattern.IsMatch(text))
                {
                    findings.Add("C07", "the pack must not contain credentials, connection strings or tokens", path);
                    break;
                }
        }
    }
}
