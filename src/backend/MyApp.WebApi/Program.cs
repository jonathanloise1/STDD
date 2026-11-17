using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MyApp.Application.Help;
using MyApp.Application.Logging;
using MyApp.Infrastructure.Logging;
using MyApp.WebApi.ClientEvents;
using MyApp.WebApi.Configuration;
using MyApp.WebApi.Configuration.HealthChecks;
using MyApp.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry logging, traces, and metrics
builder.ConfigureOpenTelemetry();

// Register all services via extension methods
builder.Services
    .AddApplicationServices(builder.Configuration, builder.Environment)
    .AddAuthenticationAndAuthorization(builder.Configuration)
    .AddMyAppControllers()
    .AddMyAppSwagger()
    .AddCorsPolicies(builder.Configuration)
    .AddRateLimiting()
    .AddHealthCheckServices();

var app = builder.Build();

// Build the client events map now so a malformed definition stops the start (operations.md 3.6)
app.Services.GetRequiredService<ClientEventDispatcher>();

// Read the help pack and the lock now: a missing or incompatible one is logged once and turns generation off (models.md 7.5)
app.Services.GetRequiredService<HelpRuntime>();

// First: every request runs in a W3C trace context (operations.md 4.1)
app.UseMiddleware<TraceContextMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

// Use middleware pipeline
app.UseCors("MyAppCORS");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyApp API v1");
        options.RoutePrefix = "swagger";
    });
}

// Responses with an error status and no body (401, 403, 404, 405) get the problem document too
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    if (status == ExceptionHandlingMiddleware.ClientClosedRequest)
        return;

    var detail = status switch
    {
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status403Forbidden => "Access denied.",
        StatusCodes.Status404NotFound => "Resource not found.",
        _ => "The request could not be completed."
    };
    await ProblemResponse.WriteAsync(context.HttpContext, ProblemResponse.Create(status, detail));
});
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ImpersonationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.UseRateLimiter();

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

// Redirect root to Swagger UI
app.MapGet("/", context =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Redirecting root to Swagger UI...");
    context.Response.Redirect("/swagger/index.html");
    return Task.CompletedTask;
});

// Run migrations
await MigrateAsync(app);

app.Logger.LogInformation("MyApp API started successfully.");
await app.RunAsync();

// Runs startup migrations for infrastructure and application layers
static async Task MigrateAsync(IHost host)
{
    var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;
    using var scope = host.Services.CreateScope();
    var serviceProvider = scope.ServiceProvider;
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

    try
    {
        await MyApp.Infrastructure.Configuration.Startup.InitAsync(serviceProvider, env, cancellationToken);
    }
    catch (Exception ex)
    {
        var summary = ExceptionSummary.From(ex);
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.WebApi.Startup")
            .PlatformStartupFailed(ex, summary.ExceptionType, summary.TopFrame);
        throw;
    }
}
