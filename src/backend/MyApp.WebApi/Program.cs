using MyApp.WebApi.Configuration;
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
    .AddCorsPolicies()
    .AddRateLimiting()
    .AddHealthCheckServices();

var app = builder.Build();

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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ImpersonationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.UseRateLimiter();

app.MapHealthChecks("/health");

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

    await MyApp.Infrastructure.Configuration.Startup.InitAsync(serviceProvider, env, cancellationToken);
}
