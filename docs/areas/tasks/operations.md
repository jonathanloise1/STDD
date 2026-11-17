# Tasks - Operations

## task-create-not-allowed

| Field | Value |
|-------|-------|
| Log key | `task.create.not_allowed` |
| Test case | [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | access |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member whose role does not allow creating tasks (a Viewer) tried to save a new task, usually because their role was lowered while the form was open. The task was not created and the user saw "Your role does not allow this change."

### Check

1. Find the user's role: ask an Admin of the organization named in the entry to open the Team page and read the member's role. The entry is correct when the role is Viewer.
2. Read the Team history: ask the Admin whether the role was lowered recently, or use `audit_trail_by_entity` (entity `organization`) for the organization to see changes made around the entry time. The usual cause is a role lowered while the user had the form open.
3. If the role is Admin or Editor, the request was refused although it should have been allowed: keep the trace id and hand over, because the role the server read is different from the role shown.
4. Count the entries per organization: a Viewer who keeps trying means the screen showed a button it should have hidden for that role.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| logs_by_correlation | trace_id={signal.TraceId} | records exists |
| audit_trail_by_entity | entity=organization, id={signal.Properties.OrganizationId} | changes exists |

### Resolve

1. No action when the user is a Viewer: tell them their role does not allow changes and that an Admin can change the role on the Team page.
2. Hand over to development when the user is an Admin or Editor, or when Viewers keep reaching the action from the screen.

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

A member saved a new task and the service did not answer. The form stays open with what was typed. Whether the task was stored is not known to the user.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that creates the task) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Check that no duplicate was created: ask the user to look at the Tasks list for the title before typing it again. If the task is there, the save succeeded and only the answer was lost.
5. Ask the user to try again once the checks above are healthy. If it fails again with a healthy service, collect the new Reference.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. No action when the service is healthy again and the entries stop: the user only has to try again.
2. When a dependency stays unhealthy, hand over to development or the platform owner with the unhealthy check, the first entry time and one trace id.
3. When the service is healthy and the entry repeats for the same request, hand over to development with the trace id: it is a code or data defect, not an outage.

### History

None.

## task-delete-not-found

| Field | Value |
|-------|-------|
| Log key | `task.delete.not_found` |
| Test case | [TASK-DELETE-002](test-cases/TASK-DELETE-002.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member confirmed the deletion of a task that was already deleted, usually by someone else a moment before. The member sees "This task no longer exists." and the list is refreshed.

### Check

1. Confirm the task is gone: `sql_entity_read` with entity `task` and the task id from the entry; `found` must be false.
2. Read `audit_trail_by_entity` (entity `task`) for the same id: a Delete entry shortly before the entry names who removed it.
3. If the task still exists, the delete was refused for the wrong reason: keep the trace id and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| sql_entity_read | entity=task, id={signal.Properties.TaskId} | found == false |

### Resolve

1. No action when the task is gone and the delete is in the history: two people deleted the same task.
2. Ask the user to reload the Tasks page.
3. Hand over to development when the task still exists.

### History

None.

## task-edit-not-allowed

| Field | Value |
|-------|-------|
| Log key | `task.edit.not_allowed` |
| Test case | [TASK-EDIT-007](test-cases/TASK-EDIT-007.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | access |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member whose role does not allow editing tasks (a Viewer) tried to save a change, usually because their role was lowered while the edit form was open. Nothing was changed and the user saw "Your role does not allow this change."

### Check

1. Find the user's role: ask an Admin of the organization named in the entry to open the Team page and read the member's role. The entry is correct when the role is Viewer.
2. Read the Team history: ask the Admin whether the role was lowered recently, or use `audit_trail_by_entity` (entity `organization`) for the organization to see changes made around the entry time. The usual cause is a role lowered while the user had the form open.
3. If the role is Admin or Editor, the request was refused although it should have been allowed: keep the trace id and hand over, because the role the server read is different from the role shown.
4. Count the entries per organization: a Viewer who keeps trying means the screen showed a button it should have hidden for that role.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| logs_by_correlation | trace_id={signal.TraceId} | records exists |
| audit_trail_by_entity | entity=organization, id={signal.Properties.OrganizationId} | changes exists |

### Resolve

1. No action when the user is a Viewer: tell them their role does not allow changes and that an Admin can change the role on the Team page.
2. Hand over to development when the user is an Admin or Editor, or when Viewers keep reaching the action from the screen.

### History

None.

## task-edit-not-found

| Field | Value |
|-------|-------|
| Log key | `task.edit.not_found` |
| Test case | [TASK-EDIT-004](test-cases/TASK-EDIT-004.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member saved changes to a task that was deleted by someone else while the edit form was open. The member sees "This task no longer exists." and the changes are lost.

### Check

1. Confirm the task is gone: `sql_entity_read` with entity `task` and the task id from the entry; `found` must be false.
2. Read `audit_trail_by_entity` (entity `task`) for the same id: a Delete entry shortly before the entry names who removed it.
3. If the task still exists, the update was refused for the wrong reason: keep the trace id and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| sql_entity_read | entity=task, id={signal.Properties.TaskId} | found == false |

### Resolve

1. No action when the task is gone and the delete is in the history. If the user still needs the task, they create it again.
2. Hand over to development when the task still exists.

### History

None.

## task-list-unavailable

| Field | Value |
|-------|-------|
| Log key | `task.list.unavailable` |
| Test case | [TASK-LIST-004](test-cases/TASK-LIST-004.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member opened the Tasks page and the tasks could not be loaded; the page shows the service message instead of the table.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that lists the tasks) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Ask the user to try again once the checks above are healthy. If it fails again with a healthy service, collect the new Reference.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. No action when the service is healthy again and the entries stop: the user only has to try again.
2. When a dependency stays unhealthy, hand over to development or the platform owner with the unhealthy check, the first entry time and one trace id.
3. When the service is healthy and the entry repeats for the same request, hand over to development with the trace id: it is a code or data defect, not an outage.

### History

None.

## task-status-not-found

| Field | Value |
|-------|-------|
| Log key | `task.status.not_found` |
| Test case | [TASK-STATUS-004](test-cases/TASK-STATUS-004.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member clicked Change status on a task that was deleted by someone else. The member sees "This task no longer exists." and the list is refreshed.

### Check

1. Confirm the task is gone: `sql_entity_read` with entity `task` and the task id from the entry; `found` must be false.
2. Read `audit_trail_by_entity` (entity `task`) for the same id: a Delete entry shortly before the entry names who removed it.
3. If the task still exists, the status change was refused for the wrong reason: keep the trace id and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| sql_entity_read | entity=task, id={signal.Properties.TaskId} | found == false |

### Resolve

1. No action when the task is gone and the delete is in the history.
2. Ask the user to reload the Tasks page.
3. Hand over to development when the task still exists.

### History

None.
