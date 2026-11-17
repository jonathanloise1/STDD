using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using MyApp.Application.Configuration;
using MyApp.Application.Logging;
using MyApp.Infrastructure.Configuration;
using MyApp.Infrastructure.Logging;
using MyApp.WebApi.Authorization;
using MyApp.WebApi.ClientEvents;
using MyApp.WebApi.Help;
using MyApp.WebApi.Configuration.HealthChecks;
using MyApp.WebApi.Middlewares;
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

        // The log key processor is registered before the exporters (UseAzureMonitor below), so the key
        // is on the record when it is exported (operations.md 3.3).
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.ParseStateValues = true;
            logging.AddProcessor(new LogKeyProcessor());
        });

        // Only enable Azure Monitor when a connection string is configured
        var aiConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? builder.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(aiConnectionString))
        {
            otelBuilder.UseAzureMonitor();
        }

        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Logging.AddJsonFile(builder.Configuration, builder.Environment);
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

        // Development-only fault switch for automated browser tests (off by default)
        services.AddSingleton<Filters.FaultSwitch>();
        services.AddSingleton(configuration);
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration, environment);
        services.AddHelp(environment);
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
        services.AddControllers()
            .ConfigureApiBehaviorOptions(o =>
            {
                // Binding and model validation errors use the problem document of operations.md 4.2
                o.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());
                    var detail = errors.Values.SelectMany(m => m).FirstOrDefault() ?? "Invalid request.";
                    return new ProblemResult(ProblemResponse.Create(StatusCodes.Status400BadRequest, detail, errors: errors));
                };
            })
            .AddNewtonsoftJson(o =>
            {
                o.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
                o.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            });

        // Log key to definition map for POST /api/client-events (operations.md 3.6); resolved at startup
        services.AddSingleton(sp => ClientEventDispatcher.ForApplication(sp.GetRequiredService<ILoggerFactory>()));

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
    public static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        // The allowed origins are configuration (MyApp:Cors:AllowedOrigins), not code: each deployment names its own
        var origins = configuration.GetSection("MyApp:Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy("MyAppCORS", policy =>
            {
                policy
                    .WithOrigins(origins) // Allowed client origins
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
                // Only in Development (ImpersonationSettings): elsewhere the header is just a header
                if (ImpersonationSettings.IsRequested(context))
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
            bool IsE2EBypass(HttpContext ctx) => ImpersonationSettings.IsRequested(ctx);

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
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "global";
                loggerFactory.CreateLogger("MyApp.WebApi.Configuration.RateLimiting").PlatformRequestRateLimited(policy);

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var baseSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    var jitter = Random.Shared.Next(0, 3);
                    var retryAfterWithJitter = baseSeconds + jitter;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterWithJitter.ToString();
                }

                await ProblemResponse.WriteAsync(context.HttpContext, ProblemResponse.Create(
                    StatusCodes.Status429TooManyRequests, "Too many requests. Please try again later."));
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
