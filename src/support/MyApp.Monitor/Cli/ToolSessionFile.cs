using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Cli;

/// <summary>
/// The state of an incident's tool calls between <c>tool --incident</c> invocations: every call is a new process, so evidence numbering, duplicates and the
/// per-incident budgets (operations.md 7.3) continue from a small file, one per incident, in the temporary folder (or <c>--state</c>). The file holds counters and the
/// canonical keys of earlier calls, never results.
/// </summary>
public sealed class ToolSessionFile(string directory)
{
    /// <summary>A state file not written for this long is ignored: an incident is investigated within minutes (C03: at most 10), and ids are reused by nobody.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);

    public static string DefaultDirectory => Path.Combine(Path.GetTempPath(), "myapp-monitor-tool-sessions");

    public string PathFor(string incidentId) =>
        Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(incidentId))).ToLowerInvariant()[..32] + ".json");

    /// <summary>The session of an incident: continued when a state file exists, new otherwise.</summary>
    public IncidentToolSession Open(string incidentId)
    {
        var session = new IncidentToolSession(incidentId);
        var path = PathFor(incidentId);
        if (!File.Exists(path)) return session;
        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > MaxAge) return session;   // an old investigation of the same id is another investigation
        try { if (MonitorJson.Parse(File.ReadAllText(path)) is JsonObject state) session.Restore(state); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { /* an unreadable state starts the incident again: calls are never refused for it */ }
        return session;
    }

    public void Save(IncidentToolSession session)
    {
        if (session.IncidentId is null) return;
        Directory.CreateDirectory(directory);
        var path = PathFor(session.IncidentId);
        var temp = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temp, session.Snapshot().ToJsonString(MonitorJson.Compact), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
