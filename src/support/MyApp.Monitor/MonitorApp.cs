using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Monitor.Cli;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor;

/// <summary>Composition and entry point. Later passes add their registrations in <see cref="AddMonitor"/> and their commands to the dispatcher.</summary>
public static class MonitorApp
{
    public static async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, Action<IServiceCollection>? configureServices = null,
        IDictionary<string, string?>? configuration = null, CancellationToken ct = default)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = null, DisableDefaults = false });
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();
        if (configuration is not null) builder.Configuration.AddInMemoryCollection(configuration);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        AddMonitor(builder.Services, builder.Configuration);
        configureServices?.Invoke(builder.Services);
        builder.Services.AddSingleton(new ServiceOverrides(configureServices));   // `run` builds its own host and applies the same overrides to it

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<MonitorOptions>();
        var context = new CommandContext(output, error, host.Services, options, builder.Configuration, host.Services.GetRequiredService<TimeProvider>());
        try
        {
            return await host.Services.GetRequiredService<CommandDispatcher>().RunAsync(args, context, ct).ConfigureAwait(false);
        }
        catch (PackException ex)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.Failed;
        }
        catch (MonitorException ex)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.Failed;
        }
    }

    /// <summary>Registers the monitor's own services. <c>run</c> replaces the in-memory state with the SQLite store and adds the host, notifiers and dashboard (<see cref="RunServices.AddMonitorRun"/>).</summary>
    public static IServiceCollection AddMonitor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(configuration);
        services.AddSingleton(_ => MonitorOptions.From(configuration));

        // commands (14.4)
        services.AddSingleton<ICommandHandler, PackBuildCommand>();
        services.AddSingleton<ICommandHandler, PackVerifyCommand>();
        services.AddSingleton<ICommandHandler, ToolCommand>();
        services.AddSingleton<ICommandHandler, AnalyseCommand>();
        services.AddSingleton<ICommandHandler, ReturnCommand>();
        services.AddSingleton<ICommandHandler, RunCommand>();
        services.AddSingleton<CommandDispatcher>();

        // runtime built from a pack
        services.AddSingleton<MonitorRuntimeFactory>();

        // sources, registered by kind (6.2)
        services.AddHttpClient("monitor-health").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None || request?.RequestUri?.IsLoopback == true,
        });
        services.AddSingleton<ISourceStateStore, InMemorySourceStateStore>();
        services.AddSingleton<ISignalSourceFactory, LogFileSourceFactory>();
        services.AddSingleton<ISignalSourceFactory, HttpHealthSourceFactory>();
        services.AddSingleton<ISignalSourceFactory, WorldSourceFactory>();
        services.AddSingleton<ISignalSourceFactory, LogAnalyticsSourceFactory>();

        // cloud clients: real implementations, created on first use so that no credential is touched by other commands
        services.AddSingleton<Azure.Core.TokenCredential>(sp => AzureCredentialFactory.Create(sp.GetRequiredService<MonitorOptions>()));
        services.AddSingleton<ILogAnalyticsClient>(sp => new AzureLogAnalyticsClient(sp.GetRequiredService<Azure.Core.TokenCredential>()));
        services.AddSingleton<IWorkspaceDiscoveryClient>(sp => new AzureWorkspaceDiscoveryClient(sp.GetRequiredService<Azure.Core.TokenCredential>()));
        services.AddSingleton<IWorkspaceResolver>(sp => new WorkspaceResolver(
            sp.GetRequiredService<IWorkspaceDiscoveryClient>(), sp.GetRequiredService<ISourceStateStore>(), sp.GetRequiredService<MonitorOptions>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ISqlReadRunner, SqlServerReadRunner>();

        // analysis state; the store replaces these with its own
        services.AddSingleton<IIncidentIndex>(sp => new InMemoryIncidentIndex(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ICounterStore, InMemoryCounterStore>();
        services.AddSingleton<IUnknownTemplateSink, InMemoryUnknownTemplateSink>();

        // the investigator host belongs to a later pass: until it registers its own, residual cases keep their rule verdict
        services.TryAddSingleton<IInvestigator, NoOpInvestigator>();
        return services;
    }
}
