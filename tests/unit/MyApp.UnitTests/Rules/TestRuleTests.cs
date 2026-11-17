using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class TestRuleTests : RuleTestBase
{
    private static string E2E(string n) => FixtureRepo.E2EPath("TASK-CREATE-" + n);
    private static string Tc(string n) => FixtureRepo.TestCasePath("TASK-CREATE-" + n);
    private const string Integration = "tests/integration/MyApp.IntegrationTests/TaskCreateApiTests.cs";
    private const string Capture = "tests/e2e/MyApp.E2E/Manual/Tasks/TasksCapture.cs";
    private const string Unit = "tests/unit/MyApp.UnitTests/TaskValidatorTests.cs";
    private const string Monitor = "tests/support/MyApp.Monitor.IntegrationTests/Unit/ParserTests.cs";
    private static string MonitorClass(string attribute) => $"using NUnit.Framework;\n[TestFixture]\n{attribute}\npublic class ParserTests {{ [Test] public void T() {{ }} }}\n";

    // R16
    [Test] public void R16_UnknownTestCase() { Fx.Edit(E2E("001"), "\"TASK-CREATE-001\"", "\"TASK-CREATE-099\""); Fails("R16", E2E("001"), "TASK-CREATE-099"); }
    [Test] public void R16_UnknownStory() { Fx.Edit(Integration, "\"US-TASK-01\"", "\"US-TASK-77\""); Fails("R16", Integration, "US-TASK-77"); }
    [Test] public void R16_E2EStoryDiffersFromTestCaseStory()
    {
        Fx.Write($"{FixtureRepo.Area}/user-stories/US-TASK-02.md", Fx.Read(FixtureRepo.StoryPath).Replace("US-TASK-01", "US-TASK-02"));
        Fx.Edit(E2E("001"), "\"US-TASK-01\"", "\"US-TASK-02\"");
        Fails("R16", E2E("001"), "differs");
    }
    [Test] public void R16_TagOnMethod() { Fx.Edit(Integration, "    [Test]", "    [Property(\"UserStory\", \"US-TASK-01\")]\n    [Test]"); Fails("R16", Integration, "method"); }
    [Test] public void R16_CategoryOnMethod() { Fx.Edit(Integration, "    [Test]", "    [Test, Category(\"tasks\")]"); Fails("R16", Integration, "method"); }
    [Test] public void R16_SelectionCategoryOnMethodIsNotATag() { Fx.Edit(Integration, "    [Test]", "    [Test, Category(\"Smoke\")]"); Passes("R16"); }
    [Test] public void R16_ScansMonitorTests() { Fx.Write(Monitor, "using NUnit.Framework;\n[TestFixture]\n[Property(\"UserStory\", \"US-TASK-77\")]\npublic class ParserTests { [Test] public void T() { } }\n"); Fails("R16", Monitor, "US-TASK-77"); }
    [Test] public void R16_IntegrationMayTagSeveralTestCases() { Fx.Edit(Integration, "[Property(\"TestCase\", \"TASK-CREATE-005\")]", "[Property(\"TestCase\", \"TASK-CREATE-005\")]\n[Property(\"TestCase\", \"TASK-CREATE-004\")]"); Passes("R16"); }

    // R17
    [Test] public void R17_E2EWithoutCategory() { Fx.Edit(E2E("001"), "[Category(\"tasks\")]\n", ""); Fails("R17", E2E("001"), "Category"); }
    [Test] public void R17_E2ETwoTestCases() { Fx.Edit(E2E("001"), "[Property(\"TestCase\", \"TASK-CREATE-001\")]", "[Property(\"TestCase\", \"TASK-CREATE-001\")]\n[Property(\"TestCase\", \"TASK-CREATE-002\")]"); Fails("R17", E2E("001"), "exactly one TestCase"); }
    [Test] public void R17_E2EWithoutStory() { Fx.Edit(E2E("001"), "[Property(\"UserStory\", \"US-TASK-01\")]\n", ""); Fails("R17", E2E("001"), "exactly one UserStory"); }
    [Test] public void R17_E2EClassNameWrong() { Fx.Edit(E2E("001"), "TaskCreate001Tests :", "CreateTests :"); Fails("R17", E2E("001"), "class name"); }
    [Test] public void R17_E2EFolderWrong()
    {
        var text = Fx.Read(E2E("001"));
        Fx.Delete(E2E("001"));
        Fx.Write("tests/e2e/MyApp.E2E/Tests/Org/TaskCreate001Tests.cs", text);
        Fails("R17", "tests/e2e/MyApp.E2E/Tests/Org/TaskCreate001Tests.cs", "folder must be Tasks");
    }
    [Test] public void R17_E2EFileNameWrong()
    {
        var text = Fx.Read(E2E("001"));
        Fx.Delete(E2E("001"));
        Fx.Write("tests/e2e/MyApp.E2E/Tests/Tasks/Create.cs", text);
        Fails("R17", "tests/e2e/MyApp.E2E/Tests/Tasks/Create.cs", "file name");
    }
    [Test] public void R17_TwoE2EClassesForOneTestCase() { Fx.Write("tests/e2e/MyApp.E2E/Tests/Tasks/Extra.cs", Fx.Read(E2E("001")).Replace("TaskCreate001Tests", "Extra")); Fails("R17", E2E("001"), "one class per test case"); }
    [Test] public void R17_CategoryNotAnAreaFolder() { Fx.Edit(E2E("001"), "[Category(\"tasks\")]", "[Category(\"tsks\")]"); Fails("R17", E2E("001"), "area folder"); }
    [Test] public void R17_E2ESelectionCategoryBesidesTheAreaPasses() { Fx.Edit(E2E("001"), "[Category(\"tasks\")]", "[Category(\"tasks\")]\n[Category(\"Smoke\")]"); Passes("R17"); }
    [Test] public void R17_E2ETwoAreaCategories()
    {
        Fx.Write("docs/areas/other/userstories.md", "# x\n");
        Fx.Edit(E2E("001"), "[Category(\"tasks\")]", "[Category(\"tasks\")]\n[Category(\"other\")]");
        Fails("R17", E2E("001"), "exactly one Category");
    }
    [Test] public void R17_E2ECategoryOfAnotherArea()
    {
        Fx.Write("docs/areas/other/userstories.md", "# x\n");
        Fx.Edit(E2E("001"), "[Category(\"tasks\")]", "[Category(\"other\")]");
        Fails("R17", E2E("001"), "must be \"tasks\"");
    }
    [Test] public void R17_UnitMayHaveAnyCategory() { Fx.Edit(Unit, "[TestFixture]", "[TestFixture]\n[Category(\"Backend\")]"); Passes("R17"); }
    [Test] public void R17_MonitorWithoutTagsPasses() { Fx.Write(Monitor, MonitorClass("[Category(\"Slow\")]")); Passes("R17"); }
    [Test] public void R17_MonitorWithStoryTag() { Fx.Write(Monitor, MonitorClass("[Property(\"UserStory\", \"US-TASK-01\")]")); Fails("R17", Monitor, "monitor tests"); }
    [Test] public void R17_MonitorWithAreaCategory() { Fx.Write(Monitor, MonitorClass("[Category(\"tasks\")]")); Fails("R17", Monitor, "monitor tests"); }
    [Test] public void R17_ClassesInFixturesAreNotTests() { Fx.Write("tests/support/MyApp.Monitor.IntegrationTests/Fixtures/repo/Sample.cs", MonitorClass("[Property(\"UserStory\", \"US-TASK-01\")]")); Passes("R17"); }
    [Test] public void R17_IntegrationWithoutStory() { Fx.Edit(Integration, "[Property(\"UserStory\", \"US-TASK-01\")]\n", ""); Fails("R17", Integration, "UserStory"); }
    [Test] public void R17_IntegrationWithoutCategory() { Fx.Edit(Integration, "[Category(\"tasks\")]\n", ""); Fails("R17", Integration, "Category"); }
    [Test] public void R17_CaptureWithTestCase() { Fx.Edit(Capture, "[Property(\"UserStory\", \"US-TASK-01\")]", "[Property(\"UserStory\", \"US-TASK-01\")]\n[Property(\"TestCase\", \"TASK-CREATE-001\")]"); Fails("R17", Capture, "TestCase"); }
    [Test] public void R17_CaptureWithoutManualCategory() { Fx.Edit(Capture, "[Category(\"manual-capture\")]\n", ""); Fails("R17", Capture, "manual-capture"); }
    [Test] public void R17_CaptureWithoutAreaCategory() { Fx.Edit(Capture, "[Category(\"tasks\")]\n", ""); Fails("R17", Capture, "area folder"); }
    [Test] public void R17_UnitWithTestCaseTag() { Fx.Edit(Unit, "[Property(\"UserStory\", \"US-TASK-01\")]", "[Property(\"TestCase\", \"TASK-CREATE-001\")]"); Fails("R17", Unit, "unit tests must not"); }
    [Test] public void R17_UnitWithoutAnyTagPasses() { Fx.Edit(Unit, "[Property(\"UserStory\", \"US-TASK-01\")]\n", ""); Passes("R17"); }
    [Test] public void R17_ManualCaptureCategoryOnE2E() { Fx.Edit(E2E("001"), "[Category(\"tasks\")]", "[Category(\"tasks\")]\n[Category(\"manual-capture\")]"); Fails("R17", E2E("001"), "only for capture classes"); }

    // R18
    [Test] public void R18_MissingItem() { Fx.Edit(Tc("005"), "e2e/TaskCreate005Tests, integration/TaskCreateApiTests", "e2e/TaskCreate005Tests"); Fails("R18", Tc("005"), "derive"); }
    [Test] public void R18_WrongOrder() { Fx.Edit(Tc("005"), "e2e/TaskCreate005Tests, integration/TaskCreateApiTests", "integration/TaskCreateApiTests, e2e/TaskCreate005Tests"); Fails("R18", Tc("005"), "derive"); }
    [Test] public void R18_NoneWhenNoTestExists()
    {
        Fx.Delete(E2E("001"));
        Fx.SetAutomatedBy();
        StringAssert.Contains("| Automated by | None |", Fx.Read(Tc("001")));
        Passes("R18");
    }
    [Test] public void R18_OrdinalOrderWithinLevel()
    {
        Fx.Write("tests/e2e/MyApp.E2E/Tests/Tasks/Other.cs", "using NUnit.Framework;\n[TestFixture]\n[Property(\"TestCase\", \"TASK-CREATE-001\")]\npublic class ZetaTests { [Test] public void T() { } }\n");
        Fx.SetAutomatedBy();
        StringAssert.Contains("| Automated by | e2e/TaskCreate001Tests, e2e/ZetaTests |", Fx.Read(Tc("001")));
        Passes("R18");
    }

    // R19
    [Test] public void R19_DoneFeatureWithoutE2E()
    {
        Fx.Delete(E2E("001"));
        Fx.SetAutomatedBy();
        Fails("R19", Tc("001"), "e2e/");
    }
    [Test] public void R19_IntegrationOnlyIsNotEnough()
    {
        Fx.Delete(E2E("005"));
        Fx.SetAutomatedBy();
        Fails("R19", Tc("005"), "e2e/");
    }
    [Test] public void R19_FeatureNotDoneNeedsNoE2E()
    {
        Fx.Write("docs/roadmap.md", FixtureRepo.RoadmapMd("In Progress"));
        Fx.Delete(E2E("001"));
        Fx.SetAutomatedBy();
        Passes("R19");
    }

    // R20
    [Test] public void R20_UsesHttpClient() { Fx.Edit(E2E("001"), "var page =", "using var client = new HttpClient();\n        var page ="); Fails("R20", E2E("001"), "HttpClient"); }
    [Test] public void R20_UsesApiRequest() { Fx.Edit(E2E("001"), "var page =", "var r = await Playwright.APIRequest.NewContextAsync();\n        var page ="); Fails("R20", E2E("001"), "APIRequest"); }
    [Test] public void R20_UsesLocatorDirectly() { Fx.Edit(E2E("001"), "await page.OpenAsync();", "await page.OpenAsync();\n        await Page.GetByRole(AriaRole.Button).ClickAsync();"); Fails("R20", E2E("001"), "locator"); }
    [Test] public void R20_UsesDatabase() { Fx.Edit(E2E("001"), "var page =", "var db = new SqlConnection(\"x\");\n        var page ="); Fails("R20", E2E("001"), "database"); }
    [Test] public void R20_NoPageObject() { Fx.Edit(E2E("001"), "var page = new TasksPage(Page);\n        await page.OpenAsync();", "await Page.GotoAsync(\"/\");"); Fails("R20", E2E("001"), "no page object"); }
    [Test] public void R20_ForbiddenNamesInsideStringsAreIgnored() { Fx.Edit(E2E("001"), "await page.OpenAsync();", "await page.OpenAsync(); var s = \"HttpClient .GetByRole(\";"); Passes("R20"); }
    [Test] public void R20_PlaywrightIPageIsNotAPageObject() { Fx.Edit(E2E("001"), "var page = new TasksPage(Page);\n        await page.OpenAsync();", "IPage p = Page;"); Fails("R20", E2E("001"), "no page object"); }

    // R21
    [Test] public void R21_CaptureWithTestCase() { Fx.Edit(Capture, "[Property(\"UserStory\", \"US-TASK-01\")]", "[Property(\"UserStory\", \"US-TASK-01\")]\n[Property(\"TestCase\", \"TASK-CREATE-001\")]"); Fails("R21", Capture, "TestCase"); }
    [Test] public void R21_CaptureWithoutCategory() { Fx.Edit(Capture, "[Category(\"manual-capture\")]\n", ""); Fails("R21", Capture, "manual-capture"); }
    [Test] public void R21_CaptureWithoutPageObject() { Fx.Edit(Capture, "var page = new TasksPage(Page);\n        await page.OpenAsync();", "await Page.GotoAsync(\"/\");"); Fails("R21", Capture, "page object"); }
}
