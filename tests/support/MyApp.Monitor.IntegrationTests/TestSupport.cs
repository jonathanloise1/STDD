using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Tests;

public sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
    public void Set(DateTimeOffset value) => _now = value;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public static FakeTime Default() => new(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "monitor-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { /* best effort */ }
    }
}

/// <summary>A copy of the fixture repository that a test can change before building a pack.</summary>
public sealed class FixtureRepo : IDisposable
{
    private readonly TempDir _dir = new();

    public string Root => _dir.Path;

    public static string FixturesRoot => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures");
    public static string WorldRoot => System.IO.Path.Combine(FixturesRoot, "world");

    public FixtureRepo() => CopyDirectory(System.IO.Path.Combine(FixturesRoot, "repo"), Root);

    public string Read(string relative) => System.IO.File.ReadAllText(System.IO.Path.Combine(Root, relative));

    public void Write(string relative, string content)
    {
        var path = System.IO.Path.Combine(Root, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
    }

    public void Replace(string relative, string oldText, string newText)
    {
        var text = Read(relative);
        if (!text.Contains(oldText, StringComparison.Ordinal)) throw new InvalidOperationException($"'{oldText}' not found in {relative}");
        Write(relative, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    public void Delete(string relative) => System.IO.File.Delete(System.IO.Path.Combine(Root, relative));

    public const string Ops = "docs/areas/tasks/operations.md";
    public const string Unkeyed = "docs/support/unkeyed-runbooks.md";
    public const string Log = "src/backend/MyApp.Application/Logging/TasksLog.cs";

    public PackBuildResult Build(string profile = "onprem", string? outDir = null, FakeTime? time = null, bool zip = true) =>
        new PackBuilder(time ?? FakeTime.Default()).Build(new PackBuildRequest(Root, profile, "1.4.0+abc1234", outDir ?? System.IO.Path.Combine(Root, "out")) { WriteZip = zip });

    public OperationsPack BuildAndLoad(string profile = "onprem")
    {
        var result = Build(profile);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("\n", result.Findings));
        return PackLoader.Load(result.PackDirectory!);
    }

    public void Dispose() => _dir.Dispose();

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) System.IO.File.Copy(f, System.IO.Path.Combine(to, System.IO.Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyDirectory(d, System.IO.Path.Combine(to, System.IO.Path.GetFileName(d)));
    }
}

public static class Fx
{
    public const string HashKey = "0123456789abcdef0123456789abcdef-test-key";
    public static readonly byte[] HashKeyBytes = Encoding.UTF8.GetBytes(HashKey);

    public static string Support(string name) => System.IO.File.ReadAllText(System.IO.Path.Combine(FixtureRepo.FixturesRoot, "repo", "docs", "support", name));

    public static MaskingPolicy Policy() => MaskingPolicyLoader.Load(Support("masking-policy.json"));
    public static Masker Masker() => new(Policy(), HashKeyBytes);
    public static ToolCatalog Catalog() => ToolCatalog.Load(Support("tools.json"));
    public static EntityCatalog Entities() => EntityCatalog.Load(Support("entities.json"));
    public static ConstraintSet Constraints() => ConstraintSet.Load(Support("constraints.json"));

    public static LogRecord Record(
        string message = "something happened", RecordLevel level = RecordLevel.Information, string? eventName = null, string category = "MyApp.Test",
        DateTimeOffset? at = null, string? template = null, string? traceId = null, Dictionary<string, string>? props = null,
        string? exceptionType = null, string? topFrame = null, string? version = null, string env = "prod", string source = "src") =>
        LogRecord.Create(source, env, at ?? new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero), level, category, message, eventName, template,
            traceId, null, props, exceptionType, null, topFrame, version);

    public static Signature Sig(
        string id = "task.create.access_lost", string severity = "Warning", string status = "active", int expected = 5,
        string? fixedIn = null, NoiseInfo? noise = null, string[]? fingerprint = null, bool template = false) => new()
    {
        Id = id,
        Kind = template ? SignatureKinds.Template : SignatureKinds.Keyed,
        Match = template ? new SignatureMatch { TemplateId = id } : new SignatureMatch { LogKey = id },
        Severity = severity,
        Status = status,
        FixedIn = fixedIn,
        Noise = noise,
        Rate = new RateInfo { ExpectedPerHour = expected },
        FingerprintBy = (fingerprint ?? []).ToList(),
        CauseCategory = "access",
        Runbook = new RunbookRef { Anchor = id.Replace('.', '-').Replace('_', '-'), Path = "docs/areas/tasks/operations.md", Text = "### Meaning\n\nSomething expected.\n\n### Check\n\n1. Look." },
    };

    public static Analyser Analyser(
        IEnumerable<Signature> signatures, FakeTime? time = null, AnalyserSettings? settings = null, IIncidentIndex? index = null,
        ICounterStore? counters = null, IUnknownTemplateSink? unknowns = null)
    {
        time ??= FakeTime.Default();
        return new Analyser(settings ?? new AnalyserSettings(), new PackSignatureCatalog(signatures), index ?? new InMemoryIncidentIndex(time),
            counters ?? new InMemoryCounterStore(), unknowns ?? new InMemoryUnknownTemplateSink(), time);
    }

    public static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;
}

/// <summary>Test-only runner behind <see cref="ISqlReadRunner"/>: SQLite with the schema <c>dbo</c> attached, and the T-SQL <c>TOP (n)</c> rewritten.</summary>
public sealed class SqliteReadRunner : ISqlReadRunner, IDisposable
{
    private readonly SqliteConnection _connection;

