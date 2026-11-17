using System.Text.Json.Nodes;

namespace MyApp.Monitor.Investigator;

/// <summary>An export of one tier: the context length it runs with and the memory it needs (models.md 8.1).</summary>
/// <param name="ToolRole">The role the result turns travel with: <c>user</c> or <c>tool</c> (the <c>tool_role</c> the pipeline records with the base model).</param>
public sealed record TierExport(string Tier, int ContextLength, int MemoryMb, string ToolRole = Protocol.ProtocolToolRole);

/// <summary>The <c>investigator</c> entry of <c>models/model.lock</c> as the monitor reads it (operations.md 11.2, models.md 7.5).</summary>
public sealed class InvestigatorLock
{
    private readonly Dictionary<string, TierExport> _tiers;
    private readonly Dictionary<string, JsonObject?> _kwargs;

    private InvestigatorLock(string version, string status, string? protocolSha, Dictionary<string, TierExport> tiers, Dictionary<string, JsonObject?> kwargs)
    {
        ModelVersion = version;
        Status = status;
        ProtocolSha256 = protocolSha;
        _tiers = tiers;
        _kwargs = kwargs;
    }

    public string ModelVersion { get; }
    public string Status { get; }
    public string? ProtocolSha256 { get; }
    public IReadOnlyDictionary<string, TierExport> Tiers => _tiers;

    /// <summary><c>chat_template_kwargs</c> of the base model of a tier (reasoning off and the like), passed to every request.</summary>
    public JsonObject? TemplateKwargs(string tier) => _kwargs.GetValueOrDefault(tier);

    /// <summary>Reads the entry. Null with a reason when the file is missing, does not parse, or has no usable investigator entry.</summary>
    public static InvestigatorLock? TryRead(string path, out string problem)
    {
        problem = "";
        JsonNode? root;
        try { root = MonitorJson.Parse(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            problem = $"the model lock {path} cannot be read: {ex.Message}";
            return null;
        }
        if (root?["models"]?["investigator"] is not JsonObject entry) { problem = $"the model lock {path} has no investigator entry"; return null; }
        var version = MonitorJson.ScalarText(entry["model_version"]);
        if (string.IsNullOrWhiteSpace(version)) { problem = "the investigator entry of the model lock has no model_version"; return null; }

        var tiers = new Dictionary<string, TierExport>(StringComparer.Ordinal);
        foreach (var x in (entry["exports"] as JsonArray) ?? [])
        {
            if (x is not JsonObject export || MonitorJson.ScalarText(export["runtime"]) != "server" || MonitorJson.ScalarText(export["tier"]) is not ("small" or "large") ) continue;
            var tier = MonitorJson.ScalarText(export["tier"])!;
            if (export["context_length"] is not JsonValue c || !c.TryGetValue<int>(out var context) || context <= 0) continue;
            if (export["memory_mb"] is not JsonValue m || !m.TryGetValue<int>(out var memory) || memory <= 0) continue;
            var baseModel = (entry["tiers"]?[tier]?["base_model"] ?? entry["base_model"]) as JsonObject;
            tiers[tier] = new TierExport(tier, context, memory, MonitorJson.ScalarText(baseModel?["tool_role"]) == "user" ? "user" : Protocol.ProtocolToolRole);
        }
        if (tiers.Count == 0) { problem = "the investigator entry of the model lock has no server export with a tier, context_length and memory_mb"; return null; }

        var kwargs = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var tier in tiers.Keys)
            kwargs[tier] = ((entry["tiers"]?[tier]?["base_model"] ?? entry["base_model"])?["chat_template_kwargs"] as JsonObject)?.DeepClone() as JsonObject;
        return new InvestigatorLock(version, MonitorJson.ScalarText(entry["status"]) ?? "", MonitorJson.ScalarText(entry["protocol"]?["sha256"]), tiers, kwargs);
    }
}
