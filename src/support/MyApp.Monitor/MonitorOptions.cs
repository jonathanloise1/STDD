using System.Text;
using Microsoft.Extensions.Configuration;

namespace MyApp.Monitor;

/// <summary>Monitor configuration (operations.md 14.2), bound from the <c>Monitor</c> section.</summary>
public sealed class MonitorOptions
{
    public const string SectionName = "Monitor";

    public PackOptions Pack { get; set; } = new();
    public string? Profile { get; set; }
    public string? Environment { get; set; }
    public AzureOptions Azure { get; set; } = new();
    public Dictionary<string, string> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public MaskingOptions Masking { get; set; } = new();
    public InvestigatorOptions Investigator { get; set; } = new();
    public DashboardOptions Dashboard { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Base for relative file paths of profiles. Default: the current directory.</summary>
    public string? ContentRoot { get; set; }

    /// <summary>Repository root for <c>pack build</c>. Default: found by walking up from the current directory.</summary>
    public string? RepositoryRoot { get; set; }

    public sealed class PackOptions { public string? Path { get; set; } }

    public sealed class AzureOptions
    {
        public string Credential { get; set; } = "developer";
        public string? ManagedIdentityClientId { get; set; }
        public string? SubscriptionIds { get; set; }

        public IReadOnlyList<string> SubscriptionIdList =>
            (SubscriptionIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public sealed class MaskingOptions { public string? HashKey { get; set; } }
    public sealed class InvestigatorOptions
{
    /// <summary>Path to <c>models/model.lock</c>. Unset: <c>models/model.lock</c> of the repository found from the content root, if any.</summary>
    public string? ModelLock { get; set; }

    /// <summary>Overrides the profile's <c>investigator.device</c>: <c>auto</c>, <c>gpu</c> or <c>cpu</c>.</summary>
    public string? Device { get; set; }

    /// <summary>The model host's address (<c>src/support/model-host</c>; port 8092 by convention, 8091 is the assistant's).</summary>
    public string Endpoint { get; set; } = "http://127.0.0.1:8092";

    /// <summary>One host per tier when both run: tier (<c>small</c>, <c>large</c>) to address. A tier without an entry uses <see cref="Endpoint"/>.</summary>
    public Dictionary<string, string> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How often a host that is missing, or present, is checked again.</summary>
    public int ProbeSeconds { get; set; } = 30;
}
    public sealed class DashboardOptions { public string? Urls { get; set; } }

    public static MonitorOptions From(IConfiguration configuration)
    {
        var options = new MonitorOptions();
        configuration.GetSection(SectionName).Bind(options);
        return options;
    }

    /// <summary>The masking key is required by every command that masks (operations.md 8.2).</summary>
    public byte[] RequireHashKey()
    {
        var key = Masking.HashKey;
        if (string.IsNullOrEmpty(key))
            throw new MonitorException("Monitor:Masking:HashKey is missing: the monitor refuses to start without a masking key.");
        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
            throw new MonitorException("Monitor:Masking:HashKey must be at least 32 bytes.");
        return bytes;
    }

    public string ResolveContentRoot() => Path.GetFullPath(ContentRoot ?? Directory.GetCurrentDirectory());

    public void ValidateAzure()
    {
        if (Azure.Credential is not ("developer" or "managed_identity"))
            throw new MonitorException("Monitor:Azure:Credential must be 'developer' or 'managed_identity'.");
    }
}