    public List<string> Statements { get; } = [];
    public List<IReadOnlyList<SqlParameter>> Parameters { get; } = [];

    public SqliteReadRunner()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Exec("ATTACH DATABASE ':memory:' AS dbo");
        Exec("CREATE TABLE dbo.Organizations (Id TEXT, Name TEXT, IsDeleted INTEGER, BillingCountryCode TEXT, CreatedAt TEXT, CreatedBy TEXT, UpdatedAt TEXT, ModifiedBy TEXT, InternalNote TEXT)");
        Exec("CREATE TABLE dbo.AuditLogs (Id TEXT, EntityType TEXT, EntityId TEXT, Timestamp TEXT, Action INTEGER, UserId TEXT, ChangedProperties TEXT, OldValues TEXT, NewValues TEXT, CorrelationId TEXT)");
        Exec("INSERT INTO dbo.Organizations VALUES ('0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91','Acme Srl',1,'IT','2026-01-01T00:00:00Z','6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72','2026-11-06T09:10:02Z','1e8b4c7d-55a0-4a39-8c1f-3d2b7e9a0c14','must not leave')");
        Exec("INSERT INTO dbo.AuditLogs VALUES ('1','Organization','0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91','2026-11-06T09:00:00Z',0,'1e8b4c7d-55a0-4a39-8c1f-3d2b7e9a0c14',NULL,NULL,'{\"Name\":\"Acme Srl\"}','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')");
        Exec("INSERT INTO dbo.AuditLogs VALUES ('2','Organization','0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91','2026-11-06T09:10:02Z',1,'1e8b4c7d-55a0-4a39-8c1f-3d2b7e9a0c14','[\"IsDeleted\"]','{\"IsDeleted\":false,\"InternalNote\":\"x\"}','{\"IsDeleted\":true,\"InternalNote\":\"y\"}','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb')");
    }

    private void Exec(string sql)
    {
        using var c = _connection.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(SqlReadRequest request, CancellationToken ct)
    {
        SqlStatements.EnsureReadOnly(request.Text);
        Statements.Add(request.Text);
        Parameters.Add(request.Parameters);
        var sql = System.Text.RegularExpressions.Regex.Replace(request.Text, @"SELECT TOP \((\d+)\) (.*)$", "SELECT $2 LIMIT $1");
        using var c = _connection.CreateCommand();
        c.CommandText = sql;
        foreach (var p in request.Parameters) c.Parameters.AddWithValue(p.Name, p.Value is Guid g ? g.ToString("D") : p.Value);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        using var r = c.ExecuteReader();
        while (r.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rows);
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class FakeLogAnalyticsClient : ILogAnalyticsClient
{
    public List<(string Workspace, string Query)> Calls { get; } = [];
    public Queue<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Responses { get; } = new();
    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string workspace, string query, CancellationToken ct)
    {
        Calls.Add((workspace, query));
        if (Failure is not null) throw Failure;
        return Task.FromResult(Responses.Count > 0 ? Responses.Dequeue() : []);
    }
}

public sealed class FakeDiscoveryClient(params string[] found) : IWorkspaceDiscoveryClient
{
    public int Calls { get; private set; }
    public List<string> Found { get; } = found.ToList();
    public (string Tag, string Value, string Type, IReadOnlyList<string> Subs)? Last { get; private set; }

    public Task<IReadOnlyList<string>> FindWorkspacesAsync(string tagName, string tagValue, string resourceType, IReadOnlyList<string> subscriptionIds, CancellationToken ct)
    {
        Calls++;
        Last = (tagName, tagValue, resourceType, subscriptionIds);
        return Task.FromResult<IReadOnlyList<string>>(Found.ToList());
    }
}

public sealed class FakeWorkspaceResolver(string workspace = "ws-1") : IWorkspaceResolver
{
    public Task<string> ResolveAsync(string sourceId, JsonObject sourceSettings, CancellationToken ct) => Task.FromResult(workspace);
}

public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}

public sealed class FakeHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);

    public static FakeHttpFactory Json(HttpStatusCode status, string body) =>
        new(new FakeHttpHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
}

public sealed class RecordingAuditSink : IToolAuditSink
{
    public List<ToolAuditRow> Rows { get; } = [];
    public Task WriteAsync(ToolAuditRow row, CancellationToken ct) { Rows.Add(row); return Task.CompletedTask; }
}

/// <summary>An implementation that behaves as told, for executor rule tests.</summary>
public sealed class ScriptedImplementation(string family, string kind, Func<ToolContext, CancellationToken, Task<ToolResult>> run) : IToolImplementation
{
    public string Family => family;
    public string SourceKind => kind;
    public List<ToolContext> Calls { get; } = [];

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        Calls.Add(context);
        return run(context, ct);
    }
}

/// <summary>The repository the tests run from: the real golden file and schemas, not the fixture copy.</summary>
public static class Repo
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "docs", "guidelines", "operations.md"))) return dir.FullName;
        throw new InvalidOperationException("the tests must run inside the repository");
    }
}
