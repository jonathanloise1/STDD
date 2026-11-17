using System.Text.Json;
using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Pack;

/// <summary>
/// The pack a simulated world directory brings (<c>{world}/pack/</c>: <c>signatures.json</c>, <c>entities.json</c>, <c>masking-policy.json</c>,
/// <c>tools.json</c>, <c>constraints.json</c>, operations.md 11.6). <c>tool --world</c> uses it so that a fictional application's entities, masking and signatures
/// are those the tool call is answered with. It is not a built pack (no manifest, no hashes): only what the executor reads.
/// </summary>
public static class WorldPack
{
    public static bool Exists(string worldDirectory) => File.Exists(Path.Combine(worldDirectory, "pack", "entities.json"));

    public static OperationsPack Load(string worldDirectory)
    {
        var dir = Path.Combine(worldDirectory, "pack");
        string Read(string name) => File.Exists(Path.Combine(dir, name)) ? File.ReadAllText(Path.Combine(dir, name)) : throw new MonitorException($"the world pack has no {name}");
        var table = JsonSerializer.Deserialize<SignatureTable>(Read("signatures.json"), MonitorJson.Snake) ?? new SignatureTable();
        return new OperationsPack
        {
            Manifest = new PackManifest { PackVersion = "world", Profile = "world" },
            Signatures = table.Signatures.OrderBy(s => s.Id, StringComparer.Ordinal).ToList(),
            Tools = ToolCatalog.Load(Read("tools.json")),
            Entities = EntityCatalog.Load(Read("entities.json")),
            Masking = MaskingPolicyLoader.Load(Read("masking-policy.json")),
            Constraints = ConstraintSet.Load(Read("constraints.json")),
            ProfileRaw = new JsonObject { ["schema_version"] = 1, ["name"] = "world", ["environment"] = "world", ["tool_sources"] = new JsonObject() },
            ProtocolText = "",
            Templates = new Dictionary<string, string>(),
            Runbooks = Directory.Exists(Path.Combine(dir, "runbooks"))
                ? Directory.EnumerateFiles(Path.Combine(dir, "runbooks"), "*.md").ToDictionary(f => Path.GetFileName(f), File.ReadAllText, StringComparer.Ordinal)
                : new Dictionary<string, string>(),
            SignatureSchema = new JsonObject(),
            IncidentReportSchema = new JsonObject(),
            Location = dir,
        };
    }
}
