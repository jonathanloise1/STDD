# MyApp - Development Guidelines

> Guidelines for development, documentation, and feature testing

---

## 1. Fundamental Principles

### 1.0 Philosophy

> "Specs and tests are the asset. Code is a commodity."

E2E tests verify the system works **from the user's perspective**. Every user story documented in userstories.md must have at least one test case validating it.

**Goals:**
- **Traceability**: Every test is linked to a user story
- **Automation**: Tests are executable automatically
- **Living documentation**: Tests describe expected behavior
- **AI-friendly**: Predictable structure for automatic generation

### 1.1 Language: English Only

**All project artifacts must be written in English.** This includes:
- ? Source code (variable names, class names, method names)
- ? Code comments and XML docs
- ? Documentation files (Markdown)
- ? Commit messages and PR descriptions
- ? User stories and test cases
- ? Log messages and error messages
- ? API responses and error codes

> **Note**: The only exceptions are user-facing UI strings managed through the localization system (`i18n` / locale JSON files), which are translated into the supported languages.

### 1.3 Documentation as Index

Documentation **must not duplicate code**. Instead:
- ? **References** to classes, methods, files
- ? **Paths** in the codebase (`Services/Tasks/TaskItemService.cs`)
- ? **Business logic** and business rules
- ? **NO** inline code (real code lives in the codebase)
- ? **NO** tech stack (already in [architecture.md](architecture.md))

### 1.4 Single Source of Truth

| Information | Where it lives |
|-------------|----------------|
| Tech stack | `docs/guidelines/architecture.md` |
| Naming conventions | `docs/guidelines/architecture.md` |
| Feature business logic | `docs/areas/{area}/userstories.md` |
| Actual implementation | **Codebase** |

---

## 2. Feature Documentation Structure

### 2.1 Files per Feature

```
docs/areas/{area-name}/
+-- userstories.md        # WHAT it does (user stories, business rules)
+-- testsuite.md          # Mapping US ? Test Cases
+-- test-cases/           # Individual test details
    +-- {CODE}.md
    +-- ...
```

### 2.2 Areas

Areas will be defined as the product evolves. Example structure:

| Area | Path | Description |
|------|------|-------------|
| `{area-name}` | `docs/areas/{area-name}/` | Feature group |

### 2.3 userstories.md - Template

```markdown
# {Feature Name} - Functional Requirements

## Overview
Brief description (2-3 sentences) of the feature's purpose.

## User Stories
| ID | Story | Priority |
|----|-------|----------|
| XX-01 | As a {user}, I want to {action} so that {benefit} | Critical/High/Medium/Low |

## Business Rules
1. Rule description...
2. Rule description...

## UI/UX Flow
[Description or ASCII art of the flow]

## Error States
| Scenario | Behavior |
|----------|----------|
| ... | ... |

## Access Control
| Role | Permission |
|------|------------|
| ... | ... |
```

---

## 3. Development Workflow

### 3.1 Activity Order

```
+-------------------------------------------------------------------------+
�  1. REQUIREMENTS                                                        �
�     +-- Write userstories.md with user stories and business rules      �
+-------------------------------------------------------------------------�
�  2. TEST SPECIFICATION                                                  �
�     +-- Write test cases in test-cases/ folder                          �
�         (happy path + error cases + edge cases)                         �
+-------------------------------------------------------------------------�
�  3. IMPLEMENTATION                                                      �
�     +-- Implement backend                                               �
�     +-- Implement frontend                                              �
�     +-- Implement tests (following test-cases/)                         �
+-------------------------------------------------------------------------�
�  4. TEST/DEV CYCLE                                                      �
�     +-- Run tests                                                       �
�     +-- Fix issues                                                      �
�     +-- Repeat until all tests pass                                     �
+-------------------------------------------------------------------------�
�  5. HUMAN REVIEW                                                        �
�     +-- Code review                                                     �
�     +-- Manual QA testing                                               �
�     +-- Stakeholder acceptance                                          �
+-------------------------------------------------------------------------+
```

### 3.2 Pre-Implementation Checklist

- [ ] userstories.md exists with complete user stories
- [ ] Test cases written (happy path + error cases + edge cases)
- [ ] Requirements review with stakeholder completed

### 3.3 Post-Implementation Checklist

- [ ] All documented test cases are implemented
- [ ] All tests pass
- [ ] Code review completed
- [ ] Manual QA testing completed
- [ ] Documentation updated if any changes were made
- [ ] PR approved by human reviewer

---

## 4. Reverse Engineering (Documenting Existing Code)

When documenting existing code:

### 4.1 Process

