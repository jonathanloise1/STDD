using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace MyApp.Monitor.Store;

/// <summary>
/// The SQLite file of the monitor (operations.md 10.2): created, put in WAL mode and migrated at start by the numbered
/// scripts in <c>Store/Migrations/</c>, which ship as embedded resources. <c>schema_info</c> holds one row per applied script.
/// </summary>
public sealed class SqliteDatabase
{
    public const int CurrentVersion = 1;

    private SqliteDatabase(string path)
    {
        Path = path;
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = true }.ToString();
    }

    public string Path { get; }
    public string ConnectionString { get; }
    public int SchemaVersion { get; private set; }

    /// <summary>Opens the file (creating the folder), enables WAL and applies the migrations that are not applied yet.</summary>
    public static SqliteDatabase Open(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var directory = System.IO.Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var db = new SqliteDatabase(full);
        using var connection = db.Connect();
        Execute(connection, "PRAGMA journal_mode=WAL");
        db.SchemaVersion = Migrate(connection);
        return db;
    }

    public SqliteConnection Connect()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        Execute(connection, "PRAGMA busy_timeout=10000; PRAGMA synchronous=NORMAL;");
        return connection;
    }

    /// <summary>Releases the file (and lets the WAL be checkpointed) when the monitor stops.</summary>
    public void Close() => SqliteConnection.ClearAllPools();

    private static int Migrate(SqliteConnection connection)
    {
        Execute(connection, "CREATE TABLE IF NOT EXISTS schema_info (version INTEGER PRIMARY KEY)");
        var applied = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_info";
            applied = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var scripts = Scripts().Where(s => s.Version > applied).OrderBy(s => s.Version).ToList();
        foreach (var script in scripts)
        {
            using var tx = connection.BeginTransaction();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = script.Sql;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO schema_info (version) VALUES ($v)";
                cmd.Parameters.AddWithValue("$v", script.Version);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            applied = script.Version;
        }
        return applied;
    }

    private sealed record Script(int Version, string Sql);

    private static IEnumerable<Script> Scripts()
    {
        var assembly = typeof(SqliteDatabase).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Store.Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal)))
        {
            var file = name[(name.IndexOf(".Store.Migrations.", StringComparison.Ordinal) + ".Store.Migrations.".Length)..];
            var digits = new string(file.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var version)) continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return new Script(version, reader.ReadToEnd());
        }
    }

    public static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ---- value helpers shared by the store files ----

    public static string Iso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseIso(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static object Db(string? value) => value is null ? DBNull.Value : value;
}
