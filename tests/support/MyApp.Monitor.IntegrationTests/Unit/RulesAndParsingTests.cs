using System.Text.Json.Nodes;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Rules;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class ExpectationParserTests
{
    private static bool Eval(string expect, string data)
    {
        Assert.That(Expectation.TryParse(expect, out var e, out var error), Is.True, error);
        return e!.Evaluate(JsonNode.Parse(data));
    }

    [TestCase("""status == "Healthy" """, """{"status":"Healthy"}""", true)]
    [TestCase("""status == "Healthy" """, """{"status":"Degraded"}""", false)]
    [TestCase("""status != "Healthy" """, """{"status":"Degraded"}""", true)]
    [TestCase("rows > 3", """{"rows":4}""", true)]
    [TestCase("rows > 3", """{"rows":3}""", false)]
    [TestCase("rows >= 3", """{"rows":3}""", true)]
    [TestCase("rows < 3", """{"rows":2}""", true)]
    [TestCase("rows <= 2", """{"rows":3}""", false)]
    [TestCase("found == true", """{"found":true}""", true)]
    [TestCase("found == false", """{"found":true}""", false)]
    [TestCase("fields.IsDeleted == true", """{"fields":{"IsDeleted":true}}""", true)]
    [TestCase("checks[0].status == \"Healthy\"", """{"checks":[{"status":"Healthy"}]}""", true)]
    [TestCase("checks.1.name == \"disk\"", """{"checks":[{"name":"a"},{"name":"disk"}]}""", true)]
    [TestCase("signature exists", """{"signature":{"id":"x"}}""", true)]
    [TestCase("signature exists", """{"signature":null}""", false)]
    [TestCase("signature exists", """{}""", false)]
    [TestCase("error absent", """{"status":"x"}""", true)]
    [TestCase("error absent", """{"error":"boom"}""", false)]
    [TestCase("error absent", """{"error":null}""", true)]
    [TestCase("value == 12", """{"value":"12"}""", true)]
    [TestCase("""name == "12" """, """{"name":12}""", true)]
    [TestCase("missing != \"x\"", """{}""", false)]
    [TestCase("missing > 1", """{}""", false)]
    [TestCase("name > 1", """{"name":"abc"}""", false)]
    public void Evaluation(string expect, string data, bool expected) => Assert.That(Eval(expect, data), Is.EqualTo(expected));

    [TestCase("")]
    [TestCase("status")]
    [TestCase("status = \"x\"")]
    [TestCase("status == Healthy")]
    [TestCase("status exists now")]
    [TestCase("== 3")]
    public void Invalid_expectations_are_rejected(string text) => Assert.That(Expectation.TryParse(text, out _, out _), Is.False);
}

public class RunbookParserTests
{
    private static readonly RunbookParser.Options Options = new(new DateOnly(2026, 11, 6), new HashSet<string> { "health_status", "logs_by_correlation", "sql_entity_read" });

    private static string Section(Action<Dictionary<string, string>>? change = null, string? rule = null, string? history = null, string anchor = "task-create-access-lost", string sections = "")
    {
        var f = new Dictionary<string, string>
        {
            ["Log key"] = "`task.create.access_lost`", ["Test case"] = "[TASK-CREATE-005](test-cases/TASK-CREATE-005.md)", ["Expected rate"] = "5 per hour",
            ["Fingerprint by"] = "OrganizationId", ["Cause category"] = "access", ["Status"] = "Active", ["Fixed in"] = "None", ["Noise rationale"] = "None", ["Noise expires"] = "None",
        };
        change?.Invoke(f);
        var table = string.Join("\n", f.Select(kv => $"| {kv.Key} | {kv.Value} |"));
        return $"""
        ## {anchor}

        | Field | Value |
        |-------|-------|
        {table}

        ### Meaning

        Something happened.

        ### Check

        1. Look.

        ### Rule checks

        {rule ?? "None."}

        ### Resolve

        1. Hand over.

        ### History

        {history ?? "None."}
        {sections}
        """;
    }

    private static (RunbookDocument Doc, FindingList Findings) Parse(string body, string h1 = "# Tasks - Operations")
    {
        var findings = new FindingList();
        var doc = RunbookParser.ParseArea($"{h1}\n\n{body}\n", "docs/areas/tasks/operations.md", Options, findings);
        return (doc, findings);
    }

    private static string Messages(FindingList f) => string.Join("\n", f.Select(x => $"[{x.Check}] {x.Message}"));

