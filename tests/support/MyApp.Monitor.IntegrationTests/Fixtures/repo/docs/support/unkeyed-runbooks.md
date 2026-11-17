# Unkeyed signatures

Runbooks for log patterns that have no test case: accepted framework noise and known infrastructure conditions
with no user-visible behaviour.

## t-2bb5dcdc1949

| Field | Value |
|-------|-------|
| Event name | platform.health.unhealthy |
| Category | MyApp.WebApi.Health |
| Exception type | None |
| Top frame | None |
| Template | sqlserver |
| Severity | Error |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

The database health check failed. Users see errors until it recovers.

### Check

1. Read `health_status`.

### Rule checks

None.

### Resolve

1. Hand over to the infrastructure owner.

### History

None.
