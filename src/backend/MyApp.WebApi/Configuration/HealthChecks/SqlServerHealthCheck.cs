using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyApp.Application.Logging;

namespace MyApp.WebApi.Configuration.HealthChecks;

public class SqlServerHealthCheck(
    ILogger<SqlServerHealthCheck> logger,
    IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SQL Server health check...");

        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            logger.LogInformation("SQL Server health check succeeded.");
            return HealthCheckResult.Healthy("SQL Server is reachable.");
        }
        catch (Exception ex)
        {
            logger.PlatformHealthUnhealthy(ex, "sqlserver");
            return HealthCheckResult.Unhealthy("SQL Server is unreachable.", ex);
        }
    }
}
