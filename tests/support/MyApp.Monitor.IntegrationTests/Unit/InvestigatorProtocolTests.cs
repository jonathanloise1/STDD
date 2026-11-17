using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

/// <summary>
/// The investigator protocol in C# against <c>training/investigator/protocol.golden.json</c> (operations.md 11.3): the prompts, the parser, the
/// report validation and the grammar must be byte-identical to what the Python side trains and evaluates with.
/// </summary>
public class InvestigatorProtocolTests
{
    private static readonly string Root = Repo.Root;
    private static readonly JsonObject Golden = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "training", "investigator", "protocol.golden.json")))!;
    private static readonly Protocol Proto = Protocol.Load();
    private static readonly JsonNode ReportSchema = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "docs", "support", "incident-report.schema.json")))!;

    private static IEnumerable<JsonObject> Cases(string group) => ((JsonArray)Golden[group]!).Select(n => (JsonObject)n!);
    public static IEnumerable<TestCaseData> Ids(string group) => Cases(group).Select(c => new TestCaseData(MonitorJson.ScalarText(c["id"])).SetName(group + " " + MonitorJson.ScalarText(c["id"])));
    public static IEnumerable<TestCaseData> RenderIds() => Ids("render");
    public static IEnumerable<TestCaseData> ParseIds() => Ids("parse");
    public static IEnumerable<TestCaseData> ValidateIds() => Ids("validate");
    public static IEnumerable<TestCaseData> NormaliseIds() => Ids("normalise");
    public static IEnumerable<TestCaseData> GrammarIds() => Ids("grammar");

    private static JsonObject Case(string group, string id) => Cases(group).Single(c => MonitorJson.ScalarText(c["id"]) == id);

    [TestCaseSource(nameof(RenderIds))]
    public void Every_render_case_is_byte_identical(string id)
    {
        var c = Case("render", id);
        var input = c["input"]!;
        var expected = MonitorJson.ScalarText(c["expected"]);
        string actual;
        switch (MonitorJson.ScalarText(c["kind"]))
        {
            case "user":
                actual = Proto.RenderUser(
                    input["incident"], input["pack"],
                    ((JsonArray)input["tools"]!).Select(t => (MonitorJson.ScalarText(t!["name"])!, MonitorJson.ScalarText(t["description"])!, t["parameters"]!)),
                    ((JsonArray)input["entities"]!).Select(e => (MonitorJson.ScalarText(e!["name"])!, MonitorJson.ScalarText(e["description"])!)),
                    ((JsonArray)input["constraints"]!).Select(k => (MonitorJson.ScalarText(k!["id"])!, MonitorJson.ScalarText(k["text"])!)),
                    input["calls"]!.GetValue<int>());
                break;
            case "call": actual = Proto.RenderCall(MonitorJson.ScalarText(input["tool"])!, input["arguments"]); break;
            case "result": actual = Proto.RenderResult(input["envelope"]!, input["calls_left"]!.GetValue<int>()); break;
            case "report": actual = Proto.RenderReport(input); break;
            case "repair": actual = Proto.RenderRepair(((JsonArray)c["input"]!).Select(e => MonitorJson.ScalarText(e)!)); break;
            default: throw new AssertionException("unknown kind");
        }
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>The conversation of <c>template_cases</c> in the Python adapter: system, user, call, result, report, with the result role as the setting says.</summary>
    [TestCase("tool", "tool")]
    [TestCase("user", "user")]
    [TestCase(null, "tool")]
    public void The_wire_roles_of_the_golden_conversation_follow_the_tool_role(string? toolRole, string expectedResultRole)
    {
        string Expected(string id) => MonitorJson.ScalarText(Case("render", id)["expected"])!;
        var conversation = new List<ChatMessage>
        {
            new("system", "rules"), new("user", Expected("u01-rate-with-pack")), new("assistant", Expected("t01-call-logs")),
            new(Proto.ResultRole, Expected("t03-result-ok")), new("assistant", Expected("t05-report-explained")),
        };
        var wire = InvestigationLoop.WireView(conversation, Proto, toolRole);
        Assert.That(wire.Select(m => m.Role), Is.EqualTo(new[] { "system", "user", "assistant", expectedResultRole, "assistant" }));
        Assert.That(wire.Select(m => m.Content), Is.EqualTo(conversation.Select(m => m.Content)), "only the role changes, the bytes of the turns do not");
        Assert.That(conversation[3].Role, Is.EqualTo("tool"), "the conversation itself keeps the protocol's role");
    }

    [TestCaseSource(nameof(ParseIds))]
    public void Every_parse_case_gives_the_same_kind_and_payload(string id)
    {
        var c = Case("parse", id);
        var turn = Protocol.ParseTurn(MonitorJson.ScalarText(c["output"])!);
        var expected = (JsonObject)c["expected"]!;
        Assert.That(turn.Kind, Is.EqualTo(MonitorJson.ScalarText(expected["kind"])));
        if (expected["payload"] is null) Assert.That(turn.Payload, Is.Null);
        else Assert.That(JsonNode.DeepEquals(turn.Payload, expected["payload"]), Is.True, ProtocolJson.Dumps(turn.Payload));
    }

    [TestCaseSource(nameof(ValidateIds))]
    public void Every_validation_case_agrees_on_valid_or_not(string id)
    {
        var c = Case("validate", id);
        var validator = new SchemaSubsetValidator(Proto.InvestigationSchema(ReportSchema));
        var evidence = ((JsonArray)c["evidence_ids"]!).Select(e => MonitorJson.ScalarText(e)!).ToHashSet();
        IReadOnlySet<string>? stories = c["stories"] is JsonArray s ? s.Select(x => MonitorJson.ScalarText(x)!).ToHashSet() : null;
        var errors = Protocol.ValidateReport(validator, c["report"], evidence, stories);
        Assert.That(errors.Count == 0, Is.EqualTo(c["expected_valid"]!.GetValue<bool>()), string.Join("; ", errors));
    }

    [TestCaseSource(nameof(NormaliseIds))]
    public void Every_normalise_case_is_the_same(string id)
    {
        var c = Case("normalise", id);
        var actual = Proto.NormaliseReport((JsonObject)c["report"]!);
        Assert.That(ProtocolJson.Dumps(actual), Is.EqualTo(ProtocolJson.Dumps(c["expected"])));
    }

    [TestCaseSource(nameof(GrammarIds))]
    public void Every_grammar_case_is_the_same_text_and_is_well_formed(string id)
    {
        var c = Case("grammar", id);
        var input = (JsonObject)c["input"]!;
        var schema = Proto.InvestigationSchema(ReportSchema);
        var grammar = TurnGrammar.Build(
            ((JsonArray)input["tools"]!).Select(t => (MonitorJson.ScalarText(t!["name"])!, t["parameters"]!)), schema,
            ((JsonArray)input["entities"]!).Select(e => MonitorJson.ScalarText(e)!).ToList(), c["only_report"]!.GetValue<bool>());
        Assert.That(grammar, Is.EqualTo(MonitorJson.ScalarText(c["expected"])));
        Assert.That(TurnGrammar.UndefinedRules(grammar), Is.Empty);
    }

    [Test]
    public void The_embedded_definition_is_the_file_the_golden_cases_were_made_with()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Root, "training", "investigator", "protocol.json"));
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo(MonitorJson.ScalarText(Golden["protocol_sha256"])));
        Assert.That(Proto.Root.ToJsonString(), Is.EqualTo(JsonNode.Parse(bytes)!.ToJsonString()));
        Assert.That((Proto.BudgetSmall, Proto.BudgetLarge, Proto.BudgetCap, Proto.DuplicateLimit, Proto.ConsecutiveFailures, Proto.SummaryFraction, Proto.RepairTurns, Proto.MinConfidenceExplained),
            Is.EqualTo((6, 10, 12, 2, 2, 0.75, 1, 0.5)));
    }

    [Test]
    public void The_protocol_block_hash_is_computed_the_way_the_lock_records_it()
    {
        var agent = File.ReadAllText(Path.Combine(Root, ".github", "agents", "support.agent.md"), new UTF8Encoding(false)).Replace("\r\n", "\n");
        const string begin = "<!-- protocol:begin -->", end = "<!-- protocol:end -->";
        var block = agent[(agent.IndexOf(begin, StringComparison.Ordinal) + begin.Length)..agent.IndexOf(end, StringComparison.Ordinal)];
        Assert.That(ProtocolBlock.Sha256(block.Trim()), Is.EqualTo(MonitorJson.ScalarText(Golden["system_block_sha256"])));
        Assert.That(ProtocolBlock.SystemText(block.Trim() + "\n"), Is.EqualTo(block.Trim('\n')));
    }

    [Test]
    public void A_value_with_braces_is_substituted_once_and_not_scanned_again()
    {
        var text = Proto.RenderUser(JsonNode.Parse("""{"note":"{budget} {tools}"}"""), null, [], [], [], 3);
        Assert.That(text, Does.Contain("""<incident>{"note":"{budget} {tools}"}</incident>""").And.Contain("<pack>null</pack>").And.EndWith("<budget calls=\"3\" />"));
    }

    [TestCase(0.82, "0.82")]
    [TestCase(120.5, "120.5")]
    [TestCase(1e-05, "1e-05")]
    [TestCase(1.5e16, "1.5e+16")]
    [TestCase(0.0001, "0.0001")]
    [TestCase(123456789012345.0, "123456789012345.0")]
    public void Floats_are_written_as_python_writes_them(double value, string expected) =>
        Assert.That(ProtocolJson.PythonFloat(value), Is.EqualTo(expected));
}