    [Test]
    public void A_valid_section_is_parsed_completely()
    {
        var (doc, findings) = Parse(Section(rule: "| Tool | Arguments | Expect |\n|------|-----------|--------|\n| health_status | - | status == \"Healthy\" |\n| logs_by_correlation | trace_id={signal.TraceId}, limit=50 | records exists |",
            history: "- 2026-10-01 INC-20261001-0001 fixed https://example.invalid/pr/1\n- 2026-10-02 INC-20261002-0002 duplicate"));
        Assert.That(findings, Is.Empty, Messages(findings));
        var e = doc.Entries.Single();
        Assert.That(e.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(e.TestCaseId, Is.EqualTo("TASK-CREATE-005"));
        Assert.That(e.ExpectedPerHour, Is.EqualTo(5));
        Assert.That(e.FingerprintBy, Is.EqualTo(new[] { "OrganizationId" }));
        Assert.That(e.Status, Is.EqualTo("active"));
        Assert.That(e.RuleChecks, Has.Count.EqualTo(2));
        Assert.That(e.RuleChecks[1].Arguments["trace_id"]!.GetValue<string>(), Is.EqualTo("{signal.TraceId}"));
        Assert.That(e.RuleChecks[1].Arguments["limit"]!.GetValue<long>(), Is.EqualTo(50));
        Assert.That(e.History, Has.Count.EqualTo(2));
        Assert.That(e.History[0].Reference, Is.EqualTo("https://example.invalid/pr/1"));
        Assert.That(e.History[1].Reference, Is.Null);
        Assert.That(e.Text, Does.StartWith("### Meaning"));
        Assert.That(e.Text, Does.Not.Contain("| Field |"));
    }

    [Test]
    public void The_h1_must_name_the_area_operations() =>
        Assert.That(Messages(Parse(Section(), "# Tasks").Findings), Does.Contain("H1 must be"));

    [Test]
    public void The_heading_must_be_the_anchor_of_the_log_key() =>
        Assert.That(Messages(Parse(Section(anchor: "wrong-anchor")).Findings), Does.Contain("is not the anchor of log key"));

    [Test]
    public void Field_rows_must_be_exactly_the_specified_ones_in_order() =>
        Assert.That(Messages(Parse(Section(f => f.Remove("Fixed in"))).Findings), Does.Contain("field rows must be exactly"));

    [TestCase("Expected rate", "five per hour", "Expected rate must be")]
    [TestCase("Expected rate", "-1 per hour", "Expected rate must be")]
    [TestCase("Fingerprint by", "organizationId", "PascalCase")]
    [TestCase("Fingerprint by", "A, B, C, D", "at most 3")]
    [TestCase("Fingerprint by", "A, A", "repeats")]
    [TestCase("Cause category", "bug", "Cause category must be one of")]
    [TestCase("Status", "Maybe", "Status must be")]
    [TestCase("Log key", "`Task.Create`", "does not match the grammar")]
    [TestCase("Test case", "TASK-CREATE-005", "must be a link")]
    [TestCase("Test case", "[TASK-CREATE-005](x.md)", "link target")]
    public void Field_values_are_validated(string field, string value, string expectedMessage) =>
        Assert.That(Messages(Parse(Section(f => f[field] = value)).Findings), Does.Contain(expectedMessage));

    [Test]
    public void Fixed_in_is_set_if_and_only_if_status_is_fixed()
    {
        Assert.That(Messages(Parse(Section(f => f["Status"] = "Fixed")).Findings), Does.Contain("Fixed in is required"));
        Assert.That(Messages(Parse(Section(f => f["Fixed in"] = "1.4.0")).Findings), Does.Contain("must be None unless Status is Fixed"));
        Assert.That(Messages(Parse(Section(f => { f["Status"] = "Fixed"; f["Fixed in"] = "v1"; })).Findings), Does.Contain("is not a version"));
        Assert.That(Parse(Section(f => { f["Status"] = "Fixed"; f["Fixed in"] = "1.4.0"; })).Findings, Is.Empty);
    }

    [Test]
    public void Noise_fields_are_set_if_and_only_if_status_is_noise()
    {
        Assert.That(Messages(Parse(Section(f => f["Status"] = "Noise")).Findings), Does.Contain("if and only if Status is Noise"));
        Assert.That(Messages(Parse(Section(f => f["Noise rationale"] = "because")).Findings), Does.Contain("if and only if Status is Noise"));
        Assert.That(Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "framework"; f["Noise expires"] = "2027-01-01"; })).Findings, Is.Empty);
    }

    [Test]
    public void A_noise_expiry_before_the_build_date_is_check_O10_and_more_than_180_days_ahead_is_refused()
    {
        var expired = Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "x"; f["Noise expires"] = "2026-11-05"; })).Findings;
        Assert.That(expired.Select(f => f.Check), Does.Contain("O10"));
        var today = Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "x"; f["Noise expires"] = "2026-11-06"; })).Findings;
        Assert.That(today, Is.Empty);
        var far = Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "x"; f["Noise expires"] = "2027-06-01"; })).Findings;
        Assert.That(Messages(far), Does.Contain("more than 180 days"));
        var last = Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "x"; f["Noise expires"] = "2027-05-05"; })).Findings;
        Assert.That(last, Is.Empty, "exactly 180 days ahead");
        Assert.That(Messages(Parse(Section(f => { f["Status"] = "Noise"; f["Noise rationale"] = "x"; f["Noise expires"] = "06/11/2027"; })).Findings), Does.Contain("YYYY-MM-DD"));
    }

    [Test]
    public void Meaning_check_and_resolve_are_never_empty()
    {
        var body = Section().Replace("Something happened.", "");
        Assert.That(Messages(Parse(body).Findings), Does.Contain("'Meaning' is empty"));
        Assert.That(Messages(Parse(Section().Replace("1. Look.", "")).Findings), Does.Contain("'Check' is empty"));
        Assert.That(Messages(Parse(Section().Replace("1. Hand over.", "")).Findings), Does.Contain("'Resolve' is empty"));
    }

    [Test]
    public void Subsections_must_be_exactly_the_five_in_order() =>
        Assert.That(Messages(Parse(Section(sections: "### Extra\n\nmore")).Findings), Does.Contain("subsections must be exactly"));

    [Test]
    public void Rule_checks_are_validated()
    {
        const string head = "| Tool | Arguments | Expect |\n|------|-----------|--------|\n";
        Assert.That(Messages(Parse(Section(rule: head + "| no_such_tool | - | a exists |")).Findings), Does.Contain("unknown tool 'no_such_tool'"));
        Assert.That(Messages(Parse(Section(rule: head + "| health_status | - | status is Healthy |")).Findings), Does.Contain("is not '{path} {op} {literal}'"));
        Assert.That(Messages(Parse(Section(rule: head + "| health_status | oops | a exists |")).Findings), Does.Contain("must be name=value"));
        Assert.That(Messages(Parse(Section(rule: "| Tool | Expect |\n|---|---|\n| health_status | a exists |")).Findings), Does.Contain("Rule checks must be"));
        Assert.That(Messages(Parse(Section(rule: "free text")).Findings), Does.Contain("Rule checks must be"));
        var eleven = string.Join("\n", Enumerable.Repeat("| health_status | - | status exists |", 11));
        Assert.That(Messages(Parse(Section(rule: head + eleven)).Findings), Does.Contain("at most 10 rule checks"));
    }

    [Test]
    public void History_lines_are_validated()
    {
        Assert.That(Messages(Parse(Section(history: "- yesterday something")).Findings), Does.Contain("History line"));
        Assert.That(Messages(Parse(Section(history: "- 2026-10-01 INC-20261001-0001 vanished")).Findings), Does.Contain("History line"));
    }

    [Test]
    public void Sections_must_be_ordered_by_log_key_and_not_repeated()
    {
        var a = Section();
        var b = Section(f => { f["Log key"] = "`task.create.unavailable`"; f["Test case"] = "[TASK-CREATE-008](test-cases/TASK-CREATE-008.md)"; }, anchor: "task-create-unavailable");
        Assert.That(Parse(a + "\n" + b).Findings, Is.Empty);
        Assert.That(Messages(Parse(b + "\n" + a).Findings), Does.Contain("must be ordered by log key"));
        Assert.That(Messages(Parse(a + "\n" + a).Findings), Does.Contain("more than one section"));
    }

    [Test]
    public void Findings_of_an_area_runbook_carry_the_id_O06()
    {
        var f = Parse(Section(f => f["Status"] = "Maybe")).Findings;
        Assert.That(f.Select(x => x.Check).Distinct(), Is.EqualTo(new[] { "O06" }));
    }

    // ---- unkeyed (5.5) ----

    private static FindingList ParseUnkeyed(string text)
    {
        var findings = new FindingList();
        RunbookParser.ParseUnkeyed(text, "docs/support/unkeyed-runbooks.md", Options, findings);
        return findings;
    }

    private static string UnkeyedSection(string id = "t-2bb5dcdc1949", string template = "sqlserver") => $"""
        ## {id}

        | Field | Value |
        |-------|-------|
        | Event name | platform.health.unhealthy |
        | Category | MyApp.WebApi.Health |
        | Exception type | None |
        | Top frame | None |
        | Template | {template} |
        | Severity | Error |
        | Expected rate | 0 per hour |
        | Fingerprint by | None |
        | Cause category | dependency_unavailable |
        | Status | Active |
        | Fixed in | None |
        | Noise rationale | None |
        | Noise expires | None |

        ### Meaning

        m

        ### Check

        1. c

        ### Rule checks

        None.

        ### Resolve

        1. r

        ### History

        None.
        """;

    [Test]
    public void Unkeyed_runbooks_accept_none_or_sections_with_the_computed_id()
    {
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\nNone.\n"), Is.Empty);
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\n" + UnkeyedSection() + "\n"), Is.Empty);
    }

    [Test]
    public void The_unkeyed_heading_must_equal_the_computed_template_id_O07()
    {
        var f = ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\n" + UnkeyedSection(id: "t-000000000000") + "\n");
        Assert.That(f.Single().Check, Is.EqualTo("O07"));
        Assert.That(f.Single().Message, Does.Contain("t-2bb5dcdc1949"));
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\n" + UnkeyedSection(template: "other") + "\n").Single().Message, Does.Contain("must equal the template id"));
    }

    [Test]
    public void Unkeyed_structure_is_checked()
    {
        Assert.That(ParseUnkeyed("# Other\n\nIntro.\n\nNone.\n").Single().Message, Does.Contain("H1 must be"));
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\nNone.\n").Single().Message, Does.Contain("introductory paragraph"));
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\n" + UnkeyedSection() + "\n").Single().Message, Does.Contain("introductory paragraph"));
        Assert.That(ParseUnkeyed("# Unkeyed signatures\n\nIntro.\n\n" + UnkeyedSection(id: "t-ffffffffffff", template: "b") + "\n" + UnkeyedSection(id: "t-000000000001", template: "a") + "\n")
            .Select(x => x.Message), Has.Some.Contain("ordered by template id"));
    }

    [Test]
    public void Pipes_inside_a_cell_are_escaped_with_a_backslash()
    {
        var row = MarkdownTables.SplitRow(@"| Template | A\|B\|C |");
        Assert.That(row, Is.EqualTo(new[] { "Template", "A|B|C" }));
    }
}

