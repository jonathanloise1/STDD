using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Return;
using MyApp.Monitor.Sources;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>The return loop of operations.md 15 (G5) and <c>tool --incident</c> across processes (14.4), with a world that brings its own pack.</summary>
public class ReturnAndToolSessionTests
{
    private record Outcome(int Code, string Out, string Error);

    private static async Task<Outcome> Run(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        using var content = new TempDir();
        var code = await MonitorApp.RunAsync(args, o, e, configuration: new Dictionary<string, string?> { ["Monitor:Masking:HashKey"] = Fx.HashKey, ["Monitor:ContentRoot"] = content.Path });
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        return new Outcome(code, o.ToString(), e.ToString());
    }

    // ---- the return loop ----

    private static async Task<(string File, TempDir Dir, FixtureRepo Repo, string Id)> ClosedIncident(string outcome, string? note = "Fixed in the role check.", Func<Rig, Task>? before = null)
    {
        using var rig = new Rig();
        await rig.FeedAsync(rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var id = (await rig.AllIncidentsAsync()).Single().Id;
        var result = await rig.Actions.CloseAsync(id, new CloseRequest(outcome == "duplicate" ? "wontfix" : outcome, "TCK-7", null, note, null), "anna");
        Assert.That(result.Ok, Is.True, result.Message);
        var dir = new TempDir();
        var file = dir.File("report.json");
        var json = (await rig.Store.GetAsync(id))!.ReportJson;
        File.WriteAllText(file, outcome == "duplicate" ? json.Replace("\"value\":\"wontfix\"", "\"value\":\"duplicate\"").Replace("\"value\": \"wontfix\"", "\"value\": \"duplicate\"") : json);
        return (file, dir, new FixtureRepo(), id);
    }

    [Test]
    public async Task A_fixed_outcome_gives_the_branch_the_runbook_section_and_the_pull_request_text_and_touches_nothing()
    {
        var (file, dir, repo, id) = await ClosedIncident("fixed");
        using var _ = dir; using var __ = repo;
        var before = repo.Read(FixtureRepo.Ops);
        var r = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"), "--release", "1.5.0");

        Assert.That(r.Code, Is.EqualTo(0), r.Error);
        Assert.That(r.Out, Does.Contain("Nothing was committed, pushed or opened"));
        Assert.That(File.ReadAllText(dir.File("out/branch.txt")).Trim(), Is.EqualTo($"fix/{id}-task.create.unavailable"));
        var runbook = File.ReadAllText(dir.File("out/runbook.md"));
        Assert.That(runbook, Does.Contain("| Status | Fixed |").And.Contain("| Fixed in | 1.5.0 |").And.Contain($"- 2026-11-06 {id} fixed TCK-7"));
        var findings = new FindingList();
        var doc = RunbookParser.ParseArea("# Tasks - Operations\n\n" + runbook[(runbook.IndexOf("-->", StringComparison.Ordinal) + 3)..].TrimStart(), "x", new RunbookParser.Options(new DateOnly(2026, 11, 6)), findings);
        Assert.That(findings.Select(f => f.ToString()), Is.Empty, "the section is a valid runbook");
        Assert.That((doc.Entries.Single().Status, doc.Entries.Single().FixedIn, doc.Entries.Single().History.Last().Outcome), Is.EqualTo(("fixed", "1.5.0", "fixed")));
        Assert.That(doc.Entries.Single().History, Has.Count.EqualTo(2), "the earlier history line stays and the new one follows it");
        var change = JsonNode.Parse(File.ReadAllText(dir.File("out/signature-change.json")))!;
        Assert.That(change["after"]!["status"]!.GetValue<string>(), Is.EqualTo("Fixed"));
        Assert.That(change["before"]!["status"]!.GetValue<string>(), Is.EqualTo("Active"));
        var body = File.ReadAllText(dir.File("out/pull-request.md"));
        Assert.That(body, Does.StartWith($"Incident: {id} (prod), outcome fixed\nSignature: keyed task.create.unavailable\nStory / test case: US-TASK-01 / TASK-CREATE-").And.Contain("Reviewer needed: as usual").And.Contain("E1: "));
        Assert.That(body, Does.Contain("no branch was created, nothing was committed or pushed"));
        Assert.That(repo.Read(FixtureRepo.Ops), Is.EqualTo(before), "the repository is only read");
        Assert.That(Directory.GetFileSystemEntries(dir.File("out")).Select(Path.GetFileName), Is.EquivalentTo(new[] { "branch.txt", "pull-request.md", "runbook.md", "signature-change.json" }));
    }

    [Test]
    public async Task A_fixed_outcome_without_a_release_says_so_and_leaves_a_placeholder()
    {
        var (file, dir, repo, _) = await ClosedIncident("fixed");
        using var _d = dir; using var _r = repo;
        var r = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That(r.Error, Does.Contain("--release"));
        Assert.That(File.ReadAllText(dir.File("out/runbook.md")), Does.Contain("| Fixed in | {release} |"));
    }

    [Test]
    public async Task A_noise_outcome_sets_the_decision_with_an_expiry_within_180_days_and_asks_for_another_reviewer()
    {
        var (file, dir, repo, id) = await ClosedIncident("noise", "Known after every team change.");
        using var _d = dir; using var _r = repo;
        var r = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That(r.Code, Is.EqualTo(0), r.Error);
        var runbook = File.ReadAllText(dir.File("out/runbook.md"));
        var expires = DateOnly.Parse(System.Text.RegularExpressions.Regex.Match(runbook, @"\| Noise expires \| (\d{4}-\d{2}-\d{2}) \|").Groups[1].Value);
        Assert.That(Math.Abs(expires.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).AddDays(180).DayNumber), Is.LessThanOrEqualTo(1));
        Assert.That(runbook, Does.Contain("| Status | Noise |").And.Contain("| Noise rationale | Known after every team change. |").And.Contain($"{id} noise TCK-7"));
        Assert.That(File.ReadAllText(dir.File("out/pull-request.md")), Does.Contain("Reviewer needed: yes, a reviewer other than the author"));
        Assert.That(File.ReadAllText(dir.File("out/notes.txt")), Does.Contain("renew it"));
    }

