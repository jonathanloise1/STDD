using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard;

/// <summary>The JSON API of the dashboard (operations.md 10.6).</summary>
public static class DashboardApi
{
    public static void Map(IEndpointRouteBuilder app, bool entra)
    {
        app.MapGet("/api/incidents", async (HttpContext http, IIncidentStore store) =>
        {
            var filter = new IncidentFilter(
                Status: Query(http, "status"), Severity: Query(http, "severity"), Trigger: Query(http, "trigger"), VerdictSource: Query(http, "verdict"), Area: Query(http, "area"));
            var rows = await store.ListAsync(filter, 200, http.RequestAborted).ConfigureAwait(false);
            return Results.Content("[" + string.Join(",", rows.Select(r => r.ReportJson)) + "]", "application/json");
        });

        app.MapGet("/api/incidents/{id}", async (string id, HttpContext http, IIncidentStore store) =>
        {
            var row = await store.GetAsync(id, http.RequestAborted).ConfigureAwait(false);
            if (row is null) return Results.NotFound(new { error = "No such incident." });
            if (Query(http, "download") is "true" or "1")
                http.Response.Headers.ContentDisposition = $"attachment; filename=\"{row.Id}.json\"";
            return Results.Content(MonitorJson.Report(row.Report), "application/json");
        });

        var ingest = app.MapPost("/api/incidents/ingest", async (HttpContext http, IncidentActions actions) =>
        {
            JsonObject? report;
            try
            {
                using var reader = new StreamReader(http.Request.Body);
                report = JsonNode.Parse(await reader.ReadToEndAsync(http.RequestAborted).ConfigureAwait(false)) as JsonObject;
            }
            catch (JsonException) { report = null; }
            if (report is null) return Results.Json(new { error = "The body must be an incident report (a JSON object)." }, statusCode: 400);

            var result = await actions.IngestAsync(report, http.RequestAborted).ConfigureAwait(false);
            return Results.Json(new { id = result.Id, message = result.Message, errors = result.Errors }, statusCode: result.Status);
        });
        if (entra) ingest.RequireAuthorization(DashboardRoles.IngestPolicy);

        var health = app.MapGet("/api/health", async (IIncidentStore store, MonitorStatus status, HttpContext http) =>
        {
            var checks = new JsonArray();
            var overall = "Healthy";
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                await store.CountByStatusAsync(http.RequestAborted).ConfigureAwait(false);
                checks.Add(new JsonObject { ["name"] = "store", ["status"] = "Healthy", ["durationMs"] = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            }
            catch (Exception)
            {
                overall = "Unhealthy";
                checks.Add(new JsonObject { ["name"] = "store", ["status"] = "Unhealthy", ["durationMs"] = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            }
            var sources = await status.HealthAsync(http.RequestAborted).ConfigureAwait(false);
            var failing = sources.Count(s => s.Status == SourceStatus.Unhealthy);
            if (failing > 0 && overall == "Healthy") overall = "Degraded";
            checks.Add(new JsonObject { ["name"] = "sources", ["status"] = failing == 0 ? "Healthy" : "Degraded", ["durationMs"] = 0 });
            return Results.Content(new JsonObject { ["status"] = overall, ["checks"] = checks }.ToJsonString(), "application/json");
        });
        if (entra) health.AllowAnonymous();
    }

    private static string? Query(HttpContext http, string name) => http.Request.Query.TryGetValue(name, out var v) && v.Count > 0 && v[0] is { Length: > 0 } s ? s : null;
}
