using System.Text.Json.Nodes;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>
/// The hand-written bench of the sample application (<c>docs/support/bench/investigator</c>, operations.md 11.4): the scenarios are well formed, use real log
/// keys, and the calls their gold answers rest on return data through the monitor's own executor and masking over their worlds.
/// </summary>
public class BenchScenarioTests
{
    private static readonly string Dir = Path.Combine(Repo.Root, "docs", "support", "bench", "investigator");

    private static IEnumerable<string> Files => Directory.GetFiles(Dir, "*.json").OrderBy(f => f, StringComparer.Ordinal);
    public static IEnumerable<TestCaseData> Scenarios() => Files.Select(f => new TestCaseData(f).SetName("scenario " + Path.GetFileNameWithoutExtension(f)));

    private static JsonObject Load(string file) => (JsonObject)JsonNode.Parse(File.ReadAllText(file))!;

    [Test]
    public void There_are_at_least_twenty_scenarios_covering_the_cases_the_document_names()
    {
        var all = Files.Select(Load).ToList();
        Assert.That(all.Count, Is.GreaterThanOrEqualTo(20));
        Assert.That(all.Select(s => s["id"]!.GetValue<string>()).Distinct().Count(), Is.EqualTo(all.Count));
        Assert.That(all.Count(s => s["trigger"]!.GetValue<string>() == "rate" && s["prompt"]!["pack"] is not null), Is.GreaterThanOrEqualTo(5), "a known signature at an abnormal rate");
        Assert.That(all.Count(s => s["prompt"]!["pack"] is null && s["gold"]!["verdict"]!.GetValue<string>() == "explained"), Is.GreaterThanOrEqualTo(3), "an unknown signature");
        Assert.That(all.Count(s => s["prompt"]!["incident"]!["rule_checks"] is not null || s["gold"]!["abstain_reason"]?.GetValue<string>() == "conflicting_evidence"), Is.GreaterThanOrEqualTo(2), "contradicting evidence");
        var reasons = all.Select(s => s["gold"]!["abstain_reason"]?.GetValue<string>()).Where(r => r is not null).ToHashSet();
        Assert.That(reasons, Is.SupersetOf(new[] { "insufficient_evidence", "conflicting_evidence", "out_of_scope", "budget_exhausted" }), "abstention in every reason");
        Assert.That(all.Count(s => s["budget_override"] is not null), Is.GreaterThanOrEqualTo(2), "the budget running out");
        Assert.That(all.Select(s => s["gold"]!["cause_category"]!.GetValue<string>()).ToHashSet(), Is.SupersetOf(new[] { "access", "data_missing", "data_conflict", "dependency_unavailable", "configuration", "code_defect", "external_input", "expected_behaviour" }));
    }

    [TestCaseSource(nameof(Scenarios))]
    public async Task A_scenario_uses_real_keys_masked_records_and_calls_that_return_data(string file)
    {
        var s = Load(file);
        var pack = SharedFixtures.Pack;
        var prompt = s["prompt"]!;
        Assert.That(s["split"]!.GetValue<string>(), Is.EqualTo("bench"));

        // the incident of the prompt is masked and the keyed signature is one the pack has
        Assert.That(prompt["incident"]!.ToJsonString(), Does.Not.Contain("@example.com"));
        var sig = prompt["incident"]!["signature"]!;
        if (sig["kind"]!.GetValue<string>() == "keyed") Assert.That(IsRealKey(sig["log_key"]!.GetValue<string>()), Is.True, "a log key of the sample application");
        Assert.That(prompt["entities"]!.AsArray().Select(e => e!["name"]!.GetValue<string>()), Is.EqualTo(pack.Entities.Entities.Select(e => e.Name)));
        Assert.That(prompt["pack"] is null, Is.EqualTo(sig["kind"]!.GetValue<string>() != "keyed"));

        // the gold answer is consistent
        var gold = s["gold"]!;
        var abstain = gold["verdict"]!.GetValue<string>() == "abstain";
        Assert.That(gold["abstain_reason"] is null, Is.EqualTo(!abstain));
        Assert.That(gold["supporting_evidence"]!.AsArray().Count == 0, Is.EqualTo(abstain));
        Assert.That(Protocol.CauseCategories, Does.Contain(gold["cause_category"]!.GetValue<string>()));

        // the supporting calls give data through the executor, masking and a world: nothing denied, nothing unmasked
        var world = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, s["world_dir"]!.GetValue<string>()));
        var executor = new ToolExecutor(pack.Tools, pack.Entities, pack.Constraints, Fx.Masker(), ToolBindings.ForWorld(pack.Tools, world),
            BuiltInTools.Create(new ToolServices(pack.Entities, pack.Masking, new MyApp.Monitor.Pack.StaticPackProvider(pack), null, null, null, null, world, FakeTime.Default())), [], FakeTime.Default());
        var session = new IncidentToolSession("bench");
        foreach (var call in gold["supporting_evidence"]!.AsArray())
        {
            var env = await executor.ExecuteAsync(new ToolRequest(call!["tool"]!.GetValue<string>(), (JsonObject)call["arguments"]!.DeepClone(), "test", session), default);
            Assert.That(env.Status, Is.AnyOf("ok", "empty"), $"{call["tool"]}: {env.Error}");
            var text = env.ToJson().ToJsonString();
            Assert.That(text, Does.Not.Contain("@example.com").And.Not.Contain("never-leaves-the-tool"));
            Assert.That(env.Data, Is.Not.Null);
        }
        // the world's logs hold the triggering trace when the scenario has one
        var firstRecord = prompt["incident"]!["evidence"]![0]!["records"]![0]!;
        if (firstRecord["TraceId"] is { } trace)
        {
            var env = await executor.ExecuteAsync(new ToolRequest("logs_by_correlation", new JsonObject { ["trace_id"] = trace.GetValue<string>() }, "test", new IncidentToolSession("bench2")), default);
            Assert.That(env.Rows, Is.GreaterThan(0), "the triggering trace is in the world's logs");
        }
    }

    // the fixture pack has the tasks area only; the keys are checked against the log definitions of the repository
    private static bool IsRealKey(string key) =>
        Directory.GetFiles(Path.Combine(Repo.Root, "src", "backend", "MyApp.Application", "Logging"), "*.cs").Any(f => File.ReadAllText(f).Contains("\"" + key + "\""));
}
