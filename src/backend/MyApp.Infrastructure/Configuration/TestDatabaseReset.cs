using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MyApp.Infrastructure.Configuration;

/// <summary>The answer of <see cref="TestDatabaseReset.Evaluate"/>: whether the database may be dropped, and why not.</summary>
public sealed record TestDatabaseResetDecision(bool Allowed, string Reason);

/// <summary>
/// The guard of the E2E reset (development.md 10.3). At start, the backend may drop the database it is
/// connected to, migrate it and seed it again, so that every E2E run begins from the same data. That is
/// destructive, so it happens only when ALL of these hold:
/// <list type="number">
/// <item>the environment is Development;</item>
/// <item><c>MyApp:Testing:ResetDatabaseOnStart</c> is <c>true</c>;</item>
/// <item>the database name in the connection string ends in <c>_E2E</c> or <c>_Test</c>.</item>
/// </list>
/// Any other database, whatever the switch says, is left alone.
/// </summary>
public static class TestDatabaseReset
{
    public const string SwitchKey = "MyApp:Testing:ResetDatabaseOnStart";

    /// <summary>The endings a database name must have to be reset.</summary>
    public static readonly IReadOnlyList<string> AllowedSuffixes = new[] { "_E2E", "_Test" };

    /// <summary>Whether the switch is on. It says nothing about whether the reset is allowed.</summary>
    public static bool IsRequested(IConfiguration configuration) =>
        bool.TryParse(configuration[SwitchKey], out var requested) && requested;

    public static TestDatabaseResetDecision Evaluate(
        IConfiguration configuration, IHostEnvironment environment, string? connectionString)
    {
        if (!environment.IsDevelopment())
            return new(false, "The environment is not Development.");

        if (!IsRequested(configuration))
            return new(false, $"{SwitchKey} is not true.");

        var name = DatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(name))
            return new(false, "The connection string names no database.");

        if (!AllowedSuffixes.Any(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)))
            return new(false, $"The database '{name}' does not end in {string.Join(" or ", AllowedSuffixes)}.");

        return new(true, $"The database '{name}' may be reset.");
    }

    /// <summary>The database a connection string points at, or null when it names none or cannot be read.</summary>
    public static string? DatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            return new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
