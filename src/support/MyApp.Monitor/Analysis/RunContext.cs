using Microsoft.Extensions.Logging;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Rules;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Analysis;

/// <summary>
/// Everything that depends on the loaded pack, in one object: the runtime (profile, masker, executor), the analyser, the pipeline,
/// the rule engine and the notification settings. A new valid pack replaces the whole context between batches (operations.md 13.3).
/// </summary>
public sealed class RunContext
{
    public required MonitorRuntime Runtime { get; init; }
    public required Analyser Analyser { get; init; }
    public required AnalysisPipeline Pipeline { get; init; }
    public required RuleEngine Rules { get; init; }
    public required SchemaSubsetValidator ReportValidator { get; init; }
    public required NotificationContext Notifications { get; init; }

    public OperationsPack Pack => Runtime.Pack;
    public Profile Profile => Runtime.Profile;
}

public interface IRunContextProvider
{
    RunContext Current { get; }
}

public sealed class RunContextHolder : IRunContextProvider
{
    private volatile RunContext? _current;

    public RunContext Current => _current ?? throw new MonitorException("the monitor has no operations pack loaded yet");
    public bool HasContext => _current is not null;

    public void Replace(RunContext context) => _current = context;
}

public sealed class RunContextFactory(
    MonitorRuntimeFactory runtimes, ISourceStateStore state, DashboardAddress address, TimeProvider time)
{
    public RunContext Create(OperationsPack pack, string? worldDirectory = null)
    {
        var runtime = runtimes.Create(pack, worldDirectory);
        var analyser = runtimes.CreateAnalyser(runtime);
        return new RunContext
        {
            Runtime = runtime,
            Analyser = analyser,
            Pipeline = new AnalysisPipeline(runtime.Masker, analyser, state, time),
            Rules = new RuleEngine(runtime.Executor, pack.Entities, time),
            ReportValidator = new SchemaSubsetValidator(pack.IncidentReportSchema),
            Notifications = NotificationContext.Create(runtime.Profile, pack, address, runtime.Masker),
        };
    }
}
