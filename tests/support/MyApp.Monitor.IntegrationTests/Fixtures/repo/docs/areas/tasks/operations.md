# Tasks - Operations

## task-create-access-lost

| Field | Value |
|-------|-------|
| Log key | `task.create.access_lost` |
| Test case | [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | access |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member tried to create a task in an organization they had just been removed from. Expected after team changes.

### Check

1. Open the trace with `logs_by_correlation` and confirm the request was refused for this organization.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |

### Resolve

1. No action when a removal precedes the entry. Otherwise hand over to development with the trace id.

### History

None.

## task-create-unavailable

| Field | Value |
|-------|-------|
| Log key | `task.create.unavailable` |
| Test case | [TASK-CREATE-008](test-cases/TASK-CREATE-008.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

The task could not be saved because a dependency failed. The user was asked to retry.

### Check

1. Read the health of the application with `health_status`.

### Rule checks

None.

### Resolve

1. Hand over to development with the trace id.

### History

- 2026-10-01 INC-20261001-0001 fixed https://example.invalid/pr/1