public class TestCaseAndScannerTests
{
    private static string Case(string type = "Error handling", string trace = "## Observable trace\n\n| Field | Value |\n|-------|-------|\n| Message | \"Hello.\" |\n| Log key | `task.create.access_lost` |\n| Severity | Warning |\n| Runbook | [task-create-access-lost](../operations.md#task-create-access-lost) |\n") =>
        $"# TASK-CREATE-005: Title\n\n| Field | Value |\n|-------|-------|\n| User Story | US-TASK-01 |\n| Type | {type} |\n| Priority | High |\n| Automated by | None |\n\n## Description\n\nd\n\n{trace}";

    private static (TestCase? Case, FindingList Findings) Parse(string md)
    {
        var f = new FindingList();
        return (TestCaseParser.Parse(md, "tasks", "docs/areas/tasks/test-cases/TASK-CREATE-005.md", f), f);
    }

    [Test]
    public void An_observable_trace_is_read()
    {
        var (tc, findings) = Parse(Case());
        Assert.That(findings, Is.Empty);
        Assert.That(tc!.Id, Is.EqualTo("TASK-CREATE-005"));
        Assert.That(tc.UserStory, Is.EqualTo("US-TASK-01"));
        Assert.That(tc.Trace!.Message, Is.EqualTo("Hello."));
        Assert.That(tc.Trace.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(tc.Trace.Severity, Is.EqualTo("Warning"));
        Assert.That(tc.Trace.RunbookLinkTarget, Is.EqualTo("../operations.md#task-create-access-lost"));
    }

    [Test]
    public void Message_None_means_no_message()
    {
        var (tc, _) = Parse(Case(trace: "## Observable trace\n\n| Field | Value |\n|---|---|\n| Message | None |\n| Log key | `task.create.access_lost` |\n| Severity | Information |\n| Runbook | [task-create-access-lost](../operations.md#task-create-access-lost) |\n"));
        Assert.That(tc!.Trace!.Message, Is.Null);
    }

    [Test]
    public void A_happy_path_has_no_trace_and_a_trace_on_it_is_a_finding()
    {
        Assert.That(Parse(Case("Happy path", "")).Case!.Trace, Is.Null);
        Assert.That(Parse(Case("Happy path")).Findings.Single().Message, Does.Contain("must not have an Observable trace"));
        Assert.That(Parse(Case("Edge case", "")).Findings.Single().Message, Does.Contain("requires an Observable trace"));
    }

    [Test]
    public void Trace_problems_are_R13_findings()
    {
        var bad = Case(trace: "## Observable trace\n\n| Field | Value |\n|---|---|\n| Message | \"x\" |\n| Log key | `Bad.Key` |\n| Severity | Warning |\n| Runbook | [a](b) |\n");
        Assert.That(Parse(bad).Findings.Single().Message, Does.Contain("grammar"));
        var sev = Case(trace: "## Observable trace\n\n| Field | Value |\n|---|---|\n| Message | \"x\" |\n| Log key | `task.create.access_lost` |\n| Severity | Fatal |\n| Runbook | [a](b) |\n");
        Assert.That(Parse(sev).Findings.Single().Message, Does.Contain("severity 'Fatal'"));
        var missing = Case(trace: "## Observable trace\n\n| Field | Value |\n|---|---|\n| Message | \"x\" |\n");
        Assert.That(Parse(missing).Findings.Select(f => f.Check).Distinct(), Is.EqualTo(new[] { "R13" }));
        Assert.That(Parse("no heading").Findings.Single().Message, Does.Contain("heading"));
    }

    private const string Source = """
    using Microsoft.Extensions.Logging;
    namespace MyApp.Application.Logging;

    public static partial class TasksLog
    {
        public static class Keys
        {
            public const string TaskCreateAccessLost = "task.create.access_lost";
            public const string TaskEditNotFound = "task.edit.not_found";
        }

        // US-TASK-01, US-TASK-02
        [LoggerMessage(EventId = 1, EventName = Keys.TaskCreateAccessLost, Level = LogLevel.Warning,
            Message = "Task not created: user {UserId} has no access to {OrganizationId:N} " +
                      "and {@UserId}")]
        public static partial void TaskCreateAccessLost(this ILogger logger, Guid userId, Guid organizationId);

        [LoggerMessage(EventId = 2, EventName = TasksLog.Keys.TaskEditNotFound, Level = LogLevel.Error, Message = "Gone {TaskId}")]
        public static partial void TaskEditNotFound(this ILogger logger, Exception exception, Guid taskId);

        // US-TASK-09

        [LoggerMessage(EventId = 3, EventName = "literal.key.here", Level = LogLevel.Information, Message = "x")]
        public static partial void Literal(this ILogger logger);

        public static void NotLogging() { }
    }
    """;

    [Test]
    public void Log_definitions_are_read_with_resolved_keys_levels_templates_and_placeholders()
    {
        var defs = CallSiteScanner.ScanText(Source, "src/backend/MyApp.Application/Logging/TasksLog.cs");
        Assert.That(defs, Has.Count.EqualTo(3));
        var d = defs[0];
        Assert.That(d.EventName, Is.EqualTo("task.create.access_lost"));
        Assert.That(d.Level, Is.EqualTo("Warning"));
        Assert.That(d.Method, Is.EqualTo("TaskCreateAccessLost"));
        Assert.That(d.Class, Is.EqualTo("TasksLog"));
        Assert.That(d.Template, Is.EqualTo("Task not created: user {UserId} has no access to {OrganizationId:N} and {@UserId}"));
        Assert.That(d.Parameters, Is.EqualTo(new[] { "UserId", "OrganizationId" }));
        Assert.That(d.StoryComment, Is.EqualTo("US-TASK-01, US-TASK-02"));
        Assert.That(d.Line, Is.EqualTo(13), "the line of the attribute");
        Assert.That(defs[1].EventName, Is.EqualTo("task.edit.not_found"), "TasksLog.Keys.X resolves too");
        Assert.That(defs[1].Level, Is.EqualTo("Error"));
        Assert.That(defs[1].StoryComment, Is.Null, "no story comment immediately above");
        Assert.That(defs[2].EventName, Is.EqualTo("literal.key.here"));
        Assert.That(defs[2].StoryComment, Is.Null, "a blank line between comment and attribute breaks 'immediately above'");
    }

    [Test]
    public void Build_output_folders_are_excluded_from_a_repository_scan()
    {
        using var repo = new FixtureRepo();
        repo.Write("src/backend/MyApp.Application/obj/Generated.cs", Source);
        repo.Write("src/backend/MyApp.Application/bin/Debug/Other.cs", Source);
        var defs = CallSiteScanner.ScanRepository(repo.Root);
        Assert.That(defs.Select(d => d.File), Has.All.EqualTo("src/backend/MyApp.Application/Logging/TasksLog.cs"));
        Assert.That(defs, Has.Count.EqualTo(2));
    }

    [Test]
    public void Method_names_are_the_key_in_pascal_case() =>
        Assert.That(SignatureGenerator.PascalCase("task.create.access_lost"), Is.EqualTo("TaskCreateAccessLost"));
}

public class TemplateRendererTests
{
    private static readonly JsonNode Report = JsonNode.Parse("""
    { "id": "INC-1", "severity": "Error", "count": 41, "note": "say \"hi\"\nnow", "correlation": { "trace_ids": ["t1", "t2"] },
      "rule_verdict": null, "model_verdict": { "cause": "x" },
      "evidence": [ { "id": "E1", "source": "signal", "reference": "ref1", "excerpt": "ex1" }, { "id": "E2", "source": "t", "reference": "ref2", "excerpt": "ex2" } ] }
    """)!;

