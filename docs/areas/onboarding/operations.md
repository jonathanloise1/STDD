# Onboarding - Operations

## onboard-wiz-unavailable

| Field | Value |
|-------|-------|
| Log key | `onboard.wiz.unavailable` |
| Test case | [ONBOARD-WIZ-004](test-cases/ONBOARD-WIZ-004.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A new user submitted the organization details and the organization could not be created. The user stays on the setup page with the service message and still belongs to no organization.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that creates the organization) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Check that nothing was half-created: the user must still belong to no organization. Read the user with `sql_entity_read` (entity `user`, id from the trace) and look at `audit_trail_by_entity` (entity `user`) for a membership change near the entry time. If an organization was created although the user saw an error, hand over.
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
