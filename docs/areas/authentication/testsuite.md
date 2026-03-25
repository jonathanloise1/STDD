# Authentication - Test Suite (MVP)

## Coverage Matrix

| Test Case | US-AUTH-01 | US-AUTH-02 | US-AUTH-04 | US-AUTH-05 | US-AUTH-06 | US-AUTH-07 | US-AUTH-08 | Type | Priority | Automated |
|-----------|:----------:|:----------:|:----------:|:----------:|:----------:|:----------:|:----------:|------|----------|-----------|
| AUTH-LOGIN-001 | ? | | | | | | | E2E | Critical | Planned |
| AUTH-LOGIN-002 | ? | | | | | | | E2E | High | Planned |
| AUTH-PROVISION-001 | | ? | ? | | | | | E2E | Critical | Planned |
| AUTH-SYNC-001 | | | ? | | | | | E2E | Critical | Planned |
| AUTH-INVITE-001 | | | | ? | | | | E2E | Critical | Planned |
| AUTH-LOGOUT-001 | | | | | ? | | | E2E | High | Planned |
| AUTH-NAV-001 | | | | | | ? | | E2E | Critical | Planned |
| AUTH-LANG-001 | | | | | | | ? | E2E | High | Planned |
| AUTH-LANG-002 | | | | | | | ? | E2E | High | Planned |

**Total: 9 test cases (MVP)**

> Deferred to post-MVP: token management, sync edge cases, multi-invitation scenarios, additional navigation guards.

## Test Case Summary

| Code | Title | Priority |
|------|-------|----------|
| AUTH-LOGIN-001 | Successful login via email + OTP (impersonation) | Critical |
| AUTH-LOGIN-002 | Unauthorized access without authentication | High |
| AUTH-PROVISION-001 | First login creates a new user record | Critical |
| AUTH-SYNC-001 | Sync returns user ID, email, and organizations | Critical |
| AUTH-INVITE-001 | Pending membership auto-activated on login | Critical |
| AUTH-LOGOUT-001 | Logout clears session and blocks protected access | High |
| AUTH-NAV-001 | User with orgs redirected to dashboard | Critical |
| AUTH-LANG-001 | User sets preferred language | High |
| AUTH-LANG-002 | Navigation menu items translate on language change | High |

## Testing Strategy

All authentication tests run as **E2E tests** using the **impersonation pattern**:

1. **Frontend mock**: localStorage `E2E_MOCK_USER` JSON bypasses MSAL.
2. **Backend mock**: `X-MyApp-Impersonate-UserId` header triggers `ImpersonationMiddleware`.
3. **Prerequisite**: `MyApp:Impersonation:Enabled = true` in `appsettings.Development.json`.

### Test Data

| User | Email | Has Orgs | Pending Memberships |
|------|-------|----------|---------------------|
| Test User A | testa@MyApp.test | Yes (1 org, Admin) | None |
| Test User C | testc@MyApp.test | No | 1 pending membership |
| New User | new@MyApp.test | N/A (not in DB) | None |

---

*Last updated: March 2026*
