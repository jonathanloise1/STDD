using System.IO.Compression;
using System.Text.Json.Nodes;
using MyApp.Monitor.Pack;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

public class PackBuildTests
{
    private FixtureRepo _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new FixtureRepo();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    private static string Describe(PackBuildResult r) => string.Join("\n", r.Findings);

    private PackBuildResult Fails(string expectedCheck, string? messagePart = null)
    {
        var result = _repo.Build();
        Assert.That(result.Succeeded, Is.False, "the build should fail");
        Assert.That(result.Findings.Select(f => f.Check), Does.Contain(expectedCheck), Describe(result));
        if (messagePart is not null) Assert.That(result.Findings.Where(f => f.Check == expectedCheck).Select(f => f.Message), Has.Some.Contain(messagePart), Describe(result));
        Assert.That(result.PackDirectory, Is.Null, "nothing is written when a check fails");
        Assert.That(Directory.Exists(Path.Combine(_repo.Root, "out")), Is.False);
        return result;
    }

    // ---- success ----

    [Test]
    public void The_fixture_repository_builds_a_pack_that_verifies()
    {
        var result = _repo.Build();
        Assert.That(result.Succeeded, Is.True, Describe(result));
        Assert.That(result.SignatureCount, Is.EqualTo(3));
        Assert.That(result.Manifest!.PackVersion, Is.EqualTo("1.4.0+abc1234"));
        var dir = result.PackDirectory!;
        Assert.That(dir, Does.EndWith(Path.Combine("out", "onprem")));
        var files = PackFiles.Read(dir);
        Assert.That(files.Keys.OrderBy(k => k, StringComparer.Ordinal), Is.EqualTo(new[]
        {
            "constraints.json", "entities.json", "incident-report.schema.json", "manifest.json", "masking-policy.json", "profile.yaml", "protocol.md",
            "runbooks/t-2bb5dcdc1949.md", "runbooks/task-create-access-lost.md", "runbooks/task-create-unavailable.md",
            "signature.schema.json", "signatures.json", "templates/email-incident.txt", "templates/ticket-incident.json", "tools.json",
        }));
        Assert.That(PackVerifier.Verify(files, new PackVerifier.Options(new DateOnly(2026, 11, 6))).Findings, Is.Empty);
    }

