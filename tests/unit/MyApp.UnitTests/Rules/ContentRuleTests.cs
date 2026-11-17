using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class ContentRuleTests : RuleTestBase
{
    private static string Idx => $"{FixtureRepo.Area}/userstories.md";
    private static string Tc(string n) => FixtureRepo.TestCasePath("TASK-CREATE-" + n);

    // R06
    [Test] public void R06_PriorityOutsideVocabulary() { Fx.Edit(FixtureRepo.StoryPath, "| Priority | Critical |", "| Priority | Urgent |"); Fails("R06", FixtureRepo.StoryPath, "Urgent"); }
    [Test] public void R06_FieldsOutOfOrder() { Fx.Edit(FixtureRepo.StoryPath, "| Priority | Critical |\n| Feature | F-TASK-01 |", "| Feature | F-TASK-01 |\n| Priority | Critical |"); Fails("R06", FixtureRepo.StoryPath, "exactly the fields"); }
    [Test] public void R06_FeatureNotInRoadmap() { Fx.Edit(FixtureRepo.StoryPath, "F-TASK-01", "F-TASK-09"); Fails("R06", FixtureRepo.StoryPath, "not in the roadmap"); }
    [Test] public void R06_RolesOutOfRegistryOrder() { Fx.Edit(FixtureRepo.StoryPath, "| Admin | Yes |\n| Editor | Yes |", "| Editor | Yes |\n| Admin | Yes |"); Fails("R06", FixtureRepo.StoryPath, "registry order"); }
    [Test] public void R06_UnknownRole() { Fx.Edit(FixtureRepo.StoryPath, "| Viewer | No |", "| Guest | No |"); Fails("R06", FixtureRepo.StoryPath, "Guest"); }
    [Test] public void R06_StoryAllowsRoleThatIndexForbids() { Fx.Edit(Idx, "| Admin | Yes |", "| Admin | No |"); Fails("R06", FixtureRepo.StoryPath, "area index"); }
    [Test] public void R06_TestCaseTypeOutsideVocabulary() { Fx.Edit(Tc("001"), "| Type | Happy path |", "| Type | Smoke |"); Fails("R06", Tc("001"), "Smoke"); }
    [Test] public void R06_StepWithoutVerb() { Fx.Edit(Tc("001"), "1. Open the Tasks page.", "1. Tap the Tasks page."); Fails("R06", Tc("001"), "step verb"); }
    [Test] public void R06_SecondActorStepIsAccepted() { Fx.Edit(Tc("001"), "1. Open the Tasks page.", "1. As Admin: Open the Tasks page."); Passes("R06"); }
    [Test] public void R06_FirstPreconditionMalformed() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "The user is an editor."); Fails("R06", Tc("001"), "first precondition"); }
    [Test] public void R06_FirstPreconditionWithoutOrganization() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor."); Fails("R06", Tc("001"), "first precondition"); }
    [Test] public void R06_FirstPreconditionUnknownRole() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Owner of \"Acme\"."); Fails("R06", Tc("001"), "Owner"); }
    [Test] public void R06_FirstPreconditionPersonWithoutOrganizationPasses() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as \"New User\", who belongs to no organization."); Passes("R06"); }
    [Test] public void R06_NotSignedInPasses() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Not signed in."); Passes("R06"); }
    [Test] public void R06_SecondSessionPasses() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor of \"Acme\".\n- A second session is signed in as Admin of \"Acme\"."); Passes("R06"); }
    [Test] public void R06_SecondSessionMalformed() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor of \"Acme\".\n- A second session is open as Admin."); Fails("R06", Tc("001"), "second actor"); }
    [Test] public void R06_SecondSessionUnknownRole() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor of \"Acme\".\n- A second session is signed in as Owner of \"Acme\"."); Fails("R06", Tc("001"), "Owner"); }
    [Test] public void R06_ServiceFaultPasses() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor of \"Acme\".\n- The service does not answer when the tasks are loaded."); Passes("R06"); }
    [Test] public void R06_ServiceFaultMalformed() { Fx.Edit(Tc("001"), "Signed in as Editor of \"Acme\".", "Signed in as Editor of \"Acme\".\n- The service does not answer."); Fails("R06", Tc("001"), "service fault"); }
    [Test] public void R06_LeavesTheObservableTraceToR13() { Fx.Edit(Tc("005"), "| Severity | Warning |\n", ""); Passes("R06"); }
    [Test] public void R06_RoadmapStatusOutsideVocabulary() { Fx.Write("docs/roadmap.md", FixtureRepo.RoadmapMd("Finished")); Fails("R06", "docs/roadmap.md", "Finished"); }
    [Test] public void R06_RoadmapAreaNotAFolder() { Fx.Edit("docs/roadmap.md", "| tasks |", "| chores |"); Fails("R06", "docs/roadmap.md", "chores"); }

    // R07
    [Test] public void R07_TooManyLines() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", string.Join("\n", Enumerable.Range(0, 40).Select(i => $"- Not part {i}."))); Fails("R07", FixtureRepo.StoryPath, "limit is 80"); }
    [Test] public void R07_TooManyCriteria() { Fx.Edit(FixtureRepo.StoryPath, "5. Clicking", "6. a\n7. b\n8. c\n9. d\n5. Clicking"); Fails("R07", FixtureRepo.StoryPath, "acceptance criteria"); }
    [Test] public void R07_TooFewQuestions() { Fx.Edit(FixtureRepo.StoryPath, "3. Non vedo il pulsante per aggiungere attività: perché?\n", ""); Fails("R07", FixtureRepo.StoryPath, "\"it\""); }
    [Test] public void R07_QuestionContainsTitle() { Fx.Edit(FixtureRepo.StoryPath, "2. Why won't it let me save when the name is blank?", "2. Can I create a task without a name?"); Fails("R07", FixtureRepo.StoryPath, "story title"); }
    [Test] public void R07_ExactlyEightyLinesPasses()
    {
        var lines = Fx.Read(FixtureRepo.StoryPath).TrimEnd('\n').Split('\n').Length;
        Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", string.Join("\n", Enumerable.Range(0, 80 - lines).Select(i => $"- Not part {i}.")) + "\n- Creating several tasks at once.");
        Passes("R07");
    }

    // R08
    [Test] public void R08_Backtick() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- Creating `several` tasks."); Fails("R08", FixtureRepo.StoryPath, "backtick"); }
    [Test] public void R08_Endpoint() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- Calling POST /tasks directly."); Fails("R08", FixtureRepo.StoryPath, "endpoint"); }
    [Test] public void R08_ApiPath() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- Calling /api/tasks."); Fails("R08", FixtureRepo.StoryPath, "endpoint"); }
    [Test] public void R08_StatusCode() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- Returns 401 Unauthorized."); Fails("R08", FixtureRepo.StoryPath, "status"); }
    [Test] public void R08_TypeName() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- The CreateTaskRequest is sent."); Fails("R08", FixtureRepo.StoryPath, "CreateTaskRequest"); }
    [Test] public void R08_PlainWordsEndingLikeATypeNamePass() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- Entity, Service and Response are plain words here."); Passes("R08"); }
    [Test] public void R08_SourceFile() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- See task.cs."); Fails("R08", FixtureRepo.StoryPath, "source file"); }
    [Test] public void R08_TechnicalWord() { Fx.Edit(FixtureRepo.StoryPath, "- Creating several tasks at once.", "- The Token expires."); Fails("R08", FixtureRepo.StoryPath, "technical word"); }
    [Test] public void R08_ImplementationSection() { Fx.Edit(FixtureRepo.StoryPath, "## Non-Goals", "## Implementation References\n\nx\n\n## Non-Goals"); Fails("R08", FixtureRepo.StoryPath, "template"); }
    [Test] public void R08_BacktickInTestCaseOutsideLogKey() { Fx.Edit(Tc("001"), "- The New task form is shown.", "- The `form` is shown."); Fails("R08", Tc("001"), "backtick"); }
    [Test] public void R08_BacktickInLogKeyIsAllowed() { Passes("R08"); }
    [Test] public void R08_TechnicalWordInLogKeyAndLinkIsIgnored()
    {
        Fx.Edit(Tc("005"), "task.create.access_lost", "task.create.token");
        Passes("R08");
    }
    [Test] public void R08_AreaIndexIsChecked() { Fx.Edit(Idx, "Team members keep", "The database keeps"); Fails("R08", Idx, "database"); }
}
