using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MyApp.Application.Help;

/// <summary>A section of a help pack, as much of it as the assistant needs.</summary>
public sealed record PackSection(string Id, string Title, string Text);

/// <summary>One language of the help pack (manual.md 6.3).</summary>
public sealed class HelpPack
{
    public required string Lang { get; init; }

    /// <summary>SHA-256 of the pack file bytes: the value the panel sends as <c>pack_sha256</c>.</summary>
    public required string Sha256 { get; init; }

    public required string ProjectName { get; init; }
    public required string ProjectVersion { get; init; }

    /// <summary><c>assistant.compatible</c> of the pack.</summary>
    public required bool Compatible { get; init; }

    /// <summary><c>assistant.protocol.sha256</c> of the pack.</summary>
    public string? ProtocolSha256 { get; init; }

    /// <summary>The protocol copy the pack carries; null when it is missing or malformed.</summary>
    public AssistantProtocol? Protocol { get; init; }

    public required IReadOnlyDictionary<string, PackSection> Sections { get; init; }
}

/// <summary>The <c>assistant</c> entry of <c>models/model.lock</c> (models.md 8.1).</summary>
public sealed record ModelLockEntry(string ModelId, string ModelVersion, string ProtocolSha256, JObject? ChatTemplateKwargs);

/// <summary>Why generation is off. The codes are the machine form of the reason shown in the status.</summary>
public static class IncompatibleReasons
{
    public const string NoPack = "no_pack";
    public const string PackUnreadable = "pack_unreadable";
    public const string NoLock = "no_lock";
    public const string PackNotCompatible = "pack_not_compatible";
    public const string ProtocolMismatch = "protocol_mismatch";
}

/// <summary>The outcome of the start-up checks of models.md 7.5 (the host alias is checked per request).</summary>
public sealed record AssistantCompatibility(bool Compatible, string? Reason, string? Detail);

/// <summary>
/// The help pack directory and the lock file as read at start-up (models.md 7.5, 7.7). Nothing here throws: no
/// pack, no lock or a damaged file only turns generation off, with the reason logged once, and retrieval keeps
/// working in the browser.
/// </summary>
public sealed class HelpRuntime
{
    /// <summary>
    /// The newest <c>schema_version</c> of pack, manifest and lock this build reads (manual.md 6.8, models.md 8.1): 2 since the
    /// embedding table. The backend never loads the table (models.md 7.8): it only reads the pack's sections, protocol and compatibility,
    /// which are the same in version 1 and 2, so a pack that names a table is accepted unchanged.
    /// </summary>
    public const int KnownSchemaVersion = 2;

    private HelpRuntime(HelpSettings settings, IReadOnlyDictionary<string, HelpPack> packs, ModelLockEntry? lockEntry,
        AssistantCompatibility compatibility)
    {
        Settings = settings;
        Packs = packs;
        Lock = lockEntry;
        Compatibility = compatibility;
    }

    public HelpSettings Settings { get; }

    /// <summary>The packs that loaded, by language.</summary>
    public IReadOnlyDictionary<string, HelpPack> Packs { get; }

    /// <summary>The assistant entry of the lock; null without a usable lock or entry.</summary>
    public ModelLockEntry? Lock { get; }

    public AssistantCompatibility Compatibility { get; }

    /// <summary>The generation tiers may run: compatible, and the order is not <c>retrieval-only</c>.</summary>
    public bool GenerationAllowed => Compatibility.Compatible && Settings.Order != HelpSettings.OrderRetrievalOnly;

    /// <summary>A runtime without any pack or lock (retrieval only), for hosts that do not load files.</summary>
    public static HelpRuntime Empty(HelpSettings settings) => new(settings, new Dictionary<string, HelpPack>(),
        null, new AssistantCompatibility(false, IncompatibleReasons.NoPack, "No help pack is loaded."));

    /// <summary>Reads the pack directory and the lock file and runs the start-up checks; logs the reason once.</summary>
    public static HelpRuntime Load(HelpSettings settings, string contentRoot, ILogger logger)
    {
        var packProblems = new List<(string Reason, string Detail)>();
        var lockProblems = new List<string>();
        var packs = LoadPacks(settings, contentRoot, packProblems);
        var lockEntry = LoadLock(settings, contentRoot, lockProblems);
        var compatibility = Check(packs, lockEntry, packProblems, lockProblems);

        if (compatibility.Compatible)
            logger.LogInformation("Help pack loaded for {Languages}; generation tiers are eligible (model {ModelVersion}).",
                string.Join(", ", packs.Keys.Order(StringComparer.Ordinal)), lockEntry!.ModelVersion);
        else
            logger.LogInformation("Help generation is off ({Reason}): {Detail} The panel runs retrieval only.",
                compatibility.Reason, compatibility.Detail);

        return new HelpRuntime(settings, packs, lockEntry, compatibility);
    }

    // ---------------------------------------------------------------- pack

    private static Dictionary<string, HelpPack> LoadPacks(HelpSettings settings, string contentRoot, List<(string Reason, string Detail)> problems)
    {
        var packs = new Dictionary<string, HelpPack>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(settings.PackDirectory))
        {
            problems.Add((IncompatibleReasons.NoPack, "MyApp:Help:PackDirectory is not set."));
            return packs;
        }

