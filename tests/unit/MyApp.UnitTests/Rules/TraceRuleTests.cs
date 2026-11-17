using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class TraceRuleTests : RuleTestBase
{
    private static string Tc(string n) => FixtureRepo.TestCasePath("TASK-CREATE-" + n);
    private const string Backend = "src/backend/MyApp.Application/TaskService.cs";
    private const string LogClass = FixtureRepo.LogClassPath;
    private const string KeyLiteral = "\"task.create.access_lost\"";
    private const string Definition = "    [LoggerMessage(";
    private const string Call = "_logger.TaskCreateAccessLost(userId);";

    private void StripTrace(string path)
    {
        var text = Fx.Read(path);
        Fx.Write(path, text[..text.IndexOf("\n## Observable trace", StringComparison.Ordinal)]);
    }

    private string NewEdgeCase(string n, string key, string message = "\"You no longer have access to this organization.\"")
    {
        var path = Tc(n);
        var text = FixtureRepo.CaseMd("TASK-CREATE-" + n, "Edge case", "Low", "A limit is reached", "None", true)
            .Replace("task.create.access_lost", key)
            .Replace("task-create-access-lost", key.Replace('.', '-').Replace('_', '-'))
            .Replace("\"You no longer have access to this organization.\"", message);
        Fx.Write(path, text);
        return path;
    }

    // R13
    [Test] public void R13_ErrorCaseWithoutTrace() { StripTrace(Tc("005")); Fails("R13", Tc("005"), "requires"); }
    [Test] public void R13_HappyPathWithTrace()
    {
        Fx.Write(Tc("001"), FixtureRepo.CaseMd("TASK-CREATE-001", "Happy path", "Critical", "Create a task with a title", "None", true));
        Fails("R13", Tc("001"), "only Edge case");
    }
    [Test] public void R13_RunbookTargetWrong() { Fx.Edit(Tc("005"), "../operations.md#task-create-access-lost", "../operations.md#wrong"); Fails("R13", Tc("005"), "target"); }
    [Test] public void R13_RunbookTextWrong() { Fx.Edit(Tc("005"), "[task-create-access-lost]", "[access lost]"); Fails("R13", Tc("005"), "text"); }
    [Test] public void R13_LogKeyAreaDiffers() { Fx.Edit(Tc("005"), "`task.create.access_lost`", "`order.create.access_lost`"); Fails("R13", Tc("005"), "area"); }
    [Test] public void R13_LogKeyActionDiffers() { Fx.Edit(Tc("005"), "`task.create.access_lost`", "`task.edit.access_lost`"); Fails("R13", Tc("005"), "action"); }
    [Test] public void R13_LogKeyGrammar() { Fx.Edit(Tc("005"), "`task.create.access_lost`", "`task.create.AccessLost`"); Fails("R13", Tc("005"), "does not match"); }
    [Test] public void R13_LogKeyNotInBackticks() { Fx.Edit(Tc("005"), "`task.create.access_lost`", "task.create.access_lost"); Fails("R13", Tc("005"), "backticks"); }
    [Test] public void R13_SeverityOutsideVocabulary() { Fx.Edit(Tc("005"), "| Severity | Warning |", "| Severity | Fatal |"); Fails("R13", Tc("005"), "Fatal"); }
    [Test] public void R13_FieldMissing() { Fx.Edit(Tc("005"), "| Severity | Warning |\n", ""); Fails("R13", Tc("005"), "exactly the fields"); }
    [Test] public void R13_DuplicateLogKey()
    {
        var other = NewEdgeCase("006", "task.create.access_lost");
        Fails("R13", other, "also defined");
        Fails("R13", Tc("005"), "also defined");
    }
    [Test] public void R13_EdgeCaseMayHaveNoMessage()
    {
        NewEdgeCase("006", "task.create.slow_reply", "None");
        Passes("R13");
    }
    [Test] public void R13_ErrorHandlingMustHaveMessage() { Fx.Edit(Tc("005"), "| Message | \"You no longer have access to this organization.\" |", "| Message | None |"); Fails("R13", Tc("005"), "Message"); }

    // R14
    [Test] public void R14_RunbookHeadingMissing() { Fx.Write($"{FixtureRepo.Area}/operations.md", "# Operations\n\n## something-else\n"); Fails("R14", Tc("005"), "no heading"); }
    [Test] public void R14_OperationsFileMissing() { Fx.Delete($"{FixtureRepo.Area}/operations.md"); Fails("R14", Tc("005"), "does not exist"); }
    [Test] public void R14_HeadingWrittenAsWordsIsNotTheAnchor()
    {
        Fx.Write($"{FixtureRepo.Area}/operations.md", "# Operations\n\n## Task create access lost\n");
        Fails("R14", Tc("005"), "no heading");
    }
    [Test] public void R14_HeadingWrittenAsTheLogKeyIsNotTheAnchor()
    {
        Fx.Write($"{FixtureRepo.Area}/operations.md", "# Operations\n\n## task.create.access_lost\n");
        Fails("R14", Tc("005"), "not the log key");
    }
    [Test] public void R14_AnchorAsH3IsNotARunbook()
    {
        Fx.Write($"{FixtureRepo.Area}/operations.md", "# Operations\n\n## other\n\n### task-create-access-lost\n");
        Fails("R14", Tc("005"), "no heading");
    }

    // R15
    [Test] public void R15_NoLiteral() { Fx.Edit(LogClass, KeyLiteral, "\"other\""); Fails("R15", Tc("005"), "does not appear"); }
    [Test] public void R15_ASecondLiteral()
    {
        Fx.Write("src/backend/MyApp.Application/Other.cs", "class O { void M() { _log.LogWarning(\"task.create.access_lost\"); } }\n");
        Fails("R15", Tc("005"), "2 places");
    }
    [Test] public void R15_LiteralInACommentDoesNotCount()
    {
        Fx.Edit(Backend, Call, Call + " // " + KeyLiteral);
        Passes("R15");
    }
    [Test] public void R15_OnlyAKeyInACommentIsNotALiteral()
    {
        Fx.Edit(LogClass, "public const string TaskCreateAccessLost = " + KeyLiteral + ";", "// " + KeyLiteral);
        Fails("R15", Tc("005"), "does not appear");
    }
    [Test] public void R15_KeyInsideALongerStringDoesNotCount() { Fx.Edit(LogClass, KeyLiteral, "\"task.create.access_lost and more\""); Fails("R15", Tc("005"), "does not appear"); }
    [Test] public void R15_ConstantOutsideTheLogClassOfTheArea()
    {
        var text = Fx.Read(LogClass);
        Fx.Delete(LogClass);
        Fx.Write("src/backend/MyApp.Application/Logging/OrganizationsLog.cs", text);
        Fails("R15", Tc("005"), "belongs in src/backend/MyApp.Application/Logging/TasksLog.cs");
    }
    [Test] public void R15_LogClassNamedAfterTheAreaTitlePasses()
    {
        Fx.Edit("docs/guidelines/development.md", "| `tasks` | `TASK` | Tasks |", "| `tasks` | `TASK` | Task Board |");
        var text = Fx.Read(LogClass);
        Fx.Delete(LogClass);
        Fx.Write("src/backend/MyApp.Application/Logging/TaskBoardLog.cs", text);
        Passes("R15");
    }
    [Test] public void R15_LiteralThatIsNotAConstant()
    {
        Fx.Edit(LogClass, "public const string TaskCreateAccessLost = " + KeyLiteral, "public static string TaskCreateAccessLost => " + KeyLiteral);
        Fails("R15", Tc("005"), "must be a constant");
    }
    [Test] public void R15_ConstantWithoutMethod()
    {
        Fx.Edit(LogClass, "public static partial void TaskCreateAccessLost(this ILogger logger, Guid userId);", "");
        Fails("R15", Tc("005"), "no method");
    }
    [Test] public void R15_MethodWithoutTheEventName()
    {
        Fx.Edit(LogClass, "EventName = Keys.TaskCreateAccessLost", "EventName = Keys.Other");
        Fails("R15", Tc("005"), "EventName");
    }
    [Test] public void R15_MethodNeverCalled() { Fx.Edit(Backend, Call, ""); Fails("R15", Tc("005"), "never called"); }
    [Test] public void R15_MethodCalledTwice() { Fx.Edit(Backend, Call, Call + "\n        " + Call); Fails("R15", Tc("005"), "2 places"); }
    [Test] public void R15_ACallInACommentDoesNotCount() { Fx.Edit(Backend, Call, Call + " // " + Call); Passes("R15"); }
    [Test] public void R15_ClientReportedNeedsNoCallSite()
    {
        Fx.Edit(Backend, Call, "");
        Fx.Edit(LogClass, Definition, "    [ClientReported]\n" + Definition);
        Passes("R15");
    }
    [Test] public void R15_ClientReportedAndAlsoCalled()
    {
        Fx.Edit(LogClass, Definition, "    [ClientReported]\n" + Definition);
        Fails("R15", Tc("005"), "ClientReported");
    }
    [Test] public void R15_ReferencesToTheConstantAreNotLiterals()
    {
        Fx.Edit(Backend, Call, Call + "\n        throw new OutcomeException(TasksLog.Keys.TaskCreateAccessLost, 403, \"Message.\");");
        Passes("R15");
    }
}
