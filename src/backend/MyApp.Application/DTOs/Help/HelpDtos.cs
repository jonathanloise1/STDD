using Newtonsoft.Json;

namespace MyApp.Application.DTOs.Help;

// Property names are the snake_case names of models.md 7.4 and manual.md 7.4; they are explicit, so they do not
// depend on the naming policy of the serializer.

/// <summary>Body of <c>POST /api/help/answer</c> (models.md 7.4).</summary>
public class HelpAnswerRequest
{
    [JsonProperty("lang")] public string? Lang { get; set; }
    [JsonProperty("question")] public string? Question { get; set; }
    [JsonProperty("section_ids")] public List<string>? SectionIds { get; set; }
    [JsonProperty("pack_sha256")] public string? PackSha256 { get; set; }
}

/// <summary>The pack a gap was recorded against (manual.md 7.4).</summary>
public class HelpGapPackDto
{
    [JsonProperty("project_version")] public string? ProjectVersion { get; set; }
    [JsonProperty("sha256")] public string? Sha256 { get; set; }
}

/// <summary>Body of <c>POST /api/help/gaps</c> and one entry of <c>GET /api/help/gaps</c> (manual.md 7.4).</summary>
public class HelpGapDto
{
    [JsonProperty("schema_version")] public int SchemaVersion { get; set; } = 1;
    [JsonProperty("recorded_at")] public DateTime? RecordedAt { get; set; }
    [JsonProperty("lang")] public string? Lang { get; set; }
    [JsonProperty("route")] public string? Route { get; set; }
    [JsonProperty("question")] public string? Question { get; set; }
    [JsonProperty("outcome")] public string? Outcome { get; set; }
    [JsonProperty("tier")] public string? Tier { get; set; }
    [JsonProperty("pack")] public HelpGapPackDto? Pack { get; set; }
    [JsonProperty("top_sections")] public List<string>? TopSections { get; set; }
    [JsonProperty("coverage")] public double? Coverage { get; set; }
}

/// <summary>The browser tier values of the status.</summary>
public class HelpBrowserStatusDto
{
    [JsonProperty("enabled")] public bool Enabled { get; set; }
    [JsonProperty("model_base_url")] public string ModelBaseUrl { get; set; } = string.Empty;
    [JsonProperty("min_tokens_per_second")] public int MinTokensPerSecond { get; set; }
    [JsonProperty("ask_before_download_mb")] public int AskBeforeDownloadMb { get; set; }
    [JsonProperty("timeout_seconds")] public int TimeoutSeconds { get; set; }
}

/// <summary>The server tier values of the status.</summary>
public class HelpServerStatusDto
{
    [JsonProperty("enabled")] public bool Enabled { get; set; }
    [JsonProperty("available")] public bool Available { get; set; }

    /// <summary><c>gpu</c> or <c>cpu</c>; null while the host is not available.</summary>
    [JsonProperty("device")] public string? Device { get; set; }

    [JsonProperty("timeout_seconds")] public int TimeoutSeconds { get; set; }

    /// <summary><c>host_unreachable</c> or <c>model_alias_mismatch</c> when enabled but not available.</summary>
    [JsonProperty("reason")] public string? Reason { get; set; }
}

/// <summary>The model recorded in the lock.</summary>
public class HelpModelStatusDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
}

/// <summary>The loaded pack: version and the SHA-256 the panel sends with an answer request.</summary>
public class HelpPackStatusDto
{
    [JsonProperty("loaded")] public bool Loaded { get; set; }
    [JsonProperty("project_version")] public string? ProjectVersion { get; set; }
    [JsonProperty("sha256")] public Dictionary<string, string> Sha256 { get; set; } = new();
}

/// <summary>Response of <c>GET /api/help/status</c> (models.md 7.4, 7.7).</summary>
public class HelpStatusDto
{
    /// <summary>The order to use: <c>retrieval-only</c> whenever generation is not allowed.</summary>
    [JsonProperty("order")] public string Order { get; set; } = string.Empty;

    [JsonProperty("configured_order")] public string ConfiguredOrder { get; set; } = string.Empty;
    [JsonProperty("compatible")] public bool Compatible { get; set; }

    /// <summary>Why generation is off (models.md 7.5): <c>no_pack</c>, <c>pack_unreadable</c>, <c>no_lock</c>, <c>pack_not_compatible</c> or <c>protocol_mismatch</c>.</summary>
    [JsonProperty("reason")] public string? Reason { get; set; }

    [JsonProperty("pack")] public HelpPackStatusDto Pack { get; set; } = new();
    [JsonProperty("model")] public HelpModelStatusDto? Model { get; set; }
    [JsonProperty("browser")] public HelpBrowserStatusDto Browser { get; set; } = new();
    [JsonProperty("server")] public HelpServerStatusDto Server { get; set; } = new();
    [JsonProperty("gaps_enabled")] public bool GapsEnabled { get; set; }
}