        var directory = Resolve(contentRoot, settings.PackDirectory);
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            problems.Add((IncompatibleReasons.NoPack, $"No manifest.json in the pack directory '{directory}'."));
            return packs;
        }

        JObject manifest;
        try
        {
            manifest = JObject.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            problems.Add((IncompatibleReasons.PackUnreadable, $"manifest.json cannot be read ({e.GetType().Name})."));
            return packs;
        }

        if ((int?)manifest["schema_version"] is not { } schema || schema > KnownSchemaVersion)
        {
            problems.Add((IncompatibleReasons.PackUnreadable, "manifest.json has a schema_version this build does not know."));
            return packs;
        }

        foreach (var entry in (manifest["packs"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
        {
            try
            {
                packs[entry.Name] = LoadPack(directory, entry.Name, entry.Value);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException
                                          or FormatException or NullReferenceException or InvalidCastException)
            {
                problems.Add((IncompatibleReasons.PackUnreadable, $"The pack of '{entry.Name}' cannot be used ({e.Message})."));
            }
        }

        return packs;
    }

    private static HelpPack LoadPack(string directory, string lang, JToken manifestEntry)
    {
        var file = (string?)manifestEntry["file"] ?? throw new FormatException("the manifest names no file");
        var expected = (string?)manifestEntry["sha256"] ?? throw new FormatException("the manifest names no SHA-256");
        var path = Path.GetFullPath(Path.Combine(directory, file));
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new FormatException("the file is outside the pack directory");

        var bytes = File.ReadAllBytes(path);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(sha, expected, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("the file does not match the SHA-256 of the manifest");

        var pack = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        if ((int?)pack["schema_version"] is not { } schema || schema > KnownSchemaVersion)
            throw new FormatException("the pack has a schema_version this build does not know");

        AssistantProtocol? protocol = null;
        if (pack.SelectToken("assistant.protocol.content") is JObject content)
        {
            try { protocol = AssistantProtocol.FromJson(content); }
            catch (FormatException) { /* a pack without a usable protocol cannot generate, but it can search */ }
        }

        var sections = new Dictionary<string, PackSection>(StringComparer.Ordinal);
        foreach (var s in (JArray?)pack["sections"] ?? [])
        {
            var id = (string)s["id"]!;
            sections[id] = new PackSection(id, (string)s["title"]!, (string)s["text"]!);
        }

        return new HelpPack
        {
            Lang = lang,
            Sha256 = sha,
            ProjectName = (string)pack["project"]!["name"]!,
            ProjectVersion = (string)pack["project"]!["version"]!,
            Compatible = (bool?)pack.SelectToken("assistant.compatible") ?? false,
            ProtocolSha256 = (string?)pack.SelectToken("assistant.protocol.sha256"),
            Protocol = protocol,
            Sections = sections
        };
    }

    // ---------------------------------------------------------------- lock

    private static ModelLockEntry? LoadLock(HelpSettings settings, string contentRoot, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(settings.LockPath))
        {
            problems.Add("MyApp:Models:LockPath is not set.");
            return null;
        }

        var path = Resolve(contentRoot, settings.LockPath);
        if (!File.Exists(path))
        {
            problems.Add($"The lock file '{path}' does not exist.");
            return null;
        }

        try
        {
            var lockFile = JObject.Parse(File.ReadAllText(path));
            if ((int?)lockFile["schema_version"] is not { } schema || schema > KnownSchemaVersion)
            {
                problems.Add("The lock file has a schema_version this build does not know.");
                return null;
            }
            if (lockFile.SelectToken("models.assistant") is not JObject entry)
            {
                problems.Add("The lock file has no assistant entry.");
                return null;
            }

            return new ModelLockEntry(
                "assistant",
                (string)entry["model_version"]!,
                (string)entry.SelectToken("protocol.sha256")!,
                entry.SelectToken("base_model.chat_template_kwargs") as JObject);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException
                                      or NullReferenceException or InvalidCastException or ArgumentNullException)
        {
            problems.Add($"The lock file cannot be read ({e.GetType().Name}).");
            return null;
        }
    }

    // ---------------------------------------------------------------- checks (models.md 7.5)

    private static AssistantCompatibility Check(IReadOnlyDictionary<string, HelpPack> packs, ModelLockEntry? lockEntry,
        List<(string Reason, string Detail)> packProblems, List<string> lockProblems)
    {
        if (packs.Count == 0)
        {
            var first = packProblems.Count > 0 ? packProblems[0] : (Reason: IncompatibleReasons.NoPack, Detail: "No help pack is loaded.");
            return new AssistantCompatibility(false, first.Reason, string.Join(' ', packProblems.Select(p => p.Detail).DefaultIfEmpty(first.Detail)));
        }

        if (lockEntry is null)
            return new AssistantCompatibility(false, IncompatibleReasons.NoLock, string.Join(' ', lockProblems));

        foreach (var pack in packs.Values.OrderBy(p => p.Lang, StringComparer.Ordinal))
        {
            if (!pack.Compatible || pack.Protocol is null)
                return new AssistantCompatibility(false, IncompatibleReasons.PackNotCompatible,
                    $"The '{pack.Lang}' pack says the assistant is not compatible with it.");
            if (!string.Equals(pack.ProtocolSha256, lockEntry.ProtocolSha256, StringComparison.OrdinalIgnoreCase))
                return new AssistantCompatibility(false, IncompatibleReasons.ProtocolMismatch,
                    $"The protocol of the '{pack.Lang}' pack is not the protocol the model was trained with.");
        }

        return new AssistantCompatibility(true, null, null);
    }

    private static string Resolve(string contentRoot, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(contentRoot, path));
}