    [Test]
    public void The_manifest_lists_every_file_with_its_sha256_and_the_contract_versions()
    {
        var result = _repo.Build();
        var dir = result.PackDirectory!;
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))!;
        Assert.That(manifest["schema_version"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(manifest["pack_version"]!.GetValue<string>(), Is.EqualTo("1.4.0+abc1234"));
        Assert.That(manifest["created_at"]!.GetValue<string>(), Is.EqualTo("2026-11-06T12:00:00Z"));
        Assert.That(manifest["profile"]!.GetValue<string>(), Is.EqualTo("onprem"));
        Assert.That(manifest["contracts"]!["test-case"]!.GetValue<int>(), Is.EqualTo(1));
        var listed = manifest["files"]!.AsArray();
        Assert.That(listed.Select(f => f!["path"]!.GetValue<string>()), Does.Not.Contain("manifest.json"));
        foreach (var f in listed)
        {
            var bytes = File.ReadAllBytes(Path.Combine(dir, f!["path"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar)));
            Assert.That(f["sha256"]!.GetValue<string>(), Is.EqualTo(PackFiles.Sha256Hex(bytes)));
        }
        Assert.That(listed.Select(f => f!["path"]!.GetValue<string>()), Is.Ordered.Using<string>(StringComparer.Ordinal));
    }

    [Test]
    public void The_zip_has_the_same_content_as_the_directory_and_the_documented_name()
    {
        var result = _repo.Build();
        Assert.That(Path.GetFileName(result.ZipPath), Is.EqualTo("myapp-ops-pack-1.4.0+abc1234-onprem.zip"));
        var fromDir = PackFiles.Read(result.PackDirectory!);
        var fromZip = PackFiles.Read(result.ZipPath!);
        Assert.That(fromZip.Keys.OrderBy(k => k), Is.EqualTo(fromDir.Keys.OrderBy(k => k)));
        foreach (var (path, bytes) in fromDir) Assert.That(fromZip[path], Is.EqualTo(bytes), path);
        Assert.That(PackLoader.Load(result.ZipPath!).Signatures, Has.Count.EqualTo(3));
    }

    [Test]
    public void A_build_is_deterministic_for_a_fixed_clock_and_version()
    {
        var a = _repo.Build(outDir: Path.Combine(_repo.Root, "a"), zip: false);
        var b = _repo.Build(outDir: Path.Combine(_repo.Root, "b"), zip: false);
        var fa = PackFiles.Read(a.PackDirectory!);
        var fb = PackFiles.Read(b.PackDirectory!);
        foreach (var (path, bytes) in fa) Assert.That(fb[path], Is.EqualTo(bytes), path);
    }

    [Test]
    public void Signatures_are_ordered_by_id_ordinally_and_carry_the_generated_fields()
    {
        var pack = _repo.BuildAndLoad();
        Assert.That(pack.Signatures.Select(s => s.Id), Is.EqualTo(new[] { "t-2bb5dcdc1949", "task.create.access_lost", "task.create.unavailable" }));

        var keyed = pack.FindByLogKey("task.create.access_lost")!;
        Assert.That(keyed.Kind, Is.EqualTo("keyed"));
        Assert.That(keyed.Match.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(keyed.Area, Is.EqualTo("tasks"));
        Assert.That(keyed.UserStory, Is.EqualTo("US-TASK-01"));
        Assert.That(keyed.TestCase, Is.EqualTo("TASK-CREATE-005"));
        Assert.That(keyed.Severity, Is.EqualTo("Warning"));
        Assert.That(keyed.Message, Is.EqualTo("You no longer have access to this organization."));
        Assert.That(keyed.CallSite!.File, Is.EqualTo("src/backend/MyApp.Application/Logging/TasksLog.cs"));
        Assert.That(keyed.CallSite.Method, Is.EqualTo("TaskCreateAccessLost"));
        Assert.That(keyed.CallSite.Template, Is.EqualTo("Task not created: user {UserId} has no access to organization {OrganizationId}"));
        Assert.That(keyed.CallSite.Parameters, Is.EqualTo(new[] { "UserId", "OrganizationId" }));
        Assert.That(keyed.CallSite.Line, Is.EqualTo(14));
        Assert.That(keyed.Runbook.Anchor, Is.EqualTo("task-create-access-lost"));
        Assert.That(keyed.Runbook.Path, Is.EqualTo("docs/areas/tasks/operations.md"));
        Assert.That(keyed.Runbook.Text, Does.StartWith("### Meaning"));
        Assert.That(keyed.Rate.ExpectedPerHour, Is.EqualTo(5));
        Assert.That(keyed.FingerprintBy, Is.EqualTo(new[] { "OrganizationId" }));
        Assert.That(keyed.CauseCategory, Is.EqualTo("access"));
        Assert.That(keyed.Status, Is.EqualTo("active"));
        Assert.That(keyed.FixedIn, Is.Null);
        Assert.That(keyed.Noise, Is.Null);
        Assert.That(keyed.RuleChecks.Single().Tool, Is.EqualTo("health_status"));
        Assert.That(keyed.RuleChecks.Single().Expect, Is.EqualTo("status == \"Healthy\""));

        var second = pack.FindByLogKey("task.create.unavailable")!;
        Assert.That(second.Severity, Is.EqualTo("Error"));
        Assert.That(second.FingerprintBy, Is.Empty);
        Assert.That(second.History.Single().Incident, Is.EqualTo("INC-20261001-0001"));

        var template = pack.FindByTemplateId("t-2bb5dcdc1949")!;
        Assert.That(template.Kind, Is.EqualTo("template"));
        Assert.That(template.Match.TemplateId, Is.EqualTo("t-2bb5dcdc1949"));
        Assert.That(template.Match.EventName, Is.EqualTo("platform.health.unhealthy"));
        Assert.That(template.Match.Category, Is.EqualTo("MyApp.WebApi.Health"));
        Assert.That(template.Match.ExceptionType, Is.Null);
        Assert.That(template.Match.Template, Is.EqualTo("sqlserver"));
        Assert.That(template.Area, Is.Null);
        Assert.That(template.CallSite, Is.Null);
        Assert.That(template.Message, Is.Null);
        Assert.That(template.Runbook.Path, Is.EqualTo("docs/support/unkeyed-runbooks.md"));
    }

    [Test]
    public void Every_signature_validates_against_the_normative_schema_and_serialises_with_all_fields()
    {
        var result = _repo.Build();
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(result.PackDirectory!, "signatures.json")))!;
        Assert.That(json["schema_version"]!.GetValue<int>(), Is.EqualTo(1));
        var first = json["signatures"]![0]!.AsObject();
        Assert.That(first.Select(p => p.Key), Is.EquivalentTo(new[]
        {
            "id", "kind", "match", "area", "user_story", "test_case", "severity", "message", "call_site", "runbook", "rate", "fingerprint_by",
            "cause_category", "status", "fixed_in", "noise", "rule_checks", "history",
        }));
        Assert.That(first["area"], Is.Null);
        Assert.That(first.ContainsKey("area"), Is.True, "nulls are written, never omitted");
    }

    [Test]
    public void A_profile_that_binds_fewer_tools_gives_a_reduced_catalog()
    {
        var mini = _repo.Read("docs/support/profiles/onprem.yaml");
        mini = mini.Replace("name: onprem", "name: mini");
        var start = mini.IndexOf("tool_sources:", StringComparison.Ordinal);
        var end = mini.IndexOf("analyser:", StringComparison.Ordinal);
        mini = mini[..start] + "tool_sources:\n  health_status: api\n  signature_lookup: pack\n\n" + mini[end..];
        _repo.Write("docs/support/profiles/mini.yaml", mini);
        var result = _repo.Build("mini");
        Assert.That(result.Succeeded, Is.True, Describe(result));
        var tools = JsonNode.Parse(File.ReadAllText(Path.Combine(result.PackDirectory!, "tools.json")))!["tools"]!.AsArray();
        Assert.That(tools.Select(t => t!["name"]!.GetValue<string>()), Is.EqualTo(new[] { "health_status", "signature_lookup" }));
    }

    [Test]
    public void The_default_version_is_the_project_version_plus_the_short_commit()
    {
        _repo.Write("Directory.Build.props", "<Project><PropertyGroup><Version>2.5.1</Version></PropertyGroup></Project>");
        Assert.That(PackBuilder.DefaultVersion(_repo.Root), Does.StartWith("2.5.1+"));
        using var plain = new FixtureRepo();
        Assert.That(PackBuilder.DefaultVersion(plain.Root), Does.StartWith("0.0.0+"));
    }

    [Test]
    public void An_existing_output_folder_that_is_not_a_pack_is_never_deleted()
    {
        var folder = Path.Combine(_repo.Root, "out", "onprem");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "important.txt"), "keep me");
        Assert.Throws<MonitorException>(() => _repo.Build());
        Assert.That(File.Exists(Path.Combine(folder, "important.txt")), Is.True);
    }

    [Test]
    public void Building_again_replaces_a_previous_pack()
    {
        _repo.Build();
        File.WriteAllText(Path.Combine(_repo.Root, "out", "onprem", "runbooks", "stale.md"), "old");
        var again = _repo.Build();
        Assert.That(again.Succeeded, Is.True);
        Assert.That(File.Exists(Path.Combine(again.PackDirectory!, "runbooks", "stale.md")), Is.False);
    }

    // ---- section 16 checks that belong to pack building ----

    [Test]
    public void O04_a_definition_level_that_differs_from_the_severity_fails()
    {
        _repo.Replace(FixtureRepo.Log, "Level = LogLevel.Warning", "Level = LogLevel.Error");
        Fails("O04", "differs from the Severity");
    }

    [Test]
    public void O05_a_story_comment_without_the_test_cases_story_fails()
    {
        _repo.Replace(FixtureRepo.Log, "    // US-TASK-01\n    [LoggerMessage(EventId = 1", "    // US-TASK-07\n    [LoggerMessage(EventId = 1");
        Fails("O05", "must contain US-TASK-01");
    }

    [Test]
    public void O05_a_missing_story_comment_fails()
    {
        _repo.Replace(FixtureRepo.Log, "    // US-TASK-01\n    [LoggerMessage(EventId = 1", "    [LoggerMessage(EventId = 1");
        Fails("O05", "found 'none'");
    }

    [Test]
    public void R15_a_log_key_without_a_definition_fails()
    {
        _repo.Replace(FixtureRepo.Log, "= \"task.create.unavailable\"", "= \"task.create.renamed\"");
        Fails("R15", "has no log definition");
    }

    [Test]
    public void R15_a_log_key_defined_twice_fails()
    {
        _repo.Write("src/backend/MyApp.Application/Logging/CopyLog.cs", _repo.Read(FixtureRepo.Log).Replace("TasksLog", "CopyLog"));
        Fails("R15", "is defined 2 times");
    }

    [Test]
    public void O01_a_method_name_that_is_not_the_key_in_pascal_case_fails()
    {
        _repo.Replace(FixtureRepo.Log, "TaskCreateAccessLost(this ILogger", "AccessLost(this ILogger");
        Fails("O01", "must be 'TaskCreateAccessLost'");
    }

    private const string Service = "src/backend/MyApp.Application/Services/TaskService.cs";

    private static string ServiceWith(string statement) =>
        "namespace MyApp.Application.Services;\npublic sealed class TaskService(Microsoft.Extensions.Logging.ILogger<TaskService> logger)\n{\n    public void Run(string userId)\n    {\n        "
        + statement + "\n    }\n}\n";

    [Test]
    public void O02_a_direct_call_that_logs_a_warning_or_worse_outside_the_definitions_fails()
    {
        foreach (var statement in new[]
                 {
                     "logger.LogWarning(\"Task refused for {UserId}\", userId);",
                     "logger.LogError(\"Task failed for {UserId}\", userId);",
                     "logger.LogCritical(\"Task failed for {UserId}\", userId);",
                     "logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, \"Task failed for {UserId}\", userId);",
                     "logger.Log(LogLevel.Warning, \"Task failed for {UserId}\", userId);",
                 })
        {
            _repo.Write(Service, ServiceWith(statement));
            var result = Fails("O02", "only by a definition");
            Assert.That(result.Findings.Where(f => f.Check == "O02").Select(f => f.Location), Has.Some.StartWith(Service + ":"), statement);
        }
    }

    [Test]
    public void O02_information_calls_and_definitions_in_the_logging_folder_pass()
    {
        _repo.Write(Service, ServiceWith("logger.LogInformation(\"Task run for {UserId}\", userId);\n        logger.LogDebug(\"Run {UserId}\", userId);\n        logger.Log(LogLevel.Information, \"Run {UserId}\", userId);"));
        _repo.Write("src/backend/MyApp.Application/Logging/DirectLog.cs",
            "namespace MyApp.Application.Logging;\npublic static class DirectLog\n{\n    public static void Fail(Microsoft.Extensions.Logging.ILogger logger, string userId)\n    {\n        logger.LogError(\"Failed for {UserId}\", userId);\n    }\n}\n");
        var result = _repo.Build();
        Assert.That(result.Succeeded, Is.True, Describe(result));
    }

    [Test]
    public void O03_a_call_template_with_a_personal_placeholder_fails()
    {
        _repo.Write(Service, ServiceWith("logger.LogInformation(\"Invitation sent to {Email}\", userId);"));
        var result = Fails("O03", "'{Email}'");
        Assert.That(result.Findings.Single(f => f.Check == "O03").Location, Does.StartWith(Service + ":"));
    }

    [Test]
    public void O03_a_definition_template_with_a_personal_placeholder_fails()
    {
        _repo.Replace(FixtureRepo.Log, "no access to organization {OrganizationId}", "no access to organization {OrganizationId} of {FullName}");
        Fails("O03", "'{FullName}'");
    }

    [Test]
    public void O03_identifier_placeholders_and_other_strings_pass()
    {
        _repo.Write(Service, ServiceWith("logger.LogInformation(\"Task {TaskId} run by {UserId}\", userId, userId);\n        var route = \"api/{Name}\";"));
        var result = _repo.Build();
        Assert.That(result.Succeeded, Is.True, Describe(result));
    }

    [Test]
    public void R14_an_area_with_traces_and_no_operations_file_fails()
    {
        _repo.Delete(FixtureRepo.Ops);
        Fails("R14", "no operations.md");
    }

    [Test]
    public void R13_a_runbook_link_that_is_not_the_anchor_of_the_key_fails()
    {
        _repo.Replace("docs/areas/tasks/test-cases/TASK-CREATE-005.md", "[task-create-access-lost](../operations.md#task-create-access-lost)", "[wrong](../operations.md#wrong)");
        Fails("R13", "Runbook link must be");
    }

    [Test]
    public void R13_a_log_key_that_does_not_belong_to_the_test_cases_area_and_action_fails()
    {
        _repo.Replace("docs/areas/tasks/test-cases/TASK-CREATE-005.md", "`task.create.access_lost`", "`task.edit.access_lost`");
        Fails("R13", "must start with 'task.create.'");
    }

    [Test]
    public void R13_a_log_key_used_by_two_test_cases_fails()
    {
        _repo.Write("docs/areas/tasks/test-cases/TASK-CREATE-006.md", _repo.Read("docs/areas/tasks/test-cases/TASK-CREATE-005.md").Replace("TASK-CREATE-005", "TASK-CREATE-006"));
        Fails("R13", "is also used by");
    }

    [Test]
    public void O06_a_log_key_without_a_runbook_section_fails()
    {
        var ops = _repo.Read(FixtureRepo.Ops);
        _repo.Write(FixtureRepo.Ops, ops[..ops.IndexOf("## task-create-unavailable", StringComparison.Ordinal)]);
        Fails("O06", "has no section for log key 'task.create.unavailable'");
    }

    [Test]
    public void O06_a_section_with_no_test_case_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "## task-create-unavailable", "## task-edit-gone");
        _repo.Replace(FixtureRepo.Ops, "`task.create.unavailable`", "`task.edit.gone`");
        Fails("O06");
    }

    [Test]
    public void O06_sections_out_of_order_fail()
    {
        var ops = _repo.Read(FixtureRepo.Ops);
        var i = ops.IndexOf("## task-create-unavailable", StringComparison.Ordinal);
        var head = ops[..ops.IndexOf("## task-create-access-lost", StringComparison.Ordinal)];
        _repo.Write(FixtureRepo.Ops, head + ops[i..] + "\n" + ops[head.Length..i]);
        Fails("O06", "must be ordered by log key");
    }

    [Test]
    public void O06_an_invalid_field_value_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "| Cause category | access |", "| Cause category | bad_category |");
        Fails("O06", "Cause category must be one of");
    }

    [Test]
    public void O06_a_runbook_that_links_another_test_case_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "[TASK-CREATE-005](test-cases/TASK-CREATE-005.md)", "[TASK-CREATE-008](test-cases/TASK-CREATE-008.md)");
        Fails("O06", "links test case 'TASK-CREATE-008'");
    }

    [Test]
    public void O06_a_rule_check_that_names_a_tool_outside_the_catalog_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "| health_status | - |", "| format_disk | - |");
        Fails("O06", "unknown tool 'format_disk'");
    }

    [Test]
    public void O07_a_template_heading_that_is_not_the_computed_id_fails()
    {
        _repo.Replace(FixtureRepo.Unkeyed, "## t-2bb5dcdc1949", "## t-aaaaaaaaaaaa");
        Fails("O07", "must equal the template id computed");
    }

    [Test]
    public void O07_a_missing_unkeyed_file_fails()
    {
        _repo.Delete(FixtureRepo.Unkeyed);
        Fails("O07", "not found");
    }

    [Test]
    public void O08_a_world_source_in_the_profile_fails()
    {
        _repo.Replace("docs/support/profiles/onprem.yaml", "\nsources:\n", "\nsources:\n  - id: sim\n    kind: world\n    directory: /tmp/w\n");
        Fails("O08", "world source");
    }

    [Test]
    public void O08_a_secret_literal_in_the_profile_fails()
    {
        _repo.Replace("docs/support/profiles/onprem.yaml", "suppression: []", "suppression: []\n\nperimeter_extra:\n  api_key: abc123");
        Fails("O08", "a profile never contains a secret");
    }

    [Test]
    public void O08_a_profile_tool_without_an_implementation_for_its_source_kind_fails()
    {
        _repo.Replace("docs/support/profiles/onprem.yaml", "health_status: api", "health_status: main-db");
        Fails("O08", "no implementation for source kind 'sqlserver'");
    }

    [Test]
    public void O08_an_entity_without_a_masking_section_fails()
    {
        var policy = JsonNode.Parse(_repo.Read("docs/support/masking-policy.json"))!;
        policy["entities"]!.AsObject().Remove("task");
        _repo.Write("docs/support/masking-policy.json", policy.ToJsonString());
        Fails("O08", "entity 'task' has no section in the masking policy");
    }

    [Test]
    public void O08_a_masking_section_for_an_entity_that_does_not_exist_fails()
    {
        var policy = JsonNode.Parse(_repo.Read("docs/support/masking-policy.json"))!;
        policy["entities"]!["ghost"] = new JsonObject { ["Id"] = "id" };
        _repo.Write("docs/support/masking-policy.json", policy.ToJsonString());
        Fails("O08", "unknown entity 'ghost'");
    }

    [Test]
    public void O08_a_tool_output_field_without_a_class_fails()
    {
        var policy = JsonNode.Parse(_repo.Read("docs/support/masking-policy.json"))!;
        policy["tools"]!["logs_by_correlation"]!.AsObject().Remove("records[].span_id");
        _repo.Write("docs/support/masking-policy.json", policy.ToJsonString());
        Fails("O08", "output field 'records[].span_id' of tool 'logs_by_correlation' has no class");
    }

    [Test]
    public void O08_an_entity_column_that_is_not_a_plain_identifier_fails()
    {
        var policy = JsonNode.Parse(_repo.Read("docs/support/masking-policy.json"))!;
        policy["entities"]!["task"]!.AsObject()["Title]; DROP TABLE x"] = "free_text";
        _repo.Write("docs/support/masking-policy.json", policy.ToJsonString());
        Fails("O08", "not a plain identifier");
    }

    [Test]
    public void O08_a_catalog_that_breaks_the_format_fails()
    {
        _repo.Replace("docs/support/tools.json", "\"family\": \"health\"", "\"family\": \"weather\"");
        Fails("O08", "family 'weather'");
    }

    [Test]
    public void O08_a_schema_keyword_outside_the_subset_fails()
    {
        _repo.Replace("docs/support/tools.json", "\"minLength\": 1, \"maxLength\": 100, \"description\": \"Key of the record\"", "\"minLength\": 1, \"format\": \"uuid\", \"description\": \"Key of the record\"");
        Fails("O08", "outside the schema subset");
    }

    [Test]
    public void O08_a_signature_that_violates_the_signature_schema_fails()
    {
        // the schema allows at most 10 parameters: a template with 11 placeholders cannot be catalogued
        var placeholders = string.Join(" ", Enumerable.Range(1, 11).Select(i => "{P" + i + "}"));
        _repo.Replace(FixtureRepo.Log, "Message = \"Task not created: user {UserId} has no access to organization {OrganizationId}\")]", $"Message = \"Task not created {placeholders}\")]");
        Fails("O08", "violates signature.schema.json");
    }

    [Test]
    public void O09_two_begin_markers_fail()
    {
        _repo.Replace(".github/agents/support.agent.md", "Report only", "<!-- protocol:begin -->\nReport only");
        Fails("O09", "exactly once");
    }

    [Test]
    public void O09_a_missing_markers_file_fails()
    {
        _repo.Delete(".github/agents/support.agent.md");
        Fails("O09", "not found");
    }

    [Test]
    public void O09_the_protocol_block_is_copied_without_the_markers()
    {
        var pack = _repo.BuildAndLoad();
        Assert.That(pack.ProtocolText, Does.StartWith("You investigate one incident"));
        Assert.That(pack.ProtocolText, Does.Not.Contain("protocol:"));
    }

    [Test]
    public void O10_a_noise_expiry_before_the_build_date_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "| Status | Active |\n| Fixed in | None |\n| Noise rationale | None |\n| Noise expires | None |\n\n### Meaning\n\nThe task could not be saved",
            "| Status | Noise |\n| Fixed in | None |\n| Noise rationale | Accepted for now |\n| Noise expires | 2026-11-05 |\n\n### Meaning\n\nThe task could not be saved");
        Fails("O10", "noise expired on 2026-11-05");
    }

    [Test]
    public void O10_a_noise_expiry_on_the_build_date_or_later_builds()
    {
        _repo.Replace(FixtureRepo.Ops, "| Status | Active |\n| Fixed in | None |\n| Noise rationale | None |\n| Noise expires | None |\n\n### Meaning\n\nThe task could not be saved",
            "| Status | Noise |\n| Fixed in | None |\n| Noise rationale | Accepted for now |\n| Noise expires | 2026-11-06 |\n\n### Meaning\n\nThe task could not be saved");
        var result = _repo.Build();
        Assert.That(result.Succeeded, Is.True, Describe(result));
        var pack = PackLoader.Load(result.PackDirectory!);
        Assert.That(pack.FindByLogKey("task.create.unavailable")!.Noise!.Expires, Is.EqualTo("2026-11-06"));
    }

    [Test]
    public void O10_a_template_that_uses_an_unknown_path_fails()
    {
        _repo.Replace("docs/support/templates/email-incident.txt", "{{count}}", "{{no.such.path}}");
        Fails("O10", "unknown path 'no.such.path'");
    }

    [Test]
    public void O10_a_ticket_template_that_does_not_render_to_json_fails()
    {
        _repo.Replace("docs/support/templates/ticket-incident.json", "\"incident_id\":", "incident_id:");
        Fails("O10", "not valid JSON");
    }

    [Test]
    public void C04_a_tool_lookback_beyond_the_constraint_fails()
    {
        _repo.Replace("docs/support/tools.json", "\"lookback_hours\": 72", "\"lookback_hours\": 200");
        Fails("C04", "200 hours");
    }

    [Test]
    public void C07_a_connection_string_inside_the_pack_fails()
    {
        _repo.Replace(FixtureRepo.Ops, "1. Open the trace with `logs_by_correlation` and confirm the request was refused for this organization.", "1. Connect with Server=db01;Database=x;User Id=sa;Password=hunter2 and look.");
        Fails("C07");
    }

    [Test]
    public void PACK_CONTRACT_a_repository_contract_version_this_monitor_was_not_built_for_fails()
    {
        _repo.Replace("docs/contracts.json", "\"test-case\": { \"version\": 1", "\"test-case\": { \"version\": 2");
        Fails("PACK-CONTRACT", "'test-case'");
    }

    [Test]
    public void A_missing_profile_fails_cleanly()
    {
        var result = _repo.Build("nonexistent");
        Assert.That(result.Findings.Single().Check, Is.EqualTo("PACK-FILE"));
    }

    [Test]
    public void Several_problems_are_all_reported_in_one_run()
    {
        _repo.Replace(FixtureRepo.Log, "Level = LogLevel.Warning", "Level = LogLevel.Error");
        _repo.Replace(FixtureRepo.Unkeyed, "## t-2bb5dcdc1949", "## t-aaaaaaaaaaaa");
        _repo.Replace(".github/agents/support.agent.md", "<!-- protocol:end -->", "");
        var checks = _repo.Build().Findings.Select(f => f.Check).Distinct().ToList();
        Assert.That(checks, Is.SupersetOf(new[] { "O04", "O07", "O09" }));
    }
}

