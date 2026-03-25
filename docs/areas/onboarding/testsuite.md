# Onboarding — Test Suite

## Coverage Matrix

| Test Case | US-ONBOARD-01 | US-ONBOARD-02 | Type | Priority | Automated |
|-----------|:-------------:|:-------------:|------|----------|-----------|
| ONBOARD-WIZ-001 | ✓ | | E2E | High | Yes |
| ONBOARD-SKIP-001 | | ✓ | E2E | Medium | Yes |

## Test Case Summary

| ID | Title | Type | US |
|---|---|---|---|
| ONBOARD-WIZ-001 | [Onboarding wizard sets language and redirects to tasks](./test-cases/ONBOARD-WIZ-001.md) | E2E | US-ONBOARD-01 |
| ONBOARD-SKIP-001 | [Skip onboarding redirects to dashboard](./test-cases/ONBOARD-SKIP-001.md) | E2E | US-ONBOARD-02 |

**Total: 2 test cases**

## Testing Strategy

Tests use the impersonation pattern with a fresh user who has `HasCompletedOnboarding = false`.

### Test Data

| User | HasCompletedOnboarding | PreferredLanguage |
|------|------------------------|-------------------|
| New User | false | de (default) |

---

*Last updated: June 2025*
