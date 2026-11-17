using System.Text;
using MyApp.DocTools;
using MyApp.DocTools.Model;
using MyApp.DocTools.Rules;

namespace MyApp.UnitTests.Fixtures;

/// <summary>
/// A small, fully valid repository in a temporary folder (one area, one story, five test cases, tests and code).
/// Rule tests mutate it to produce the failing cases. The unmodified repository must produce no findings.
/// </summary>
public sealed class FixtureRepo : IDisposable
{
    public const string Story = "US-TASK-01";
    public const string ErrorCase = "TASK-CREATE-005";
    public const string LogKey = "task.create.access_lost";
    public const string Area = "docs/areas/tasks";
    public const string LogClassPath = "src/backend/MyApp.Application/Logging/TasksLog.cs";

    public string Root { get; }

    public FixtureRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "doctools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        BuildGolden();
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }

    public Repo Open() => new(Root);

    public List<Finding> Run(string ruleId) => RuleSet.Get(ruleId).Check(Open()).Distinct().ToList();

    public string Abs(string rel) => Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
    public bool Exists(string rel) => File.Exists(Abs(rel));

    public void Write(string rel, string content)
    {
        var p = Abs(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
    }

    public void WriteBytes(string rel, byte[] bytes)
    {
        var p = Abs(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, bytes);
    }

    public string Read(string rel) => File.ReadAllText(Abs(rel));

    /// <summary>Replaces text in a file; fails the test when the text is not there.</summary>
    public void Edit(string rel, string from, string to)
    {
        var text = Read(rel);
        if (!text.Contains(from, StringComparison.Ordinal)) throw new InvalidOperationException($"Fixture edit: \"{from}\" not found in {rel}");
        Write(rel, text.Replace(from, to));
    }

    public void Delete(string rel) => File.Delete(Abs(rel));
    public void DeleteDir(string rel) => Directory.Delete(Abs(rel), true);

    public static string TestCasePath(string id) => $"{Area}/test-cases/{id}.md";
    public static string StoryPath => $"{Area}/user-stories/{Story}.md";
    public static string E2EPath(string id) => $"tests/e2e/MyApp.E2E/Tests/Tasks/{Ids.TestCasePascal(id)}Tests.cs";

    private void BuildGolden()
    {
        Write(Repo.DevelopmentPath, DevelopmentMd);
        Write(Repo.ContractsPath, ContractsJson);
        Write(Repo.RoadmapPath, RoadmapMd("Done"));
        Write($"{Area}/userstories.md", IndexMd);
        Write($"{Area}/testsuite.md", SuiteMd);
        Write($"{Area}/operations.md", "# Tasks - Operations\n\n## task-create-access-lost\n\nWhat to do when a user loses access while saving.\n");
        Write(StoryPath, StoryMd);
        Write(TestCasePath("TASK-CREATE-001"), CaseMd("TASK-CREATE-001", "Happy path", "Critical", "Create a task with a title", "None", false));
        Write(TestCasePath("TASK-CREATE-002"), CaseMd("TASK-CREATE-002", "Validation", "High", "Saving without a title shows a field message", "None", false));
        Write(TestCasePath("TASK-CREATE-003"), CaseMd("TASK-CREATE-003", "Business rule", "Medium", "A new task starts with status To do", "None", false));
        Write(TestCasePath("TASK-CREATE-004"), CaseMd("TASK-CREATE-004", "Security", "High", "A viewer cannot create tasks", "None", false));
        Write(TestCasePath(ErrorCase), CaseMd(ErrorCase, "Error handling", "High", "Saving after losing access shows a message", "None", true));
        foreach (var n in new[] { "001", "002", "003", "004", "005" })
        {
            var id = "TASK-CREATE-" + n;
            Write(E2EPath(id), E2EClass(id));
        }
        Write("tests/e2e/MyApp.E2E/Manual/Tasks/TasksCapture.cs", CaptureClass);
        Write("tests/integration/MyApp.IntegrationTests/TaskCreateApiTests.cs", IntegrationClass);
        Write("tests/unit/MyApp.UnitTests/TaskValidatorTests.cs", UnitClass);
        Write("src/backend/MyApp.Application/TaskService.cs", BackendSource);
        Write(LogClassPath, LogClassSource);
        Write("src/frontend/src/pages/TasksPage.tsx", FrontendSource);
        SetAutomatedBy();
    }

    /// <summary>Writes the derived Automated by value into every test case, as the sync command does.</summary>
    public void SetAutomatedBy()
    {
        AutomatedBy.Sync(Open());
    }

    // ---- content ----

    public static string RoadmapMd(string status) => $$"""
# Roadmap

## Features

| ID | Feature | Area | Status |
|----|---------|------|--------|
| F-TASK-01 | Create tasks | tasks | {{status}} |

""";

    public const string ContractsJson = """
{
  "description": "Versions.",
  "contracts": {
    "identifiers": { "version": 1, "spec": "docs/guidelines/development.md#3-identifiers-and-registries" },
    "roadmap-features": { "version": 1, "spec": "docs/guidelines/development.md#33-roadmap-features" },
    "area-layout": { "version": 1, "spec": "docs/guidelines/development.md#4-area-layout" },
    "area-index": { "version": 1, "spec": "docs/guidelines/development.md#5-area-index" },
    "user-story": { "version": 1, "spec": "docs/guidelines/development.md#6-user-story-file" },
    "test-case": { "version": 1, "spec": "docs/guidelines/development.md#8-test-case-file" },
    "test-suite": { "version": 1, "spec": "docs/guidelines/development.md#9-test-suite" },
    "code-tags": { "version": 1, "spec": "docs/guidelines/development.md#11-traceability-tags-in-code-and-tests" },
    "baseline": { "version": 1, "spec": "docs/guidelines/development.md#123-baseline" }
  }
}

""";

    public const string DevelopmentMd = """
# Development Guidelines

## 3. Identifiers and registries

### 3.2 Area registry

| Area folder | Area code | Title |
|-------------|-----------|-------|
| `tasks` | `TASK` | Tasks |

### 3.3 Roadmap features

Text.

### 3.4 Role registry

| Role | Meaning |
|------|---------|
| `Visitor` | Not signed in |
| `Signed-in user` | Signed in |
| `Admin` | Full access |
| `Editor` | Edits |
| `Viewer` | Reads |

### 3.5 Closed vocabularies

| Field | Values |
|-------|--------|
| Priority | `Critical`, `High`, `Medium`, `Low` |
| User input | `Yes`, `No` |
| Allowed | `Yes`, `No` |
| Type | `Happy path`, `Validation`, `Business rule`, `Edge case`, `Error handling`, `UX`, `Security` |
| Severity | `Information`, `Warning`, `Error`, `Critical` |
| Roadmap status | `Done`, `In Progress`, `Planned`, `Backlog` |
| Step verbs | `Open`, `Click`, `Type`, `Clear`, `Select`, `Check`, `Uncheck`, `Upload`, `Drag`, `Press`, `Reload`, `Sign in`, `Sign out` |
| UI element names | `page`, `form`, `dialog`, `button`, `link`, `field`, `list`, `table`, `menu`, `tab`, `message` |

## 4. Area layout

## 5. Area index

## 6. User story file

## 8. Test case file

## 9. Test suite

## 11. Traceability tags in code and tests

### 12.3 Baseline

## 13. Contract versions

| Format key | Defined in |
|------------|------------|
| `identifiers` | Section 3 |
| `roadmap-features` | Section 3.3 |
| `area-layout` | Section 4 |
| `area-index` | Section 5 |
| `user-story` | Sections 6 and 7.3 |
| `test-case` | Section 8 |
| `test-suite` | Section 9 |
| `code-tags` | Section 11 |
| `baseline` | Section 12.3 |

""";

    public const string IndexMd = """
# Tasks - User Stories

## Overview

Team members keep a shared list of tasks.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-TASK-01](user-stories/US-TASK-01.md) | Create a task | Critical |

## Business Rules

None.

## UI Flow

1. Tasks: the user sees the task list and opens the New task form.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |

""";

    public const string SuiteMd = """
# Tasks - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [TASK-CREATE-001](test-cases/TASK-CREATE-001.md) | Create a task with a title | US-TASK-01 | Happy path | Critical |
| [TASK-CREATE-002](test-cases/TASK-CREATE-002.md) | Saving without a title shows a field message | US-TASK-01 | Validation | High |
| [TASK-CREATE-003](test-cases/TASK-CREATE-003.md) | A new task starts with status To do | US-TASK-01 | Business rule | Medium |
| [TASK-CREATE-004](test-cases/TASK-CREATE-004.md) | A viewer cannot create tasks | US-TASK-01 | Security | High |
| [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) | Saving after losing access shows a message | US-TASK-01 | Error handling | High |

## Seed Data

None.

""";

    public const string StoryMd = """
# US-TASK-01: Create a task

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-TASK-01 |
| User input | Yes |

## Story

**As a** team member
**I want** to add a task with a title
**So that** the team can see what needs to be done

## Acceptance Criteria

1. On the Tasks page, the "New task" button opens the New task form.
2. The form has the fields Title and Description; only Title is required.
3. Clicking "Save" with an empty Title keeps the form open and the Title field shows "Enter a title."
4. After saving, the form closes and the task is first in the list.
5. Clicking "Cancel" closes the form and no task is added.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

1. A title made only of spaces counts as empty.
2. A new task always starts with status "To do".

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The user was removed from the organization while the form was open | "You no longer have access to this organization." | Ask an admin to add them back |

## Non-Goals

- Creating several tasks at once.

## Questions users ask

### en

1. How do I add something new to the team's to-do list?
2. Why won't it let me save when the name is blank?
3. I can't find the button to add work. What am I missing?

### it

1. Come inserisco un nuovo lavoro da fare per il team?
2. Perché non salva se lascio vuoto il nome?
3. Non vedo il pulsante per aggiungere attività: perché?

""";

    public static string CaseMd(string id, string type, string priority, string title, string automated, bool trace)
    {
        var testData = type == "Validation"
            ? "| Field | Value |\n|-------|-------|\n| Title | \"   \" |"
            : "None.";
        var sb = new StringBuilder();
        sb.Append($"# {id}: {title}\n\n| Field | Value |\n|-------|-------|\n| User Story | {Story} |\n| Type | {type} |\n| Priority | {priority} |\n| Automated by | {automated} |\n\n");
        sb.Append("## Description\n\nThe behaviour this test case verifies.\n\n");
        sb.Append("## Preconditions\n\n- Signed in as Editor of \"Acme\".\n\n");
        sb.Append($"## Test Data\n\n{testData}\n\n");
        sb.Append("## Steps\n\n1. Open the Tasks page.\n2. Click the \"New task\" button.\n\n");
        sb.Append("## Expected Results\n\n- The New task form is shown.\n");
        if (trace)
            sb.Append("\n## Observable trace\n\n| Field | Value |\n|-------|-------|\n| Message | \"You no longer have access to this organization.\" |\n| Log key | `task.create.access_lost` |\n| Severity | Warning |\n| Runbook | [task-create-access-lost](../operations.md#task-create-access-lost) |\n");
        return sb.ToString();
    }

    public static string E2EClass(string id) => $$"""
using NUnit.Framework;

namespace MyApp.E2E.Tests.Tasks;

[TestFixture]
[Category("tasks")]
[Property("TestCase", "{{id}}")]
[Property("UserStory", "US-TASK-01")]
public class {{Ids.TestCasePascal(id)}}Tests : AuthenticatedTestBase
{
    [Test]
    public async Task Runs()
    {
        var page = new TasksPage(Page);
        await page.OpenAsync();
    }
}

""";

    public const string CaptureClass = """
using NUnit.Framework;

namespace MyApp.E2E.Manual.Tasks;

[TestFixture]
[Category("tasks")]
[Category("manual-capture")]
[Property("UserStory", "US-TASK-01")]
public class TasksCapture : AuthenticatedTestBase
{
    [Test]
    public async Task Capture()
    {
        var page = new TasksPage(Page);
        await page.OpenAsync();
    }
}

""";

    public const string IntegrationClass = """
using NUnit.Framework;

namespace MyApp.IntegrationTests;

[TestFixture]
[Category("tasks")]
[Property("UserStory", "US-TASK-01")]
[Property("TestCase", "TASK-CREATE-005")]
public class TaskCreateApiTests
{
    [Test]
    public void Rejects() { }
}

""";

    public const string UnitClass = """
using NUnit.Framework;

namespace MyApp.UnitTests;

[TestFixture]
[Property("UserStory", "US-TASK-01")]
public class TaskValidatorTests
{
    [Test]
    public void Works() { }
}

""";

    public const string BackendSource = """
namespace MyApp.Application;

public class TaskService
{
    // US-TASK-01
    public void Create()
    {
        _logger.TaskCreateAccessLost(userId);
    }
}

""";

    public const string LogClassSource = """
namespace MyApp.Application.Logging;

public static partial class TasksLog
{
    public static class Keys
    {
        public const string TaskCreateAccessLost = "task.create.access_lost";
    }

    // US-TASK-01
    [LoggerMessage(EventId = 1, EventName = Keys.TaskCreateAccessLost, Level = LogLevel.Warning,
        Message = "Task not created: user {UserId} has no access")]
    public static partial void TaskCreateAccessLost(this ILogger logger, Guid userId);
}

""";

    public const string FrontendSource = """
export function TasksPage() {
  return (
    <div>
      {/* US-TASK-01 */}
      <button>New task</button>
    </div>
  );
}

""";
}