    [Test]
    public async Task A_duplicate_has_no_repository_change_and_writes_nothing()
    {
        var (file, dir, repo, _) = await ClosedIncident("duplicate", null);
        using var _d = dir; using var _r = repo;
        var r = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That((r.Code, r.Out), Is.EqualTo((0, r.Out)));
        Assert.That(r.Out, Does.Contain("no repository change"));
        Assert.That(Directory.Exists(dir.File("out")), Is.False);
    }

    [Test]
    public async Task An_unknown_resolved_outcome_gives_a_runbook_skeleton_to_complete_and_a_report_that_is_not_closed_is_refused()
    {
        using var rig = new Rig();
        await rig.FeedAsync(Rig.Unknown("A widget exploded"));
        var id = (await rig.AllIncidentsAsync()).Single().Id;
        using var dir = new TempDir(); using var repo = new FixtureRepo();
        var file = dir.File("report.json");
        File.WriteAllText(file, (await rig.Store.GetAsync(id))!.ReportJson);
        var open = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That(open.Code, Is.EqualTo(1));
        Assert.That(open.Error, Does.Contain("no outcome"));

        await rig.Actions.CloseAsync(id, new CloseRequest("unknown_resolved", null, "https://example.test/pr/1", "A new test case is needed.", null), "anna");
        File.WriteAllText(file, (await rig.Store.GetAsync(id))!.ReportJson);
        var r = await Run("return", "--report", file, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That(r.Code, Is.EqualTo(0), r.Error);
        Assert.That(File.ReadAllText(dir.File("out/branch.txt")).Trim(), Does.StartWith($"fix/{id}-t-"));
        var skeleton = File.ReadAllText(dir.File("out/runbook.md"));
        Assert.That(skeleton, Does.Contain("{log key}").And.Contain($"- 2026-11-06 {id} unknown_resolved https://example.test/pr/1"));
        Assert.That(File.ReadAllText(dir.File("out/pull-request.md")), Does.Contain("new test case with its Observable trace").And.Contain("log definition"));
    }

    [Test]
    public async Task The_report_can_be_read_from_the_store_by_incident_id()
    {
        using var rig = new Rig();
        await rig.FeedAsync(rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var id = (await rig.AllIncidentsAsync()).Single().Id;
        await rig.Actions.CloseAsync(id, new CloseRequest("wontfix", null, null, "Accepted.", null), "anna");
        rig.Store.Database.Close();
        using var dir = new TempDir(); using var repo = new FixtureRepo();
        var r = await Run("return", "--incident", id, "--store", rig.DbPath, "--repo", repo.Root, "--out", dir.File("out"));
        Assert.That(r.Code, Is.EqualTo(0), r.Error);
        Assert.That(File.ReadAllText(dir.File("out/runbook.md")), Does.Contain($"{id} wontfix"));
    }

    [TestCase("INC-20261106-0007", "task.create.not_allowed", "fix/INC-20261106-0007-task.create.not_allowed")]
    [TestCase("INC-20261106-0007", "t-0123456789ab", "fix/INC-20261106-0007-t-0123456789ab")]
    [TestCase("INC-1", "odd key: ~^?*[x]..lock", "")]
    public void The_branch_name_is_the_incident_and_the_subject_with_only_characters_git_accepts(string incident, string subject, string expectedStart)
    {
        var name = ReturnPlanner.BranchName(incident, subject);
        Assert.That(name, Does.StartWith("fix/INC-"));
        Assert.That(name, Does.Not.Contain("..").And.Not.Match(@"[\s~^:?*\[\\]").And.Not.EndWith(".lock").And.Not.EndWith("."));
        if (!subject.Contains(' ')) Assert.That(name, Is.EqualTo(expectedStart));
    }

    // ---- tool --incident across processes ----

    private const string Args = """{"trace_id":"4bf92f3577b34da6a3ce929d0e0e4736"}""";

    [Test]
    public async Task Evidence_numbers_duplicates_and_budgets_continue_between_invocations_of_one_incident()
    {
        using var state = new TempDir(); using var repo = new FixtureRepo();
        var pack = repo.Build(zip: false).PackDirectory!;
        Task<Outcome> Call(string tool, string args, string incident = "INC-TOOL-1") =>
            Run("tool", tool, "--args", args, "--incident", incident, "--world", FixtureRepo.WorldRoot, "--pack", pack, "--state", state.Path);

        var first = JsonNode.Parse((await Call("health_status", "{}")).Out)!;
        var second = JsonNode.Parse((await Call("logs_by_correlation", Args)).Out)!;
        var again = await Call("health_status", "{}");
        Assert.That((first["evidence_id"]!.GetValue<string>(), first["call"]!.GetValue<int>()), Is.EqualTo(("E2", 1)));
        Assert.That((second["evidence_id"]!.GetValue<string>(), second["call"]!.GetValue<int>()), Is.EqualTo(("E3", 2)));
        var dup = JsonNode.Parse(again.Out)!;
        Assert.That((again.Code, dup["status"]!.GetValue<string>(), dup["evidence_id"]!.GetValue<string>(), dup["call"]!.GetValue<int>()), Is.EqualTo((1, "duplicate", "E2", 3)), "the same call is a duplicate even in a new process");
        var other = JsonNode.Parse((await Call("health_status", "{}", "INC-TOOL-2")).Out)!;
        Assert.That(other["evidence_id"]!.GetValue<string>(), Is.EqualTo("E2"), "another incident starts again");
        Assert.That(Directory.GetFiles(state.Path, "*.json"), Has.Length.EqualTo(2));
        Assert.That(File.ReadAllText(Directory.GetFiles(state.Path, "*.json")[0]), Does.Not.Contain("jane"), "the state holds counters and keys, not results");
    }

    [Test]
    public async Task Without_an_incident_every_call_starts_again()
    {
        using var repo = new FixtureRepo();
        var pack = repo.Build(zip: false).PackDirectory!;
        var a = JsonNode.Parse((await Run("tool", "health_status", "--args", "{}", "--world", FixtureRepo.WorldRoot, "--pack", pack)).Out)!;
        var b = JsonNode.Parse((await Run("tool", "health_status", "--args", "{}", "--world", FixtureRepo.WorldRoot, "--pack", pack)).Out)!;
        Assert.That((a["evidence_id"]!.GetValue<string>(), b["evidence_id"]!.GetValue<string>()), Is.EqualTo(("E2", "E2")));
    }

    [Test]
    public async Task A_world_with_its_own_pack_is_answered_with_that_packs_entities_and_masking()
    {
        using var world = new TempDir();
        var support = Path.Combine(FixtureRepo.FixturesRoot, "repo", "docs", "support");
        Directory.CreateDirectory(Path.Combine(world.Path, "pack"));
        Directory.CreateDirectory(Path.Combine(world.Path, "entities"));
        foreach (var f in new[] { "tools.json", "constraints.json" }) File.Copy(Path.Combine(support, f), Path.Combine(world.Path, "pack", f));
        var entities = new JsonObject
        {
            ["schema_version"] = 1,
            ["entities"] = new JsonArray(new JsonObject
            {
                ["name"] = "widget", ["description"] = "A fictional record.", ["source_kind"] = "sqlserver", ["table"] = "dbo.Widgets", ["key"] = "Id", ["key_type"] = "guid", ["audit_entity_type"] = null,
            }),
        };
        File.WriteAllText(Path.Combine(world.Path, "pack", "entities.json"), entities.ToJsonString());
        var policy = JsonNode.Parse(File.ReadAllText(Path.Combine(support, "masking-policy.json")))!.AsObject();
        policy["entities"] = new JsonObject { ["widget"] = new JsonObject { ["Id"] = "id", ["Owner"] = "free_text", ["Secret"] = "hash" } };
        File.WriteAllText(Path.Combine(world.Path, "pack", "masking-policy.json"), policy.ToJsonString());
        File.WriteAllText(Path.Combine(world.Path, "pack", "signatures.json"), """{"schema_version":1,"signatures":[]}""");
        const string id = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91";
        File.WriteAllText(Path.Combine(world.Path, "entities", "widget.jsonl"), $$"""{"Id":"{{id}}","Owner":"call jane.doe@example.com","Secret":"Value","Dropped":"x"}""" + "\n");

        var r = await Run("tool", "sql_entity_read", "--args", $$"""{"entity":"widget","id":"{{id}}"}""", "--world", world.Path);
        Assert.That(r.Code, Is.EqualTo(0), r.Error + r.Out);
        var env = JsonNode.Parse(r.Out)!;
        Assert.That(env["data"]!["found"]!.GetValue<bool>(), Is.True);
        Assert.That(r.Out, Does.Contain("[email]").And.Not.Contain("jane.doe@example.com").And.Not.Contain("\"Dropped\""));
        var other = await Run("tool", "sql_entity_read", "--args", $$"""{"entity":"organization","id":"{{id}}"}""", "--world", world.Path);
        Assert.That(JsonNode.Parse(other.Out)!["status"]!.GetValue<string>(), Is.EqualTo("denied"), "an entity of the repository is not one of this world");
    }
}
