# Organizations - Operations

## org-invite-already-invited

| Field | Value |
|-------|-------|
| Log key | `org.invite.already_invited` |
| Test case | [ORG-INVITE-006](test-cases/ORG-INVITE-006.md) |
| Expected rate | 10 per hour |
| Fingerprint by | OrganizationId |
| Cause category | expected_behaviour |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin invited an email address that already has a pending invitation. The Admin sees a message and no second invitation is created.

### Check

1. No investigation is needed for a single entry: it is the user's own repeat.
2. When the rate is high for one organization, ask its Admin whether invitations are not reaching people: the invited person may not have received the first email, so the Admin keeps inviting again.
3. Ask the Admin to check the Pending Invitations list on the Team page: the person is already there, with the date the invitation was sent.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| signature_lookup | log_key=org.invite.already_invited | found == true |

### Resolve

1. Tell the Admin the invitation is already pending and that the person can sign in with the invited email address.
2. When the person says no email arrived, hand over to the platform owner to check the mail delivery of the sign-in provider.

### History

None.

## org-invite-already-member

| Field | Value |
|-------|-------|
| Log key | `org.invite.already_member` |
| Test case | [ORG-INVITE-005](test-cases/ORG-INVITE-005.md) |
| Expected rate | 10 per hour |
| Fingerprint by | OrganizationId |
| Cause category | expected_behaviour |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin invited an email address that already belongs to a member of the organization. The Admin sees a message and no invitation is created.

### Check

1. No investigation is needed for a single entry: the invitation was not necessary.
2. Ask the Admin to open the Team page and search the email: the person is listed with a role.
3. If the person says they cannot get in, check their role: they may be a Viewer who expected to edit. The role is changed with the Edit button on the Team page.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| signature_lookup | log_key=org.invite.already_member | found == true |

### Resolve

1. Tell the Admin that the person is already a member and which role they have; change the role from the Team page if it is the wrong one.
2. No further action.

### History

None.

## org-logo-unavailable

| Field | Value |
|-------|-------|
| Log key | `org.logo.unavailable` |
| Test case | [ORG-LOGO-004](test-cases/ORG-LOGO-004.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin uploaded the organization logo and the service did not answer; the logo did not change. Only that Admin is affected.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that uploads the logo) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Check the file: the application accepts images up to 5 MB. A file near the limit on a slow connection can end in a timeout even when the service is healthy; ask the Admin for the file size.
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

## org-remove-not-found

| Field | Value |
|-------|-------|
| Log key | `org.remove.not_found` |
| Test case | [ORG-REMOVE-004](test-cases/ORG-REMOVE-004.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin tried to remove a member who was already removed, usually by another Admin a moment before. The Admin sees "This member is no longer in the team." and nothing else changes.

### Check

1. Confirm that the member is gone: read the membership with `sql_entity_read` (entity `membership`) using the id in the entry.
2. Read its history with `audit_trail_by_entity` (entity `membership`): the removal and the user who made it should appear shortly before the entry.
3. If the membership still exists, the request was rejected for the wrong reason: keep the trace id and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| sql_entity_read | entity=membership, id={signal.Properties.MembershipId} | found == false |

### Resolve

1. No action when the membership is gone and the removal is in the history: two Admins acted on the same member.
2. Ask the Admin to refresh the Team page.
3. Hand over to development when the membership still exists.

### History

None.

## org-role-last-admin

| Field | Value |
|-------|-------|
| Log key | `org.role.last_admin` |
| Test case | [ORG-ROLE-005](test-cases/ORG-ROLE-005.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | expected_behaviour |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

The only Admin of an organization tried to give themselves a lower role. The change was refused so that the organization keeps an Admin; the Admin sees "An organization needs at least one Admin."

### Check

1. No investigation is needed for a single entry: the rule worked.
2. Ask the Admin what they wanted to do. To hand over the organization, they first make another member an Admin, then lower their own role.
3. If the organization has no Admin at all (found by asking a member), that is a data problem: look at the history of the memberships with `audit_trail_by_entity` (entity `organization`) and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| signature_lookup | log_key=org.role.last_admin | found == true |

### Resolve

1. Explain how to hand over the Admin role: promote another member first from the Team page.
2. Hand over to development only when an organization is found with no Admin.

### History

None.

## org-role-not-found

| Field | Value |
|-------|-------|
| Log key | `org.role.not_found` |
| Test case | [ORG-ROLE-006](test-cases/ORG-ROLE-006.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin saved a new role for a member who had been removed in the meantime. The Admin sees "This member is no longer in the team." and no role is changed.

### Check

1. Confirm that the member is gone: read the membership with `sql_entity_read` (entity `membership`) using the id in the entry.
2. Read `audit_trail_by_entity` (entity `membership`) to see who removed the member and when.
3. If the membership still exists, the request was rejected for the wrong reason: keep the trace id and hand over.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| sql_entity_read | entity=membership, id={signal.Properties.MembershipId} | found == false |

### Resolve

1. No action when the membership is gone and the removal is in the history.
2. Ask the Admin to refresh the Team page; invite the person again if they should still be in the team.
3. Hand over to development when the membership still exists.

### History

None.

## org-team-unavailable

| Field | Value |
|-------|-------|
| Log key | `org.team.unavailable` |
| Test case | [ORG-TEAM-005](test-cases/ORG-TEAM-005.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin opened the Team page and the members could not be loaded; the page shows the service message instead of the table.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that lists the team) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
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

## org-update-unavailable

| Field | Value |
|-------|-------|
| Log key | `org.update.unavailable` |
| Test case | [ORG-UPDATE-007](test-cases/ORG-UPDATE-007.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin saved the organization details and the service did not answer. The form keeps what was typed; it is not known to the user whether the change was stored.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that saves the organization) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Check whether the change was stored: read `audit_trail_by_entity` (entity `organization`) for the organization and look for an update near the entry time.
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

## org-view-unavailable

| Field | Value |
|-------|-------|
| Log key | `org.view.unavailable` |
| Test case | [ORG-VIEW-003](test-cases/ORG-VIEW-003.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin opened the Organization page and the details could not be loaded; the page shows the service message.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that loads the organization) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
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
