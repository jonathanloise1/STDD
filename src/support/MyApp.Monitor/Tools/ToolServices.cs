using System.Text.Json.Nodes;

namespace MyApp.Monitor.Tools;

public sealed record SqlParameter(string Name, object Value);

public sealed record SqlReadRequest(SourceBinding Source, string Text, IReadOnlyList<SqlParameter> Parameters, int MaxRows, TimeSpan Timeout);

/// <summary>Runs a statement built by the executor's tools on a read-only connection (operations.md 7.3, C02).</summary>
public interface ISqlReadRunner
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(SqlReadRequest request, CancellationToken ct);
}

/// <summary>Builds the one statement shape the executor allows: <c>SELECT TOP (n) ... WHERE ... ORDER BY ...</c> (C02).</summary>
public static class SqlStatements
{
    public static string Quote(string identifier)
    {
        if (!EntityCatalog.Identifier.IsMatch(identifier)) throw new ToolException($"'{identifier}' is not a plain identifier");
        return "[" + identifier + "]";
    }

    public static string QuoteTable(string schemaTable)
    {
        var parts = schemaTable.Split('.');
        if (parts.Length != 2) throw new ToolException($"table '{schemaTable}' must be schema.table");
        return Quote(parts[0]) + "." + Quote(parts[1]);
    }

    /// <summary>Throws when a statement is not a bounded SELECT.</summary>
    public static void EnsureReadOnly(string text)
    {
        var t = text.TrimStart();
        if (!t.StartsWith("SELECT TOP (", StringComparison.Ordinal) || t.Contains(';') || t.Contains("--", StringComparison.Ordinal))
            throw new ToolException("only bounded SELECT statements built from the catalog are allowed");
    }
}

/// <summary>The log query client behind the cloud source and <c>logs_by_correlation</c>. A fake implements it in tests.</summary>
public interface ILogAnalyticsClient
{
    /// <summary>Runs a query on a workspace (id or resource id). Each row maps column name to value; dynamic columns arrive as JSON text or objects.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string workspace, string query, CancellationToken ct);
}

/// <summary>Finds the workspace resources that carry the profile tag (operations.md 6.3 Discovery).</summary>
public interface IWorkspaceDiscoveryClient
{
    Task<IReadOnlyList<string>> FindWorkspacesAsync(
        string tagName, string tagValue, string resourceType, IReadOnlyList<string> subscriptionIds, CancellationToken ct);
}
