using System.Security.Cryptography;
using MyApp.Application.Help;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

/// <summary>
/// The golden cases of <c>training/assistant/protocol.golden.json</c> (models.md 3.7): the prompt and the parse of the
/// C# implementation are byte-identical with training.
/// </summary>
[TestFixture]
[Property("UserStory", "US-HELP-05")]
public class AssistantProtocolTests
{
    private static JObject Golden() =>
        JObject.Parse(File.ReadAllText(Path.Combine(HelpFiles.RepoRoot(), "training", "assistant", "protocol.golden.json")));

    private static AssistantProtocol Protocol() => AssistantProtocol.FromJson(HelpFiles.Protocol().Content);

    public static IEnumerable<TestCaseData> RenderCases() =>
        Golden()["render"]!.Select(c => new TestCaseData(c).SetName("Render_" + (string)c["id"]!));

    public static IEnumerable<TestCaseData> ParseCases() =>
        Golden()["parse"]!.Select(c => new TestCaseData(c).SetName("Parse_" + (string)c["id"]!));

    public static IEnumerable<TestCaseData> SelectCases() =>
        Golden()["select"]!.Select(c => new TestCaseData(c).SetName("Select_" + (string)c["id"]!));

    [Test]
    public void GoldenFile_RecordsTheShaOfTheProtocolFileAndHasEnoughCases()
    {
        var golden = Golden();
        var bytes = File.ReadAllBytes(Path.Combine(HelpFiles.RepoRoot(), "training", "assistant", "protocol.json"));

        Assert.That((string)golden["protocol_sha256"]!, Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(bytes))));
        Assert.That(golden["render"]!.Count(), Is.GreaterThanOrEqualTo(12));
        Assert.That(golden["parse"]!.Count(), Is.GreaterThanOrEqualTo(22));
        Assert.That(golden["select"]!.Count(), Is.GreaterThanOrEqualTo(5));
    }

    [TestCaseSource(nameof(RenderCases))]
    public void Render_IsByteIdenticalWithTheGoldenCase(JToken c)
    {
        var sections = c["sections"]!.Select(s => new ProtocolSection((string)s["title"]!, (string)s["text"]!)).ToList();

        var rendered = Protocol().RenderMessages((string)c["lang"]!, (string)c["app_name"]!, sections, (string)c["question"]!);

        Assert.That(rendered.System, Is.EqualTo((string)c["expected"]!["system"]!));
        Assert.That(rendered.User, Is.EqualTo((string)c["expected"]!["user"]!));
        Assert.That(rendered.Question, Is.EqualTo((string)c["expected"]!["question"]!));
    }

    [TestCaseSource(nameof(ParseCases))]
    public void Parse_GivesTheGoldenOutcome(JToken c)
    {
        var result = Protocol().ParseAnswer((string)c["lang"]!, (string)c["output"]!, (int)c["k"]!);

        Assert.That(result.Outcome.ToString().ToLowerInvariant(), Is.EqualTo((string)c["expected"]!["outcome"]!));
        Assert.That(result.Cited, Is.EqualTo(c["expected"]!["cited"]!.Select(x => (int)x).ToList()));
    }

    [TestCaseSource(nameof(SelectCases))]
    public void Select_GivesTheGoldenSections(JToken c)
    {
        var ranked = c["ranked"]!.Select(s => (Id: (string)s["id"]!, Text: (string)s["text"]!)).ToList();
        int? max = c["max_sections"]!.Type == JTokenType.Null ? null : (int)c["max_sections"]!;

        var selected = Protocol().SelectSections(ranked, s => s.Text, max);

        Assert.That(selected.Select(s => s.Id), Is.EqualTo(c["expected"]!["selected_ids"]!.Select(x => (string)x!).ToList()));
    }

    [Test]
    public void Decoding_IsGreedyWithTwoHundredNewTokens()
    {
        var protocol = Protocol();

        Assert.That(protocol.Temperature, Is.Zero);
        Assert.That(protocol.MaxNewTokens, Is.EqualTo(200));
    }

    [Test]
    public void IncompleteDefinition_IsRejected()
    {
        Assert.Throws<FormatException>(() => AssistantProtocol.FromJson(new JObject { ["schema_version"] = 1 }));
    }
}
