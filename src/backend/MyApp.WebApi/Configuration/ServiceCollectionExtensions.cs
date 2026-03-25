using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using MyApp.Application.Configuration;
using MyApp.Infrastructure.Configuration;
using MyApp.WebApi.Authorization;
using MyApp.WebApi.Configuration.HealthChecks;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace MyApp.WebApi.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures OpenTelemetry logging, tracing, and metrics using Azure Monitor.
    /// </summary>
    public static WebApplicationBuilder ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        var otelBuilder = builder.Services.AddOpenTelemetry();

        // Only enable Azure Monitor when a connection string is configured
        var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? builder.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(aiConnectionString))
        {
            otelBuilder.UseAzureMonitor();
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.ParseStateValues = true;
        });

        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System", LogLevel.Warning);
        builder.Logging.AddFilter("MyApp", LogLevel.Information);

        return builder;
    }

    /// <summary>
    /// Registers core application and infrastructure services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpClient();
        services.AddSingleton(configuration);
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration, environment);
        return services;
    }

    /// <summary>
    /// Configures authentication using Azure AD and sets up authorization policies.
    /// </summary>
    public static IServiceCollection AddAuthenticationAndAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAuthenticatedUser", policy => policy.RequireAuthenticatedUser())
            // Role-based policies (hierarchical: Admin > Editor > Viewer)
            .AddPolicy("Admin", policy => policy.Requirements.Add(new OrganizationPermissionRequirement(OrganizationPermission.Admin)))
            .AddPolicy("Editor", policy => policy.Requirements.Add(new OrganizationPermissionRequirement(OrganizationPermission.Editor)))
            .AddPolicy("Viewer", policy => policy.Requirements.Add(new OrganizationPermissionRequirement(OrganizationPermission.Viewer)));

        services.AddScoped<IAuthorizationHandler, OrganizationPermissionHandler>();
        return services;
    }

    /// <summary>
    /// Registers MVC controllers with Newtonsoft.Json support.
    /// </summary>
    public static IServiceCollection AddMyAppControllers(this IServiceCollection services)
    {
        services.AddControllers().AddNewtonsoftJson(o =>
        {
            o.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
            o.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        });

        return services;
    }

    /// <summary>
    /// Adds Swagger generation and documentation support.
    /// </summary>
    public static IServiceCollection AddMyAppSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer(); // Enable endpoint API explorer
        services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename)); // Include XML documentation

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "MyApp API",
                Description = "API for MyApp platform"
            });
        });
        return services;
    }

    /// <summary>
    /// Configures CORS policies for allowed client origins.
    /// </summary>
    public static IServiceCollection AddCorsPolicies(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("MyAppCORS", policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:3147",
                        "https://localhost:3147",
                        "https://MyAppsadev.z38.web.core.windows.net",
                        "https://MyAppsaprod.z38.web.core.windows.net",
                        "https://appdev.MyApp.eu",
                        "https://app.MyApp.eu") // Allowed client origins
                    .AllowAnyHeader() // Allow all headers
                    .AllowAnyMethod(); // Allow all HTTP methods
            });
        });
        return services;
    }

    /// <summary>
    /// Adds rate limiting policies and rejection handling.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global policy: bypass rate limiting for impersonated E2E test requests
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var config = context.RequestServices.GetRequiredService<IConfiguration>();
                var impersonationEnabled = config.GetValue<bool>("MyApp:Impersonation:Enabled");
                var headerName = config.GetValue<string>("MyApp:Impersonation:HeaderName") ?? "X-MyApp-Impersonate-UserId";

                if (impersonationEnabled && context.Request.Headers.ContainsKey(headerName))
                {
                    return RateLimitPartition.GetNoLimiter("e2e-bypass");
                }

                return RateLimitPartition.GetTokenBucketLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1000,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 1000,
                        AutoReplenishment = true
                    });
            });

            // Helper: check if the request is an impersonated E2E test request
            bool IsE2EBypass(HttpContext ctx)
            {
                var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
                return cfg.GetValue<bool>("MyApp:Impersonation:Enabled")
                    && ctx.Request.Headers.ContainsKey(
                        cfg.GetValue<string>("MyApp:Impersonation:HeaderName") ?? "X-MyApp-Impersonate-UserId");
            }

            // Default policy: 100 requests/minute per user (bypassed for E2E)
            options.AddPolicy("DefaultPerUser", context =>
            {
                if (IsE2EBypass(context))
                    return RateLimitPartition.GetNoLimiter("e2e-bypass");

                return RateLimitPartition.GetTokenBucketLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    key => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 100,
                        AutoReplenishment = true
                    });
            });

            // File Upload: 10 uploads/minute per user (bypassed for E2E)
            options.AddPolicy("FileUpload", context =>
            {
                if (IsE2EBypass(context))
                    return RateLimitPartition.GetNoLimiter("e2e-bypass");

                return RateLimitPartition.GetTokenBucketLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    key => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 10,
                        AutoReplenishment = true
                    });
            });

            // Admin operations: 5 requests/minute (bypassed for E2E)
            options.AddPolicy("AdminOperations", context =>
            {
                if (IsE2EBypass(context))
                    return RateLimitPartition.GetNoLimiter("e2e-bypass");

                return RateLimitPartition.GetTokenBucketLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin",
                    key => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 5,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = 429; // Too Many Requests

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var baseSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    var jitter = Random.Shared.Next(0, 3);
                    var retryAfterWithJitter = baseSeconds + jitter;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterWithJitter.ToString();
                }

                await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Adds health check services, including custom SQL Server connectivity check.
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver"); // Register custom health check

        return services;
    }
}