public class PackVerifyAndLoadTests
{
    private FixtureRepo _repo = null!;
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new FixtureRepo();
        _dir = _repo.Build(zip: false).PackDirectory!;
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    private string F(string relative) => Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));

    private static VerifyResult Verify(string dir, bool noiseAndTemplates = true) =>
        PackVerifier.Verify(PackFiles.Read(dir), new PackVerifier.Options(new DateOnly(2026, 11, 6), noiseAndTemplates, noiseAndTemplates));

    private static void HasCheck(VerifyResult r, string check, string? part = null)
    {
        Assert.That(r.Findings.Select(f => f.Check), Does.Contain(check), string.Join("\n", r.Findings));
        if (part is not null) Assert.That(r.Findings.Select(f => f.Message), Has.Some.Contain(part), string.Join("\n", r.Findings));
        Assert.That(r.Pack, Is.Null);
    }

    private static void Rewrite(string path, Func<string, string> change) => File.WriteAllText(path, change(File.ReadAllText(path)));

    [Test]
    public void A_built_pack_verifies_and_loads_with_everything_in_it()
    {
        var pack = PackLoader.Load(_dir);
        Assert.That(pack.Version, Is.EqualTo("1.4.0+abc1234"));
        Assert.That(pack.Signatures, Has.Count.EqualTo(3));
        Assert.That(pack.Tools.Tools, Has.Count.EqualTo(5));
        Assert.That(pack.Entities.Names, Has.Count.EqualTo(4));
        Assert.That(pack.Masking.Patterns, Has.Count.EqualTo(11));
        Assert.That(pack.Constraints.Constraints, Has.Count.EqualTo(10));
        Assert.That(pack.Templates.Keys, Is.EquivalentTo(new[] { "email-incident.txt", "ticket-incident.json" }));
        Assert.That(pack.Runbooks, Has.Count.EqualTo(3));
        Assert.That(pack.GetProfile(null).Name, Is.EqualTo("onprem"));
        Assert.That(pack.FindByLogKey("task.create.access_lost"), Is.Not.Null);
    }

    [Test]
    public void A_file_changed_after_the_build_is_refused_by_its_hash()
    {
        Rewrite(F("tools.json"), t => t + " ");
        HasCheck(Verify(_dir), "PACK-HASH", "does not match its hash");
        Assert.Throws<PackException>(() => PackLoader.Load(_dir));
    }

    [Test]
    public void A_file_that_is_not_in_the_manifest_is_refused()
    {
        File.WriteAllText(F("runbooks/extra.md"), "x");
        HasCheck(Verify(_dir), "PACK-HASH", "is not listed in the manifest");
    }

    [Test]
    public void A_file_listed_in_the_manifest_but_missing_is_refused()
    {
        File.Delete(F("templates/email-incident.txt"));
        HasCheck(Verify(_dir), "PACK-HASH", "missing");
    }

    [Test]
    public void A_missing_required_file_is_refused()
    {
        File.Delete(F("signatures.json"));
        Rewrite(F("manifest.json"), t => t);
        var r = Verify(_dir);
        Assert.That(r.Findings.Select(f => f.Check), Does.Contain("PACK-FILE"));
        Assert.That(r.Pack, Is.Null);
    }

    [Test]
    public void A_missing_manifest_is_refused()
    {
        File.Delete(F("manifest.json"));
        HasCheck(Verify(_dir), "PACK-MANIFEST", "manifest.json is missing");
    }

    [Test]
    public void A_pack_built_for_another_contract_version_is_refused()
    {
        Rewrite(F("manifest.json"), t => t.Replace("\"test-case\": 1", "\"test-case\": 2"));
        HasCheck(Verify(_dir), "PACK-CONTRACT", "built for version 1");
        Assert.Throws<PackException>(() => PackLoader.Load(_dir));
    }

    [Test]
    public void A_pack_with_a_missing_contract_or_another_pack_format_is_refused()
    {
        Rewrite(F("manifest.json"), t => t.Replace("\"baseline\": 1,", ""));
        HasCheck(Verify(_dir), "PACK-CONTRACT", "'baseline' is missing");
        Rewrite(F("manifest.json"), t => t.Replace("\"schema_version\": 1,", "\"schema_version\": 2,"));
        HasCheck(Verify(_dir), "PACK-CONTRACT", "pack format 2");
    }

    private void Retarget(string file)
    {
        // rewrite the manifest hash of a file changed on purpose so that only the content check can fail
        var manifest = JsonNode.Parse(File.ReadAllText(F("manifest.json")))!;
        foreach (var f in manifest["files"]!.AsArray().Where(f => f!["path"]!.GetValue<string>() == file))
            f!["sha256"] = PackFiles.Sha256Hex(File.ReadAllBytes(F(file)));
        File.WriteAllText(F("manifest.json"), manifest.ToJsonString());
    }

    [Test]
    public void A_signature_that_violates_the_schema_is_refused_even_with_a_matching_hash()
    {
        Rewrite(F("signatures.json"), t => t.Replace("\"severity\": \"Warning\"", "\"severity\": \"Fatal\""));
        Retarget("signatures.json");
        HasCheck(Verify(_dir), "O08", "violates signature.schema.json");
    }

    [Test]
    public void Signatures_out_of_order_are_refused()
    {
        var json = JsonNode.Parse(File.ReadAllText(F("signatures.json")))!;
        var arr = json["signatures"]!.AsArray();
        var first = arr[0]!.DeepClone();
        arr.RemoveAt(0);
        arr.Add(first);
        File.WriteAllText(F("signatures.json"), json.ToJsonString());
        Retarget("signatures.json");
        HasCheck(Verify(_dir), "O06", "ordered by unique id");
    }

    [Test]
    public void A_missing_runbook_file_is_refused()
    {
        File.Delete(F("runbooks/task-create-access-lost.md"));
        var manifest = JsonNode.Parse(File.ReadAllText(F("manifest.json")))!;
        var files = manifest["files"]!.AsArray();
        foreach (var f in files.Where(f => f!["path"]!.GetValue<string>() == "runbooks/task-create-access-lost.md").ToList()) files.Remove(f);
        File.WriteAllText(F("manifest.json"), manifest.ToJsonString());
        HasCheck(Verify(_dir), "O06", "runbook file runbooks/task-create-access-lost.md is missing");
    }

    [Test]
    public void A_runbook_file_that_differs_from_the_signature_text_is_refused()
    {
        Rewrite(F("runbooks/task-create-access-lost.md"), t => t.Replace("Expected after team changes.", "Edited by hand."));
        Retarget("runbooks/task-create-access-lost.md");
        HasCheck(Verify(_dir), "O06", "runbook text differs");
    }

    [Test]
    public void A_runbook_with_an_expired_noise_date_fails_verification_but_still_loads()
    {
        Rewrite(F("runbooks/task-create-unavailable.md"), t => t
            .Replace("| Status | Active |", "| Status | Noise |")
            .Replace("| Noise rationale | None |", "| Noise rationale | Accepted |")
            .Replace("| Noise expires | None |", "| Noise expires | 2026-01-01 |"));
        Retarget("runbooks/task-create-unavailable.md");
        // the signature record still says active (text equality is checked), so only the O10 parse finding matters here
        var strict = Verify(_dir);
        Assert.That(strict.Findings.Select(f => f.Check), Does.Contain("O10"));
        var load = Verify(_dir, noiseAndTemplates: false);
        Assert.That(load.Findings.Select(f => f.Check), Does.Not.Contain("O10"));
    }

    [Test]
    public void A_template_with_an_unknown_path_fails_verification_but_a_loader_does_not_render_templates()
    {
        Rewrite(F("templates/email-incident.txt"), t => t.Replace("{{count}}", "{{missing.path}}"));
        Retarget("templates/email-incident.txt");
        HasCheck(Verify(_dir), "O10", "missing.path");
        Assert.That(Verify(_dir, noiseAndTemplates: false).Findings, Is.Empty);
    }

    [Test]
    public void A_profile_whose_name_differs_from_the_manifest_is_refused()
    {
        Rewrite(F("profile.yaml"), t => t.Replace("name: onprem", "name: other"));
        Retarget("profile.yaml");
        HasCheck(Verify(_dir), "O08", "must equal the manifest profile");
    }

    [Test]
    public void A_protocol_that_still_has_its_markers_or_is_empty_is_refused()
    {
        File.WriteAllText(F("protocol.md"), "<!-- protocol:begin -->\nx\n");
        Retarget("protocol.md");
        HasCheck(Verify(_dir), "O09", "without the markers");
        File.WriteAllText(F("protocol.md"), "  \n");
        Retarget("protocol.md");
        HasCheck(Verify(_dir), "O09", "empty");
    }

    [Test]
    public void A_pack_that_is_neither_a_directory_nor_a_zip_cannot_be_loaded() =>
        Assert.Throws<MonitorException>(() => PackLoader.Load(Path.Combine(_repo.Root, "nowhere")));

    [Test]
    public void A_tampered_zip_is_refused()
    {
        var zipPath = Path.Combine(_repo.Root, "tampered.zip");
        ZipFile.CreateFromDirectory(_dir, zipPath);
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("tools.json")!;
            entry.Delete();
            var replacement = zip.CreateEntry("tools.json");
            using var w = new StreamWriter(replacement.Open());
            w.Write("{}");
        }
        Assert.Throws<PackException>(() => PackLoader.Load(zipPath));
    }

    [Test]
    public void The_pack_holds_no_source_code_stories_or_test_case_text_beyond_the_message()
    {
        var all = string.Join("\n", PackFiles.Read(_dir).Where(f => f.Key != "manifest.json").Select(f => System.Text.Encoding.UTF8.GetString(f.Value)));
        Assert.That(all, Does.Not.Contain("LoggerMessage"));
        Assert.That(all, Does.Not.Contain("Preconditions"));
        Assert.That(all, Does.Not.Contain("Prepare the report"));
        Assert.That(all, Does.Contain("You no longer have access to this organization."));
    }
}
