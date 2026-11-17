using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Cli;

/// <summary>
/// <c>run [--profile p] [--pack path]</c> (operations.md 14.4): starts the sources, the analyser, the store, the notifiers and the dashboard
/// and runs until it is stopped. Unattended: it never prompts, and a missing secret, an invalid pack, a store that cannot open or a
/// dashboard that cannot start stops the start with one Critical log entry. Stopping (Ctrl+C, SIGTERM, the service manager) lets the
/// batch in flight finish and its checkpoint be saved.
/// </summary>
public sealed class RunCommand : ICommandHandler
{
    public string Name => "run";
    public string Usage => "run [--profile {name}] [--pack {path}] [--repo {dir}]";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool> { ["profile"] = true, ["pack"] = true, ["repo"] = true };

    public static readonly EventId StartFailed = new(1, "monitor.start.failed");

    private sealed record Prepared(OperationsPack Pack, Profile Profile, SqliteStore Store, string? WatchPath, string Urls);

    public async Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Monitor");
        Prepared prepared;
        try { prepared = Prepare(args, context); }
        catch (Exception ex) when (ex is MonitorException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            logger.Log(LogLevel.Critical, StartFailed, "The monitor did not start: {Error}", ex.Message);
            return ExitCodes.Failed;
        }

        IHost host;
        try { host = Build(prepared, context); }
        catch (Exception ex) when (ex is MonitorException or InvalidOperationException or IOException)
        {
            prepared.Store.Database.Close();
            logger.Log(LogLevel.Critical, StartFailed, "The monitor did not start: {Error}", ex.Message);
            return ExitCodes.Failed;
        }

        using (host)
        {
            var holder = host.Services.GetRequiredService<RunContextHolder>();
            var address = host.Services.GetRequiredService<DashboardAddress>();
            var status = host.Services.GetRequiredService<MonitorStatus>();
            try
            {
                address.Hold();
                holder.Replace(host.Services.GetRequiredService<RunContextFactory>().Create(prepared.Pack));
                status.PackPath = prepared.WatchPath ?? prepared.Pack.Location;
                status.PackLoaded();
                status.StorePath = prepared.Store.Database.Path;
                status.StoreSchemaVersion = prepared.Store.Database.SchemaVersion;

                await host.StartAsync(ct).ConfigureAwait(false);
                Uri? dashboard = null;
                if (prepared.Profile.Dashboard.Enabled)
                {
                    var bound = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
                    address.BaseUrl = DashboardAddress.FromUrls(string.Join(';', bound));
                    dashboard = new Uri(address.BaseUrl);
                    logger.LogInformation("Dashboard at {Url} (auth: {Auth})", address.BaseUrl, prepared.Profile.Dashboard.Auth);
                }
                address.Release();
                context.Services.GetService<IRunObserver>()?.Started(new RunInfo(dashboard, host.Services, prepared.Store));

                await host.WaitForShutdownAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* stopping */ }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Sockets.SocketException)
            {
                logger.Log(LogLevel.Critical, StartFailed, "The monitor did not start: {Error}", ex.Message);
                address.Release();
                await StopAsync(host, prepared).ConfigureAwait(false);
                return ExitCodes.Failed;
            }
            logger.LogInformation("Stopping");
            await StopAsync(host, prepared).ConfigureAwait(false);
        }
        return ExitCodes.Ok;
    }

    private static async Task StopAsync(IHost host, Prepared prepared)
    {
        try { await host.StopAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
        finally { prepared.Store.Database.Close(); }
    }

    private static Prepared Prepare(CommandArgs args, CommandContext context)
    {
        var options = context.Options;
        options.RequireHashKey();   // 8.2: without the masking key the monitor refuses to start
        options.ValidateAzure();

        var pack = PackSelection.Resolve(args, context);
        var profile = pack.GetProfile(context.Configuration);
        if (profile.UnresolvedSettings.Count > 0)
            throw new MonitorException($"the profile needs settings that have no value and no default: {string.Join(", ", profile.UnresolvedSettings)}");

        var urls = options.Dashboard.Urls ?? profile.Dashboard.Urls ?? "http://localhost:5310";
        if (DashboardAuthentication.Validate(profile.Dashboard, urls, context.Configuration) is { } problem) throw new MonitorException(problem);

        var path = Path.GetFullPath(profile.Store.Path, options.ResolveContentRoot());
        var store = new SqliteStore(SqliteDatabase.Open(path));
        var explicitPack = args.Get("pack") ?? options.Pack.Path;
        return new Prepared(pack, profile, store, string.IsNullOrWhiteSpace(explicitPack) ? null : Path.GetFullPath(explicitPack), urls);
    }

    private static IHost Build(Prepared prepared, CommandContext context)
    {
        var dashboard = prepared.Profile.Dashboard;
        var hostOptions = new MonitorHostOptions { WatchPackPath = prepared.WatchPath };
        var overrides = context.Services.GetService<ServiceOverrides>();

        void Register(IServiceCollection services, IConfiguration configuration)
        {
            MonitorApp.AddMonitor(services, configuration);
            services.AddMonitorRun(prepared.Store, hostOptions, prepared.Urls);
            services.AddSystemd();
            services.AddWindowsService(o => o.ServiceName = "MyApp.Monitor");
            services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
            if (dashboard.Enabled) DashboardHosting.AddDashboard(services, dashboard, configuration);
            overrides?.Configure?.Invoke(services);   // last, so that a test or a project can replace any of the above
        }

        if (!dashboard.Enabled)
        {
            var generic = Host.CreateApplicationBuilder();
            ConfigureCommon(generic.Configuration, generic.Logging, context);
            Register(generic.Services, generic.Configuration);
            return generic.Build();
        }

        var web = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MonitorApp).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            Args = [],
        });
        ConfigureCommon(web.Configuration, web.Logging, context);
        web.WebHost.UseUrls(prepared.Urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Register(web.Services, web.Configuration);
        var app = web.Build();
        DashboardHosting.Map(app, dashboard);
        return app;
    }

    private static void ConfigureCommon(ConfigurationManager configuration, ILoggingBuilder logging, CommandContext context)
    {
        ((IConfigurationBuilder)configuration).Sources.Clear();
        configuration.AddConfiguration(context.Configuration);
        logging.ClearProviders();
        logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
    }
}
