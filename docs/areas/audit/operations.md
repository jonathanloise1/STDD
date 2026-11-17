# Audit Trail - Operations

## aud-list-unavailable

| Field | Value |
|-------|-------|
| Log key | `aud.list.unavailable` |
| Test case | [AUD-LIST-007](test-cases/AUD-LIST-007.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin opened the Audit Log page or changed a filter and the changes could not be loaded; the page shows the service message instead of the table. Only that Admin's request is affected.

### Check

1. Ask the user for the Reference under the message (a 32-character trace id) and open it with `logs_by_correlation`. Find the request that failed (the request that lists audit changes) and read the dependency call and the exception under it: a timeout, a refused connection or a database error each point to a different fix.
2. Read `health_status`. A check that is not healthy (database first, then storage or the sign-in provider if listed) is the cause; note which one and since when.
3. Count the entries with this log key in the last hour. A few isolated entries with a healthy service are a short interruption that has ended. Many entries from different organizations are an outage: do not investigate user by user.
4. Check whether the filter matters: a very wide date range on a large history is slow, so read the request's duration in the trace and compare it with the timeout.
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
