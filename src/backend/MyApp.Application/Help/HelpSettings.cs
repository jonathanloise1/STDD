using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace MyApp.Application.Help;

/// <summary>The help configuration keys of models.md 7.7, read once with their documented defaults.</summary>
public sealed class HelpSettings
{
    public const string OrderBrowserFirst = "browser-first";
    public const string OrderServerFirst = "server-first";
    public const string OrderRetrievalOnly = "retrieval-only";

    /// <summary><c>MyApp:Help:PackDirectory</c>: folder of manifest.json and the packs; empty means none.</summary>
    public string PackDirectory { get; init; } = string.Empty;

    /// <summary><c>MyApp:Help:Gaps:Enabled</c></summary>
    public bool GapsEnabled { get; init; } = true;

    /// <summary><c>MyApp:Help:Gaps:RetentionDays</c></summary>
    public int GapsRetentionDays { get; init; } = 90;

    /// <summary><c>MyApp:Models:LockPath</c>: the lock file read at start-up; empty means none.</summary>
    public string LockPath { get; init; } = string.Empty;

    /// <summary><c>MyApp:Assistant:Order</c></summary>
    public string Order { get; init; } = OrderBrowserFirst;

    public bool BrowserEnabled { get; init; } = true;
    public string BrowserModelBaseUrl { get; init; } = "/models/assistant/";
    public int BrowserMinTokensPerSecond { get; init; } = 8;
    public int BrowserAskBeforeDownloadMb { get; init; } = 100;
    public int BrowserTimeoutSeconds { get; init; } = 20;

    public bool ServerEnabled { get; init; }
    public string ServerEndpoint { get; init; } = "http://127.0.0.1:8091";
    public int ServerTimeoutSeconds { get; init; } = 20;
    public int ServerMaxConcurrent { get; init; } = 2;
    public int ServerQueueTimeoutSeconds { get; init; } = 5;

    /// <summary>Reads the keys; a missing, empty or malformed value gives the default.</summary>
    public static HelpSettings Load(IConfiguration configuration)
    {
        var d = new HelpSettings();
        var order = configuration["MyApp:Assistant:Order"]?.Trim().ToLowerInvariant();
        return new HelpSettings
        {
            PackDirectory = Text(configuration, "MyApp:Help:PackDirectory", d.PackDirectory),
            GapsEnabled = Flag(configuration, "MyApp:Help:Gaps:Enabled", d.GapsEnabled),
            GapsRetentionDays = Number(configuration, "MyApp:Help:Gaps:RetentionDays", d.GapsRetentionDays, 1),
            LockPath = Text(configuration, "MyApp:Models:LockPath", d.LockPath),
            Order = order is OrderBrowserFirst or OrderServerFirst or OrderRetrievalOnly ? order : d.Order,
            BrowserEnabled = Flag(configuration, "MyApp:Assistant:Browser:Enabled", d.BrowserEnabled),
            BrowserModelBaseUrl = Text(configuration, "MyApp:Assistant:Browser:ModelBaseUrl", d.BrowserModelBaseUrl),
            BrowserMinTokensPerSecond = Number(configuration, "MyApp:Assistant:Browser:MinTokensPerSecond", d.BrowserMinTokensPerSecond, 1),
            BrowserAskBeforeDownloadMb = Number(configuration, "MyApp:Assistant:Browser:AskBeforeDownloadMb", d.BrowserAskBeforeDownloadMb, 0),
            BrowserTimeoutSeconds = Number(configuration, "MyApp:Assistant:Browser:TimeoutSeconds", d.BrowserTimeoutSeconds, 1),
            ServerEnabled = Flag(configuration, "MyApp:Assistant:Server:Enabled", d.ServerEnabled),
            ServerEndpoint = Text(configuration, "MyApp:Assistant:Server:Endpoint", d.ServerEndpoint),
            ServerTimeoutSeconds = Number(configuration, "MyApp:Assistant:Server:TimeoutSeconds", d.ServerTimeoutSeconds, 1),
            ServerMaxConcurrent = Number(configuration, "MyApp:Assistant:Server:MaxConcurrent", d.ServerMaxConcurrent, 1),
            ServerQueueTimeoutSeconds = Number(configuration, "MyApp:Assistant:Server:QueueTimeoutSeconds", d.ServerQueueTimeoutSeconds, 0)
        };
    }

    private static string Text(IConfiguration c, string key, string fallback) =>
        c[key] is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

    private static bool Flag(IConfiguration c, string key, bool fallback) =>
        bool.TryParse(c[key], out var v) ? v : fallback;

    private static int Number(IConfiguration c, string key, int fallback, int min) =>
        int.TryParse(c[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= min ? v : fallback;
}
