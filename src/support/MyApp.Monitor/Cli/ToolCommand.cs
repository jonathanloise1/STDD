using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Cli;

/// <summary>
/// <c>tool {name} --args {json} [--incident id] [--world dir]</c>: one audited call through the executor; prints the envelope. With <c>--incident</c> the incident's
/// numbering, duplicates and budgets continue across invocations (<see cref="ToolSessionFile"/>), as the evaluation and <c>support.agent.md</c> call it one process at a time.
/// A world that has its own <c>pack/</c> is answered with that pack's entities, masking policy, catalog and signatures; without one, with the repository's.
/// The exit code is 1 when the call did not succeed (denied, error, duplicate...); the envelope is printed either way.
/// </summary>
public sealed class ToolCommand : ICommandHandler
{
    public string Name => "tool";
    public string Usage => "tool {name} --args {json} [--incident {id}] [--world {dir}] [--pack {path}] [--profile {name}] [--state {dir}]";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool>
    {
        ["args"] = true, ["incident"] = true, ["world"] = true, ["pack"] = true, ["profile"] = true, ["repo"] = true, ["state"] = true,
    };

    public async Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        if (args.Positional.Count != 1) throw new UsageException("give exactly one tool name");
        var name = args.Positional[0];

        JsonObject? arguments = null;
        if (args.Get("args") is { } json)
        {
            try { arguments = MonitorJson.Parse(json) as JsonObject ?? throw new UsageException("--args must be a JSON object"); }
            catch (System.Text.Json.JsonException ex) { throw new UsageException($"--args is not valid JSON: {ex.Message}"); }
        }
        var world = args.Get("world");
        if (world is not null && !Directory.Exists(world)) throw new UsageException($"--world: directory not found: {world}");

        var useWorldPack = world is not null && WorldPack.Exists(world) && args.Get("pack") is null && context.Options.Pack.Path is null;
        var pack = useWorldPack ? WorldPack.Load(world!) : PackSelection.Resolve(args, context);
        var factory = context.Services.GetRequiredService<MonitorRuntimeFactory>();
        var runtime = factory.Create(pack, world);

        var incident = args.Get("incident");
        var sessions = new ToolSessionFile(args.Get("state") ?? ToolSessionFile.DefaultDirectory);
        var session = incident is null ? new IncidentToolSession(null) : sessions.Open(incident);
        var caller = "cli:" + Environment.UserName;
        var envelope = await runtime.Executor.ExecuteAsync(new ToolRequest(name, arguments, caller, session), ct).ConfigureAwait(false);
        if (incident is not null) sessions.Save(session);
        context.Out.WriteLine(envelope.ToJson().ToJsonString(MonitorJson.SnakeCompact));   // one line: the evaluation reads the last line of the output
        return envelope.Status is ToolStatus.Ok or ToolStatus.Empty ? ExitCodes.Ok : ExitCodes.Failed;
    }
}
