using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class StructureRuleTests : RuleTestBase
{
    // R01
    [Test] public void R01_FolderWithoutRegistryRow() { Fx.Write("docs/areas/extra/userstories.md", "# x\n"); Fails("R01", "docs/areas/extra", "no row"); }
    [Test] public void R01_RegistryRowWithoutFolder() { Fx.DeleteDir("docs/areas/tasks"); Fails("R01", "docs/areas/tasks", "does not exist"); }
    [Test] public void R01_DuplicateCode()
    {
        Fx.Edit("docs/guidelines/development.md", "| `tasks` | `TASK` | Tasks |", "| `tasks` | `TASK` | Tasks |\n| `other` | `TASK` | Other |");
        Fails("R01", "docs/areas/other", "more than one");
    }

    // R02
    [Test] public void R02_ExtraFile() { Fx.Write($"{FixtureRepo.Area}/notes.txt", "x"); Fails("R02", $"{FixtureRepo.Area}/notes.txt"); }
    [Test] public void R02_NestedFolder() { Fx.Write($"{FixtureRepo.Area}/user-stories/old/US-TASK-09.md", "# x\n"); Fails("R02", $"{FixtureRepo.Area}/user-stories/old"); }
    [Test] public void R02_NonMarkdownInTestCases() { Fx.Write($"{FixtureRepo.Area}/test-cases/x.png", "x"); Fails("R02", $"{FixtureRepo.Area}/test-cases/x.png"); }

    // R03
    [Test] public void R03_Bom() { Fx.WriteBytes("docs/overview.md", new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n' }); Fails("R03", "docs/overview.md", "byte order mark"); }
    [Test] public void R03_Crlf() { Fx.WriteBytes("docs/overview.md", "a\r\nb\r\n"u8.ToArray()); Fails("R03", "docs/overview.md", "CRLF"); }
    [Test] public void R03_ReplacementCharacter() { Fx.Write("docs/overview.md", "broken � text\n"); Fails("R03", "docs/overview.md", "U+FFFD"); }
    [Test] public void R03_InvalidUtf8() { Fx.WriteBytes("docs/overview.md", new byte[] { 0xFF, 0xFE, 0x41 }); Fails("R03", "docs/overview.md", "not valid UTF-8"); }

    [Test] public void R03_BoxDrawing() { Fx.Write("docs/overview.md", "ok\n\u2502 side\n"); Fails("R03", "docs/overview.md", "Line 2"); Fails("R03", "docs/overview.md", "box-drawing"); }
    [Test] public void R03_CheckMark() { Fx.Write("docs/overview.md", "done \u2705\n"); Fails("R03", "docs/overview.md", "check or cross mark"); }
    [Test] public void R03_CrossMark() { Fx.Write("docs/overview.md", "no \u274C\n"); Fails("R03", "docs/overview.md", "check or cross mark"); }
    [Test] public void R03_ArrowCharacter() { Fx.Write("docs/overview.md", "a \u2192 b\n"); Fails("R03", "docs/overview.md", "write ->"); }
    [Test] public void R03_AsciiArrowAndItalianPass() { Fx.Write("docs/overview.md", "a -> b, perch\u00E9 attivit\u00E0\n"); Passes("R03"); }

    // R04
    [Test] public void R04_MissingHeading() { Fx.Edit(FixtureRepo.StoryPath, "## Non-Goals", "## Out of scope"); Fails("R04", FixtureRepo.StoryPath, "Missing heading"); }
    [Test] public void R04_ExtraHeading() { Fx.Edit(FixtureRepo.StoryPath, "## Non-Goals", "## Implementation References\n\nx\n\n## Non-Goals"); Fails("R04", FixtureRepo.StoryPath, "Unexpected heading"); }
    [Test] public void R04_WrongOrder()
    {
        Fx.Write($"{FixtureRepo.Area}/testsuite.md", "# Tasks - Test Suite\n\n## Seed Data\n\nNone.\n\n## Coverage Matrix\n\n| Test Case | Title | User Story | Type | Priority |\n|-----------|-------|------------|------|----------|\n");
        Fails("R04", $"{FixtureRepo.Area}/testsuite.md");
    }
    [Test] public void R04_IndexMissing() { Fx.Delete($"{FixtureRepo.Area}/userstories.md"); Fails("R04", $"{FixtureRepo.Area}/userstories.md", "missing"); }
    [Test] public void R04_RoadmapWithoutFeatures() { Fx.Write("docs/roadmap.md", "# Roadmap\n\n## Plans\n"); Fails("R04", "docs/roadmap.md", "Features"); }
    [Test] public void R04_IndexH1UsesWrongTitle() { Fx.Edit($"{FixtureRepo.Area}/userstories.md", "# Tasks - User Stories", "# Chores - User Stories"); Fails("R04", $"{FixtureRepo.Area}/userstories.md", "H1"); }
    [Test] public void R04_OptionalTraceSectionIsAccepted() { Passes("R04"); }

    // R05
    [Test] public void R05_H1DiffersFromFileName() { Fx.Edit(FixtureRepo.StoryPath, "# US-TASK-01:", "# US-TASK-02:"); Fails("R05", FixtureRepo.StoryPath, "differs"); }
    [Test] public void R05_WrongAreaCode()
    {
        var text = Fx.Read(FixtureRepo.StoryPath).Replace("US-TASK-01", "US-ORG-01");
        Fx.Write($"{FixtureRepo.Area}/user-stories/US-ORG-01.md", text);
        Fails("R05", $"{FixtureRepo.Area}/user-stories/US-ORG-01.md", "area code");
    }
    [Test] public void R05_BadGrammar()
    {
        var path = FixtureRepo.TestCasePath("TASK-CREATE-005");
        Fx.Edit(path, "# TASK-CREATE-005:", "# TASK-CREATE-5:");
        Fails("R05", path);
    }
    [Test] public void R05_NoIdInH1() { Fx.Edit(FixtureRepo.StoryPath, "# US-TASK-01: Create a task", "# Create a task"); Fails("R05", FixtureRepo.StoryPath, "does not start with an id"); }
}
