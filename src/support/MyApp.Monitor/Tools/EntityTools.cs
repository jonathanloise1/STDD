using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Tools;

internal static class EntitySupport
{
    public static EntityDefinition Entity(ToolServices s, ToolContext ctx)
    {
        var name = ToolValues.Required(ctx.Arguments, "entity");
        var def = s.Entities.Find(name) ?? throw new ToolArgumentException($"unknown entity '{name}'");
        if (def.SourceKind != ctx.Source.Kind && !(ctx.Source.Kind == "world"))
            throw new ToolArgumentException($"entity '{name}' is bound to source kind '{def.SourceKind}', not '{ctx.Source.Kind}'");
        return def;
    }

    /// <summary>The columns an entity read selects: the keys of the entity's masking section, nothing else (operations.md 7.2).</summary>
    public static List<string> Columns(ToolServices s, string entity)
    {
        if (!s.Masking.Entities.TryGetValue(entity, out var section) || section.Count == 0)
            throw new ToolException($"the masking policy has no section for entity '{entity}'");
        var columns = section.Keys.ToList();
        foreach (var c in columns) SqlStatements.Quote(c);   // identifier check
        return columns;
    }

    public static object TypedKey(EntityDefinition def, string id)
    {
        switch (def.KeyType)
        {
            case "guid":
                if (!Guid.TryParse(id, out var g)) throw new ToolArgumentException("id must be a GUID for this entity");
                return g;
            case "int":
                if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) throw new ToolArgumentException("id must be an integer for this entity");
                return l;
            default:
                return id;
        }
    }

    public static JsonObject Project(JsonObject row, IEnumerable<string> columns)
    {
        var result = new JsonObject();
        foreach (var column in columns)
            foreach (var kv in row)
                if (string.Equals(kv.Key, column, StringComparison.OrdinalIgnoreCase)) { result[column] = kv.Value?.DeepClone(); break; }
        return result;
    }

    public static ToolResult EntityResult(EntityDefinition def, string id, JsonObject? fields) =>
        new(new JsonObject { ["entity"] = def.Name, ["id"] = id, ["found"] = fields is not null, ["fields"] = fields }, fields is null ? 0 : 1);

    // ---- audit mapping ----

    private static readonly string[] ActionNames = ["Create", "Update", "Delete"];
    private static readonly Regex OrderBy = new(@"^[A-Za-z_][A-Za-z0-9_]*(\s+(ASC|DESC))?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string CheckedOrderBy(string? text)
    {
        var t = (text ?? "").Trim();
        if (!OrderBy.IsMatch(t)) throw new ToolException("order_by must be one column with an optional ASC or DESC");
        var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return SqlStatements.Quote(parts[0]) + (parts.Length > 1 ? " " + parts[1].ToUpperInvariant() : "");
    }

    /// <summary>Maps a row with the audit log's column names to the tool's <c>changes[]</c> item.</summary>
    public static JsonObject AuditChange(JsonObject row, JsonObject columns)
    {
        var change = JsonColumns.Map(row, columns);
        if (change["action"] is JsonValue a && int.TryParse(MonitorJson.ScalarText(a), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 0 and < 3)
            change["action"] = ActionNames[n];
        change["changed_properties"] = ChangedProperties(ToolValues.ParseJsonText(change["changed_properties"]));
        change["old_values"] = ToolValues.ParseJsonText(change["old_values"]);
        change["new_values"] = ToolValues.ParseJsonText(change["new_values"]);
        return change;
    }

    private static JsonNode? ChangedProperties(JsonNode? node) => node switch
    {
        JsonArray a => new JsonArray(a.Select(x => (JsonNode?)JsonValue.Create(MonitorJson.ScalarText(x) ?? "")).ToArray()),
        JsonObject o => new JsonArray(o.Select(kv => (JsonNode?)JsonValue.Create(kv.Key)).ToArray()),
        JsonValue v when MonitorJson.ScalarText(v) is { Length: > 0 } s =>
            new JsonArray(s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        _ => new JsonArray(),
    };

    public static readonly JsonObject SqlAuditColumns = new()
    {
        ["timestamp"] = "Timestamp", ["action"] = "Action", ["user_id"] = "UserId", ["changed_properties"] = "ChangedProperties",
        ["old_values"] = "OldValues", ["new_values"] = "NewValues", ["correlation_id"] = "CorrelationId",
    };
}

/// <summary><c>sql_entity_read</c> on SQL Server: <c>SELECT TOP (1) {allowed columns} FROM {table} WHERE {key} = @id</c>.</summary>
public sealed class SqlEntityReadSqlServer(ToolServices services) : IToolImplementation
{
    public string Family => "entity";
    public string SourceKind => "sqlserver";

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var runner = services.Sql ?? throw new ToolException("no SQL runner is configured");
        var def = EntitySupport.Entity(services, ctx);
        var id = ToolValues.Required(ctx.Arguments, "id");
        var typed = EntitySupport.TypedKey(def, id);
        var columns = EntitySupport.Columns(services, def.Name);

        var template = MonitorJson.ScalarText(ctx.Settings["statement"]) ?? "SELECT TOP (1) {columns} FROM {table} WHERE {key} = @id";
        var statement = template
            .Replace("{columns}", string.Join(", ", columns.Select(SqlStatements.Quote)), StringComparison.Ordinal)
            .Replace("{table}", SqlStatements.QuoteTable(def.Table), StringComparison.Ordinal)
            .Replace("{key}", SqlStatements.Quote(def.Key), StringComparison.Ordinal);
        SqlStatements.EnsureReadOnly(statement);

        var rows = await runner.QueryAsync(new SqlReadRequest(ctx.Source, statement, [new SqlParameter("@id", typed)], 1, TimeSpan.FromSeconds(ctx.Limits.TimeoutSeconds)), ct).ConfigureAwait(false);
        var fields = rows.Count == 0 ? null : EntitySupport.Project(ToolValues.Row(rows[0]), columns);
        return EntitySupport.EntityResult(def, id, fields);
    }
}

/// <summary><c>sql_entity_read</c> on a world directory: <c>entities/{entity}.jsonl</c>.</summary>
public sealed class SqlEntityReadWorld(ToolServices services) : IToolImplementation
{
    public string Family => "entity";
    public string SourceKind => "world";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var def = EntitySupport.Entity(services, ctx);
        var id = ToolValues.Required(ctx.Arguments, "id");
        EntitySupport.TypedKey(def, id);
        var columns = EntitySupport.Columns(services, def.Name);
        var directory = ctx.Source.GetString("directory") ?? throw new ToolException("the world source has no directory");
        var pattern = MonitorJson.ScalarText(ctx.Settings["file_pattern"]) ?? "{entity}.jsonl";
        var folder = MonitorJson.ScalarText(ctx.Settings["directory"]) ?? "entities";
        var file = Path.Combine(directory, folder, pattern.Replace("{entity}", def.Name, StringComparison.Ordinal));
        if (!File.Exists(file)) return Task.FromResult(EntitySupport.EntityResult(def, id, null));

        foreach (var line in File.ReadLines(file))
        {
            if (line.Length == 0) continue;
            JsonObject? obj;
            try { obj = JsonNode.Parse(line) as JsonObject; } catch (JsonException) { continue; }
            if (obj is null) continue;
            var keyValue = MonitorJson.ScalarText(MonitorJson.Get(obj, def.Key));
            if (string.Equals(keyValue, id, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(EntitySupport.EntityResult(def, id, EntitySupport.Project(obj, columns)));
        }
        return Task.FromResult(EntitySupport.EntityResult(def, id, null));
    }
}

/// <summary><c>audit_trail_by_entity</c> on SQL Server: the audit table filtered by entity type and id, newest first.</summary>
public sealed class AuditTrailSqlServer(ToolServices services) : IToolImplementation
{
    public string Family => "audit";
    public string SourceKind => "sqlserver";

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var runner = services.Sql ?? throw new ToolException("no SQL runner is configured");
        var def = EntitySupport.Entity(services, ctx);
        if (def.AuditEntityType is null) throw new ToolArgumentException($"entity '{def.Name}' has no audit trail");
        var id = ToolValues.Required(ctx.Arguments, "id");
        EntitySupport.TypedKey(def, id);
        var limit = ToolValues.Limit(ctx.Arguments, "limit", 20, Math.Min(50, ctx.Limits.MaxRows));

        var table = MonitorJson.ScalarText(ctx.Settings["table"]) ?? throw new ToolException("the catalog has no audit table");
        var filter = ctx.Settings["filter"] as JsonObject ?? throw new ToolException("the catalog has no audit filter");
        var typeColumn = MonitorJson.ScalarText(filter["entity_type_column"]) ?? throw new ToolException("entity_type_column is missing");
        var idColumn = MonitorJson.ScalarText(filter["entity_id_column"]) ?? throw new ToolException("entity_id_column is missing");
        var columns = ctx.Settings["columns"] as JsonObject ?? EntitySupport.SqlAuditColumns;
        var selected = columns.Select(kv => MonitorJson.ScalarText(kv.Value) ?? "").Where(c => c.Length > 0).Distinct().ToList();

        var statement =
            $"SELECT TOP ({limit.ToString(CultureInfo.InvariantCulture)}) {string.Join(", ", selected.Select(SqlStatements.Quote))} " +
            $"FROM {SqlStatements.QuoteTable(table)} WHERE {SqlStatements.Quote(typeColumn)} = @type AND {SqlStatements.Quote(idColumn)} = @id " +
            $"ORDER BY {EntitySupport.CheckedOrderBy(MonitorJson.ScalarText(ctx.Settings["order_by"]) ?? "Timestamp DESC")}";
        SqlStatements.EnsureReadOnly(statement);

        var rows = await runner.QueryAsync(
            new SqlReadRequest(ctx.Source, statement, [new SqlParameter("@type", def.AuditEntityType), new SqlParameter("@id", id)], limit, TimeSpan.FromSeconds(ctx.Limits.TimeoutSeconds)),
            ct).ConfigureAwait(false);
        var changes = rows.Take(limit).Select(r => (JsonNode?)EntitySupport.AuditChange(ToolValues.Row(r), columns)).ToArray();
        return new ToolResult(new JsonObject { ["changes"] = new JsonArray(changes) }, changes.Length);
    }
}

/// <summary><c>audit_trail_by_entity</c> on a world directory: <c>audit.jsonl</c> with the audit table's column names.</summary>
public sealed class AuditTrailWorld(ToolServices services) : IToolImplementation
{
    public string Family => "audit";
    public string SourceKind => "world";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var def = EntitySupport.Entity(services, ctx);
        if (def.AuditEntityType is null) throw new ToolArgumentException($"entity '{def.Name}' has no audit trail");
        var id = ToolValues.Required(ctx.Arguments, "id");
        var limit = ToolValues.Limit(ctx.Arguments, "limit", 20, Math.Min(50, ctx.Limits.MaxRows));
        var directory = ctx.Source.GetString("directory") ?? throw new ToolException("the world source has no directory");
        var file = Path.Combine(directory, MonitorJson.ScalarText(ctx.Settings["file"]) ?? "audit.jsonl");
        var found = new List<(DateTimeOffset At, JsonObject Row)>();
        if (File.Exists(file))
            foreach (var line in File.ReadLines(file))
            {
                if (line.Length == 0) continue;
                JsonObject? obj;
                try { obj = JsonNode.Parse(line) as JsonObject; } catch (JsonException) { continue; }
                if (obj is null) continue;
                if (MonitorJson.ScalarText(MonitorJson.Get(obj, "EntityType")) != def.AuditEntityType) continue;
                if (!string.Equals(MonitorJson.ScalarText(MonitorJson.Get(obj, "EntityId")), id, StringComparison.OrdinalIgnoreCase)) continue;
                var at = DateTimeOffset.TryParse(MonitorJson.ScalarText(MonitorJson.Get(obj, "Timestamp")), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : DateTimeOffset.MinValue;
                found.Add((at, obj));
            }
        var changes = found.OrderByDescending(f => f.At).Take(limit)
            .Select(f => (JsonNode?)EntitySupport.AuditChange(f.Row, EntitySupport.SqlAuditColumns)).ToArray();
        return Task.FromResult(new ToolResult(new JsonObject { ["changes"] = new JsonArray(changes) }, changes.Length));
    }
}