    [Test]
    public void Paths_arrays_nulls_and_numbers_render()
    {
        var r = TemplateRenderer.Render("{{id}} {{severity}} {{count}} {{correlation.trace_ids.1}} [{{rule_verdict}}] {{model_verdict.cause}}", Report, "http://d");
        Assert.That(r.Text, Is.EqualTo("INC-1 Error 41 t2 [] x"));
        Assert.That(r.UnknownPaths, Is.Empty);
    }

    [Test]
    public void An_unknown_path_renders_empty_and_is_reported()
    {
        var r = TemplateRenderer.Render("a{{nope.deeper}}b {{correlation.trace_ids.9}}", Report, "http://d");
        Assert.That(r.Text, Is.EqualTo("ab "));
        Assert.That(r.UnknownPaths, Is.EquivalentTo(new[] { "nope.deeper", "correlation.trace_ids.9" }));
    }

    [Test]
    public void Json_prefix_escapes_for_a_json_string()
    {
        var r = TemplateRenderer.Render("{\"n\":\"{{json:note}}\"}", Report, "http://d");
        Assert.That(JsonNode.Parse(r.Text)!["n"]!.GetValue<string>(), Is.EqualTo("say \"hi\"\nnow"));
    }

    [Test]
    public void Evidence_lines_and_dashboard_url()
    {
        var r = TemplateRenderer.Render("{{evidence_lines}}\n{{dashboard_url}}", Report, "http://d/INC-1");
        Assert.That(r.Text, Is.EqualTo("E1 signal ref1: ex1\nE2 t ref2: ex2\nhttp://d/INC-1"));
        Assert.That(TemplateRenderer.Render("{{evidence_lines}}", Report, "x", includeExcerpts: false).Text, Is.EqualTo("E1 signal ref1\nE2 t ref2"));
        Assert.That(JsonNode.Parse(TemplateRenderer.Render("{\"d\":\"{{json:evidence_lines}}\"}", Report, "x").Text), Is.Not.Null);
    }

    [Test]
    public void The_shipped_templates_render_with_the_example_report_without_unknown_paths()
    {
        var example = JsonNode.Parse(PackVerifier.ReadEmbeddedExample()!)!;
        foreach (var file in new[] { "email-incident.txt", "ticket-incident.json" })
        {
            var r = TemplateRenderer.Render(Fx.Support("templates/" + file), example, "http://localhost:5310/incidents/INC");
            Assert.That(r.UnknownPaths, Is.Empty, file);
        }
        var ticket = TemplateRenderer.Render(Fx.Support("templates/ticket-incident.json"), example, "http://x").Text;
        Assert.That(JsonNode.Parse(ticket)!["incident_id"], Is.Not.Null);
    }
}
