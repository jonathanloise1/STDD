using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class IndexRuleTests : RuleTestBase
{
    private static string Idx => $"{FixtureRepo.Area}/userstories.md";
    private static string Suite => $"{FixtureRepo.Area}/testsuite.md";
    private static string Tc(string n) => FixtureRepo.TestCasePath("TASK-CREATE-" + n);

    // R09
    [Test] public void R09_StoryWithoutRow() { Fx.Edit(Idx, "| [US-TASK-01](user-stories/US-TASK-01.md) | Create a task | Critical |\n", ""); Fails("R09", Idx, "no row"); }
    [Test] public void R09_RowWithoutStoryFile() { Fx.Delete(FixtureRepo.StoryPath); Fails("R09", Idx, "no such story file"); }
    [Test] public void R09_TitleMismatch() { Fx.Edit(Idx, "| Create a task | Critical |", "| Create tasks | Critical |"); Fails("R09", Idx, "title"); }
    [Test] public void R09_PriorityMismatch() { Fx.Edit(Idx, "| Create a task | Critical |", "| Create a task | Low |"); Fails("R09", Idx, "priority"); }
    [Test] public void R09_BrokenLink() { Fx.Edit(Idx, "Team members keep", "See [the guide](missing.md). Team members keep"); Fails("R09", Idx, "does not resolve"); }
    [Test] public void R09_ExternalLinkIsNotChecked() { Fx.Edit(Idx, "Team members keep", "See [site](https://example.org/x). Team members keep"); Passes("R09"); }
    [Test] public void R09_TestCaseWithoutMatrixRow()
    {
        Fx.Write(FixtureRepo.TestCasePath("TASK-CREATE-006"), FixtureRepo.CaseMd("TASK-CREATE-006", "UX", "Low", "The form shows a hint", "None", false));
        Fails("R09", Suite, "no row");
    }
    [Test] public void R09_MatrixTypeMismatch() { Fx.Edit(Suite, "| US-TASK-01 | Security |", "| US-TASK-01 | UX |"); Fails("R09", Suite, "type"); }
    [Test] public void R09_MatrixRowForMissingFile() { Fx.Delete(Tc("003")); Fails("R09", Suite, "no such test case file"); }
    [Test] public void R09_MatrixOrder()
    {
        var lines = Fx.Read(Suite).Split('\n').ToList();
        var a = lines.FindIndex(l => l.Contains("[TASK-CREATE-001]")); var b = lines.FindIndex(l => l.Contains("[TASK-CREATE-002]"));
        (lines[a], lines[b]) = (lines[b], lines[a]);
        Fx.Write(Suite, string.Join("\n", lines));
        Fails("R09", Suite, "not ordered");
    }
    [Test] public void R09_FilesTableWithoutOperations() { Fx.Edit(Idx, "| [operations.md](operations.md) | Runbooks |\n", ""); Fails("R09", Idx, "operations.md"); }
    [Test] public void R09_FilesTableWithoutOperationsIsFineWhenFileAbsent()
    {
        Fx.Edit(Idx, "| [operations.md](operations.md) | Runbooks |\n", "");
        Fx.Delete($"{FixtureRepo.Area}/operations.md");
        Passes("R09");
    }
    [Test] public void R09_FilesTableListsMissingOperations() { Fx.Delete($"{FixtureRepo.Area}/operations.md"); Fails("R09", Idx, "does not exist"); }

    // R10
    [Test] public void R10_UnknownStory() { Fx.Edit(Tc("001"), "| User Story | US-TASK-01 |", "| User Story | US-TASK-09 |"); Fails("R10", Tc("001"), "US-TASK-09"); }
    [Test] public void R10_StoryOfAnotherAreaIsNotEnough()
    {
        Fx.Write("docs/areas/other/user-stories/US-OTHR-01.md", "# US-OTHR-01: x\n");
        Fx.Edit(Tc("001"), "| User Story | US-TASK-01 |", "| User Story | US-OTHR-01 |");
        Fails("R10", Tc("001"));
    }

    // R11
    [Test] public void R11_NoHappyPath() { Fx.Delete(Tc("001")); Fails("R11", FixtureRepo.StoryPath, "Happy path"); }
    [Test] public void R11_NoValidation() { Fx.Delete(Tc("002")); Fails("R11", FixtureRepo.StoryPath, "Validation"); }
    [Test] public void R11_NoBusinessRule() { Fx.Delete(Tc("003")); Fails("R11", FixtureRepo.StoryPath, "Business rule"); }
    [Test] public void R11_NoSecurity() { Fx.Delete(Tc("004")); Fails("R11", FixtureRepo.StoryPath, "Security"); }
    [Test] public void R11_LeavesTheErrorStatesRowToR12() { Fx.Delete(Tc("005")); Passes("R11"); }
    [Test] public void R11_ValidationNotNeededWithoutUserInput()
    {
        Fx.Edit(FixtureRepo.StoryPath, "| User input | Yes |", "| User input | No |");
        Fx.Delete(Tc("002"));
        Passes("R11");
    }
    [Test] public void R11_NoneSectionsNeedNoCases()
    {
        Fx.Edit(FixtureRepo.StoryPath, "1. A title made only of spaces counts as empty.\n2. A new task always starts with status \"To do\".", "None.");
        Fx.Delete(Tc("003"));
        Passes("R11");
    }

    // R12
    [Test] public void R12_RowWithoutTestCase() { Fx.Delete(Tc("005")); Fails("R12", FixtureRepo.StoryPath, "You no longer have access"); }
    [Test] public void R12_MessageDiffers() { Fx.Edit(FixtureRepo.StoryPath, "\"You no longer have access to this organization.\"", "\"Access was removed.\""); Fails("R12", FixtureRepo.StoryPath, "Access was removed."); }
    [Test] public void R12_NoErrorStatesPasses()
    {
        Fx.Edit(FixtureRepo.StoryPath, "| Situation | Message | What the user can do |\n|-----------|---------|----------------------|\n| The user was removed from the organization while the form was open | \"You no longer have access to this organization.\" | Ask an admin to add them back |", "None.");
        Passes("R12");
    }
}
