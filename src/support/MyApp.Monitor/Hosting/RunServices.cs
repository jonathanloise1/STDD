using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Hosting;

/// <summary>Registrations the test project (or a project's own code) can add to the run host after the monitor's own.</summary>
public sealed record ServiceOverrides(Action<IServiceCollection>? Configure);

public sealed record RunInfo(Uri? DashboardUri, IServiceProvider Services, SqliteStore Store);

/// <summary>Told when the monitor is up. Tests use it to find the dashboard address of a monitor started in process.</summary>
public interface IRunObserver
{
    void Started(RunInfo info);
}

public static class RunServices
{
    /// <summary>
    /// Registers everything <c>run</c> adds to <see cref="MonitorApp.AddMonitor"/>: the SQLite store behind every persistence seam, the
    /// processor, notifications, the host and the dashboard's services. The in-memory registrations of <c>AddMonitor</c> are replaced.
    /// </summary>
    public static IServiceCollection AddMonitorRun(this IServiceCollection services, SqliteStore store, MonitorHostOptions hostOptions, string? dashboardUrls)
    {
        services.RemoveAll<ISourceStateStore>();
        services.RemoveAll<IIncidentIndex>();
        services.RemoveAll<ICounterStore>();
        services.RemoveAll<IUnknownTemplateSink>();
        services.AddSingleton(store);
        services.AddSingleton<ISourceStateStore>(store);
        services.AddSingleton<IIncidentIndex>(store);
        services.AddSingleton<ICounterStore>(store);
        services.AddSingleton<IUnknownTemplateSink>(store);
        services.AddSingleton<IToolAuditSink>(store);
        services.AddSingleton<IIncidentStore>(store);

        services.AddSingleton(new DashboardAddress { BaseUrl = DashboardAddress.FromUrls(dashboardUrls) });
        services.AddSingleton<RunContextHolder>();
        services.AddSingleton<IRunContextProvider>(sp => sp.GetRequiredService<RunContextHolder>());
        services.AddSingleton<RunContextFactory>();
        services.AddSingleton<MonitorStatus>();
        services.AddSingleton(hostOptions);

        services.AddHttpClient(HttpTicketWebhookClient.ClientName);
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        services.TryAddSingleton<ITicketWebhookClient, HttpTicketWebhookClient>();
        services.RemoveAll<IInvestigator>();   // `run` has the real host; the other commands keep the no-op
        services.AddSingleton(Protocol.Load());
        services.AddHttpClient(LlamaServerBackendFactory.ClientName, c => c.Timeout = Timeout.InfiniteTimeSpan);   // the wall time of an investigation is the loop's, not the client's
        services.TryAddSingleton<IInferenceBackendFactory, LlamaServerBackendFactory>();
        services.AddSingleton<InvestigatorHost>();
        services.AddSingleton<IInvestigator>(sp => sp.GetRequiredService<InvestigatorHost>());
        services.AddHostedService(sp => sp.GetRequiredService<InvestigatorHost>());
        services.TryAddSingleton<NotificationOptions>();
        services.AddSingleton<SuppressionService>();
        services.AddSingleton<NotificationDispatcher>();
        services.AddSingleton<DigestService>();
        services.AddSingleton<IncidentProcessor>();
        services.AddSingleton<IncidentActions>();
        services.AddHostedService<MonitorHost>();
        return services;
    }
}
