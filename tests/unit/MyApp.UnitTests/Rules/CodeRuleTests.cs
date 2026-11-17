using MyApp.DocTools.Model;
using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class CodeRuleTests : RuleTestBase
{
    private const string Backend = "src/backend/MyApp.Application/TaskService.cs";
    private const string Frontend = "src/frontend/src/pages/TasksPage.tsx";
    private const string Contracts = "docs/contracts.json";

    // R22
    [Test] public void R22_UnknownStoryInSrc() { Fx.Edit(Backend, "// US-TASK-01", "// US-TASK-09"); Fails("R22", Backend, "US-TASK-09"); }
    [Test] public void R22_StoryIdInsideACodeLine() { Fx.Edit(Backend, "// US-TASK-01", "var story = \"US-TASK-01\";"); Fails("R22", Backend, "comment line"); }
    [Test] public void R22_StoryIdWithTitleInComment() { Fx.Edit(Backend, "// US-TASK-01", "// US-TASK-01 Create a task"); Fails("R22", Backend, "alone"); }
    [Test] public void R22_XmlFormIsRejected() { Fx.Edit(Backend, "// US-TASK-01", "/// <userstory ref=\"US-TASK-01\" />"); Fails("R22", Backend, "comment line"); }
    [Test] public void R22_SeveralIdsInOneCommentPass() { Fx.Edit(Backend, "// US-TASK-01", "// US-TASK-01, US-TASK-01"); Passes("R22"); }
    [Test] public void R22_JsxCommentFormPasses() { Passes("R22"); }
    [Test] public void R22_JsxCommentWithUnknownId() { Fx.Edit(Frontend, "US-TASK-01", "US-TASK-42"); Fails("R22", Frontend, "US-TASK-42"); }
    [Test] public void R22_UnknownStoryInTestsMayBeAnyForm() { Fx.Write("tests/e2e/MyApp.E2E/Tests/Tasks/Notes.cs", "// see US-TASK-55 for details\n"); Fails("R22", "tests/e2e/MyApp.E2E/Tests/Tasks/Notes.cs", "US-TASK-55"); }
    [Test] public void R22_KnownStoryInTestsMayBeAnyForm() { Fx.Write("tests/e2e/MyApp.E2E/Tests/Tasks/Notes.cs", "// see US-TASK-01 for details\n"); Passes("R22"); }
    [Test] public void R22_UnitTestsAreScanned() { Fx.Write("tests/unit/MyApp.UnitTests/Data.cs", "var id = \"US-NONE-99\";\n"); Fails("R22", "tests/unit/MyApp.UnitTests/Data.cs", "US-NONE-99"); }
    [TestCase("tests/unit/MyApp.UnitTests/Rules/SomeRuleTests.cs")]
    [TestCase("tests/unit/MyApp.UnitTests/Fixtures/Sample.cs")]
    [TestCase("tests/support/MyApp.Monitor.IntegrationTests/Unit/ParserTests.cs")]
    [TestCase("tests/support/MyApp.Monitor.IntegrationTests/Fixtures/repo/src/backend/Sample.cs")]
    [TestCase("src/frontend/scripts/help/golden.ts")]
    [TestCase("src/frontend/src/help/fixtures/sample.json")]
    [TestCase("src/frontend/src/help/bench/split.test.tsx")]
    public void R22_TestDataIsNotScanned(string path) { Fx.Write(path, "var id = \"US-NONE-99\";\n"); Passes("R22"); }
    [TestCase("\n    public void Create()", "a blank line")]
    [TestCase("    /// <summary>Doc.</summary>\n    public void Create()", "a doc comment")]
    [TestCase("    // More.\n    public void Create()", "a comment")]
    [TestCase("    return new Thing();", "a statement")]
    [TestCase("    const { t } = useTranslation();", "a destructuring")]
    [TestCase("    service.update(value);", "a call")]
    [TestCase("    using System;", "an import")]
    public void R22_CommentNotRightAboveADeclaration(string next, string _)
    {
        Fx.Edit(Backend, "// US-TASK-01\n    public void Create()", "// US-TASK-01\n" + next);
        Fails("R22", Backend, "immediately above");
    }
    [TestCase("    [HttpPost]\n    public void Create()")]
    [TestCase("    Task<int> CreateAsync();")]
    [TestCase("    const handleSubmit = async () => {")]
    [TestCase("    completeOnboarding: async () => {")]
    [TestCase("    export function TasksPage() {")]
    public void R22_CommentRightAboveADeclaration(string next)
    {
        Fx.Edit(Backend, "// US-TASK-01\n    public void Create()", "// US-TASK-01\n" + next);
        Passes("R22");
    }
    [Test] public void R22_CommentOnTheLastLine() { Fx.Write("src/backend/MyApp.Application/Last.cs", "class L { }\n// US-TASK-01"); Fails("R22", "src/backend/MyApp.Application/Last.cs", "immediately above"); }
    [Test] public void R22_XmlDocAboveTheCommentPasses() { Fx.Edit(Backend, "    // US-TASK-01", "    /// <summary>Doc.</summary>\n    // US-TASK-01"); Passes("R22"); }
    [Test] public void R22_BuildOutputIsSkipped() { Fx.Write("src/backend/MyApp.Application/obj/Generated.cs", "// US-NONE-99\n"); Passes("R22"); }

    // R23
    [Test] public void R23_InvalidJson() { Fx.Write(Contracts, "{ not json"); Fails("R23", Contracts, "not valid JSON"); }
    [Test] public void R23_FormatMissing() { Fx.Edit(Contracts, ",\n    \"baseline\": { \"version\": 1, \"spec\": \"docs/guidelines/development.md#123-baseline\" }", ""); Fails("R23", Contracts, "\"baseline\""); }
    [Test] public void R23_VersionNotAnInteger() { Fx.Edit(Contracts, "\"identifiers\": { \"version\": 1,", "\"identifiers\": { \"version\": \"1\","); Fails("R23", Contracts, "integer"); }
    [Test] public void R23_VersionIsFractional() { Fx.Edit(Contracts, "\"identifiers\": { \"version\": 1,", "\"identifiers\": { \"version\": 1.5,"); Fails("R23", Contracts, "integer"); }
    [Test] public void R23_AnchorDoesNotResolve() { Fx.Edit(Contracts, "#4-area-layout", "#4-layout-of-areas"); Fails("R23", Contracts, "4-layout-of-areas"); }
    [Test] public void R23_SpecFileMissing() { Fx.Edit(Contracts, "docs/guidelines/development.md#9-test-suite", "docs/guidelines/missing.md#9"); Fails("R23", Contracts, "does not exist"); }
    [Test] public void R23_MissingFile() { Fx.Delete(Contracts); Fails("R23", Contracts, "missing"); }
    [Test] public void R23_FormatNotInSection13()
    {
        Fx.Edit(Contracts, "\"contracts\": {", "\"contracts\": {\n    \"manual-page\": { \"version\": 2, \"spec\": \"docs/guidelines/development.md#4-area-layout\" },");
        Fails("R23", Contracts, "not in the table of section 13");
    }

    private void RegisterFormatOfAnotherGuideline(string heading, string anchor)
    {
        Fx.Write("docs/guidelines/operations.md", $"# Operations\n\n{heading}\n\nText.\n");
        Fx.Edit(Repo.DevelopmentPath, "| `baseline` | Section 12.3 |", "| `baseline` | Section 12.3 |\n| `runbook` | `operations.md` section 5.4 |");
        Fx.Edit(Contracts, "\"contracts\": {", $"\"contracts\": {{\n    \"runbook\": {{ \"version\": 1, \"spec\": \"docs/guidelines/operations.md#{anchor}\" }},");
    }
    [Test] public void R23_AnchorInAnotherGuidelineResolves() { RegisterFormatOfAnotherGuideline("## 5. Signatures and runbooks", "5-signatures-and-runbooks"); Passes("R23"); }
    [Test] public void R23_AnchorOfAHeadingWithACodeSpanResolves() { RegisterFormatOfAnotherGuideline("### 5.4 Runbook format (area `operations.md`)", "54-runbook-format-area-operationsmd"); Passes("R23"); }
    [Test] public void R23_AnchorInAnotherGuidelineDoesNotResolve() { RegisterFormatOfAnotherGuideline("## 5. Signatures and runbooks", "5-runbooks"); Fails("R23", Contracts, "5-runbooks"); }
}
