using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor;

/// <summary>Everything built from one loaded pack: profile, masker, tool executor, analyser settings.</summary>
public sealed class MonitorRuntime
{
    public required OperationsPack Pack { get; init; }
    public required Profile Profile { get; init; }
    public required Masker Masker { get; init; }
    public required ToolServices ToolServices { get; init; }
    public required ToolExecutor Executor { get; init; }
    public required string Environment { get; init; }
}

/// <summary>
/// Builds a <see cref="MonitorRuntime"/> from a pack. The pack can be replaced between batches (operations.md 13.3), so everything that
/// depends on it is created here rather than registered once. Later passes add the store, notifiers and the investigator host on top.
/// </summary>
public sealed class MonitorRuntimeFactory(IServiceProvider services, MonitorOptions options, IConfiguration configuration, TimeProvider time)
{
    public MonitorRuntime Create(OperationsPack pack, string? worldDirectory = null, string? environmentOverride = null)
    {
        var hashKey = options.RequireHashKey();
        var profile = pack.GetProfile(configuration);
        var masker = new Masker(pack.Masking, hashKey);
        var packProvider = new StaticPackProvider(pack);

        var toolServices = new ToolServices(
            pack.Entities, pack.Masking, packProvider,
            services.GetService<ISqlReadRunner>(), services.GetService<ILogAnalyticsClient>(), services.GetService<IWorkspaceResolver>(),
            services.GetService<IHttpClientFactory>(), options.ResolveContentRoot(), time);

        var implementations = BuiltInTools.Create(toolServices).Concat(services.GetServices<IToolImplementation>()).ToList();
        var bindings = worldDirectory is null ? ToolBindings.FromProfile(profile) : ToolBindings.ForWorld(pack.Tools, worldDirectory);
        var executor = new ToolExecutor(
            pack.Tools, pack.Entities, pack.Constraints, masker, bindings, implementations, services.GetServices<IToolAuditSink>(), time,
            services.GetService<ILoggerFactory>()?.CreateLogger("MyApp.Monitor.Tools"));

        return new MonitorRuntime
        {
            Pack = pack,
            Profile = profile,
            Masker = masker,
            ToolServices = toolServices,
            Executor = executor,
            Environment = environmentOverride ?? options.Environment ?? profile.Environment,
        };
    }

    public Analyser CreateAnalyser(MonitorRuntime runtime, IIncidentIndex? index = null, ICounterStore? counters = null, IUnknownTemplateSink? unknowns = null) =>
        new(runtime.Profile.Analyser, new PackSignatureCatalog(runtime.Pack.Signatures),
            index ?? services.GetService<IIncidentIndex>() ?? new InMemoryIncidentIndex(time),
            counters ?? services.GetService<ICounterStore>() ?? new InMemoryCounterStore(),
            unknowns ?? services.GetService<IUnknownTemplateSink>() ?? new InMemoryUnknownTemplateSink(),
            time);
}
