using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MyApp.Monitor.Tools;

/// <summary>
/// The real <see cref="ISqlReadRunner"/>: SQL Server with <c>ApplicationIntent=ReadOnly</c> (constraint C02). The connection string
/// comes from the setting named by the source (<c>Monitor:Connections:{id}</c>); with <c>authentication: entra</c> the identity of
/// operations.md 6.3 is used. Not exercised by the tests: they use a SQLite fixture behind the same interface.
/// </summary>
public sealed class SqlServerReadRunner(IConfiguration configuration, MonitorOptions options) : ISqlReadRunner
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(SqlReadRequest request, CancellationToken ct)
    {
        SqlStatements.EnsureReadOnly(request.Text);
        var settingKey = request.Source.GetString("connection_setting") ?? $"Monitor:Connections:{request.Source.Id}";
        var connectionString = configuration[settingKey];
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ToolException($"the connection string setting '{settingKey}' is empty");

        var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
        if (request.Source.GetString("authentication") == "entra")
        {
            if (options.Azure.Credential == "managed_identity")
            {
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
                if (!string.IsNullOrWhiteSpace(options.Azure.ManagedIdentityClientId)) builder.UserID = options.Azure.ManagedIdentityClientId;
            }
            else builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
        }

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = request.Text;
        command.CommandTimeout = Math.Max(1, (int)request.Timeout.TotalSeconds);
        foreach (var p in request.Parameters) command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter(p.Name, p.Value));

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (rows.Count < request.MaxRows && await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
