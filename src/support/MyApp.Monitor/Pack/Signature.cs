using System.Text.Json.Nodes;

namespace MyApp.Monitor.Pack;

/// <summary>Signature record (operations.md 5.3, docs/support/signature.schema.json). Serialised with snake_case names.</summary>
public sealed class Signature
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public SignatureMatch Match { get; set; } = new();
    public string? Area { get; set; }
    public string? UserStory { get; set; }
    public string? TestCase { get; set; }
    public string Severity { get; set; } = "";
    public string? Message { get; set; }
    public CallSite? CallSite { get; set; }
    public RunbookRef Runbook { get; set; } = new();
    public RateInfo Rate { get; set; } = new();
    public List<string> FingerprintBy { get; set; } = [];
    public string CauseCategory { get; set; } = "";
    public string Status { get; set; } = "";
    public string? FixedIn { get; set; }
    public NoiseInfo? Noise { get; set; }
    public List<RuleCheck> RuleChecks { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsKeyed => Kind == SignatureKinds.Keyed;
}

public static class SignatureKinds
{
    public const string Keyed = "keyed";
    public const string Template = "template";
}

public static class SignatureStatus
{
    public const string Active = "active";
    public const string Noise = "noise";
    public const string Fixed = "fixed";
}

public sealed class SignatureMatch
{
    public string? LogKey { get; set; }
    public string? TemplateId { get; set; }
    public string? EventName { get; set; }
    public string? Category { get; set; }
    public string? ExceptionType { get; set; }
    public string? TopFrame { get; set; }
    public string? Template { get; set; }
}

public sealed class CallSite
{
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string Method { get; set; } = "";
    public string Template { get; set; } = "";
    public List<string> Parameters { get; set; } = [];
}

public sealed class RunbookRef
{
    public string Anchor { get; set; } = "";
    public string Path { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class RateInfo { public int ExpectedPerHour { get; set; } }

public sealed class NoiseInfo
{
    public string Rationale { get; set; } = "";
    public string Expires { get; set; } = "";
}

public sealed class RuleCheck
{
    public string Tool { get; set; } = "";
    public JsonObject Arguments { get; set; } = new();
    public string Expect { get; set; } = "";
}

public sealed class HistoryEntry
{
    public string Date { get; set; } = "";
    public string Incident { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string? Reference { get; set; }
}

public sealed class SignatureTable
{
    public int SchemaVersion { get; set; } = 1;
    public List<Signature> Signatures { get; set; } = [];
}