1. **Analyze** the existing code (controller ? service ? repository)
2. **Extract** implicit user stories from behavior
3. **Document** references to existing files (don't copy code)
4. **Identify** test cases from observed behavior

### 4.2 Don't Do

- ? Copy code blocks into docs
- ? Repeat tech stack in every file
- ? Duplicate information already in architecture.md
- ? Invent business rules not present in code

### 4.3 Do

- ? Precise references to files and methods
- ? Mapping tables (endpoint ? controller ? service)
- ? Business rules extracted from code
- ? Notes on non-obvious behaviors

---

## 5. Test Cases Documentation

### 5.1 Test Case Structure

```markdown
# TEST_CASE_ID: Feature Description

## Preconditions
- User is authenticated as {role}
- {Other preconditions}

## Steps
1. Navigate to {page}
2. Click {element}
3. Fill {form} with {data}
4. Submit

## Expected Result
- {Observable outcome}
- {State change}

## Related
- userstories.md: {User Story ID}
- API: {Endpoint}
```

### 5.2 Naming Convention

```
{FEATURE}_{ACTION}_{OUTCOME}.md

TASK-CREATE-001.md
ACTIVITY_CREATE_VALIDATION_ERROR.md
TASK-UPDATE-001.md
COSTITEM_SPLIT_INVALID_PERCENTAGE.md
```

---

## 6. Maintenance

### 6.1 When to Update Docs

| Event | Action |
|-------|--------|
| New feature | Create USER_STORIES + test cases before implementing |
| Bug fix | Update if business logic changes, add test case |
| Refactoring | Update references to files/classes |
| API change | Update relevant docs |
| UI change | Update if structure changes |

### 6.2 Periodic Review

- Quarterly: verify file references are correct
- After major release: complete documentation review

---

## 7. Quick Reference

### What to Put in userstories.md
- User stories
- Business rules
- Access control
- Error handling
- UI flows (descriptive)

### What to NEVER Put
- Code blocks (code lives in the codebase)
- Tech stack (lives in architecture.md)
- Information duplicated from other docs

---

## 8. Test Strategy

### 8.1 testsuite.md

High-level file that maps user stories to test cases. **Does not duplicate** user stories (they are in userstories.md).

**Template:**
```markdown
# {Feature Name} - Test Suite

> **User Stories**: [userstories.md](./userstories.md)

## Coverage Matrix

| User Story | Test Cases |
|------------|------------|
| US-XXX-01 | XXX-CREATE-001, XXX-CREATE-002 |
| US-XXX-02 | XXX-ASSIGN-001 |

## Test Cases

| Code | Title | Type | Priority |
|------|-------|------|----------|
| XXX-CREATE-001 | Create with valid data | Happy path | Critical |
| XXX-CREATE-002 | Create fails without required field | Validation | High |
```

### 8.2 Test Types

| Type | Description |
|------|-------------|
| `Happy path` | Main flow, everything works |
| `Validation` | Input validation (required fields, formats) |
| `Business rule` | Business rules (states, permissions) |
| `Edge case` | Edge cases |
| `Error handling` | Error handling |
| `UX` | UI behavior (loading, confirmations) |
| `Security` | Authorization, access control |
| `Integration` | External integrations |

### 8.3 Test Case File

Each test case has a dedicated file in the `test-cases/` folder.

**Naming Convention:** `{PREFIX}-{ACTION}-{NUMBER}.md`

**Template Test Case:**
```markdown
# {Title}

> **User Story**: US-XXX-NN  
> **Type**: Happy path | Validation | Business rule | ...  
> **Priority**: Critical | High | Medium | Low  
> **Automated**: ? Yes | ? No | ?? In progress

## Preconditions
- [ ] User is logged in as {role}
- [ ] {Entity} exists with ID `{id}`

## Test Data
| Field | Value |
|-------|-------|
| fieldName | "Test Value" |

## Steps
1. Navigate to `/path`
2. Click "Button" button
3. Fill in field: `{fieldName}`
4. Click "Save"

## Expected Results
- [ ] Success toast appears: "Created successfully"
- [ ] Redirect to `/path/{newId}`

## API Calls
| Method | Endpoint | Expected Status |
|--------|----------|-----------------|
| POST | `/api/resource` | 201 |
```

### 8.4 Coverage Goals

| Priority | Coverage Target |
|----------|-----------------|
| Core flows | 90%+ |
| Secondary features | 70%+ |
| Edge cases | 50%+ |

Every user story must have at least:
- 1 **Happy path** test
- 1 **Validation** test (if it has inputs)
- 1 **Business rule** test (if it has rules)

### 8.5 E2E Authentication (Impersonation Pattern)

For E2E tests with Playwright, we use an **impersonation** pattern that completely bypasses Microsoft Entra login:

```
+---------------------------------------------------------------------+
�                   E2E AUTH FLOW                                      �
+---------------------------------------------------------------------�
�                                                                     �
�  Test           localStorage               Frontend                 �
�  Setup   --->   E2E_MOCK_USER json  --->   Skips MSAL               �
�                                                                     �
�  Test           X-Impersonate-UserId       Backend                  �
�  API call --->  header added        --->   ImpersonationMiddleware  �
�                                                                     �
+---------------------------------------------------------------------+
```

> **?? SECURITY**: Impersonation is enabled ONLY in `appsettings.Development.json`.  
> NEVER enable in production!

---

## 9. Related Documents

| Document | Purpose |
|----------|---------|
| [overview.md](../overview.md) | What is MyApp |
| [architecture.md](architecture.md) | Technical architecture |

---

*Last updated: March 2026*
