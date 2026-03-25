# Authentication - Functional Requirements

## Overview

The Authentication system provides secure access to the MyApp platform using **Microsoft Entra External ID** with **email + one-time passcode (OTP)** — a passwordless login flow similar to Canva. Users enter their email, receive a 6-digit code via email, enter the code, and they're in. No passwords, no social login, no Microsoft account required.

The system supports automatic user provisioning on first login, identity synchronization, organization membership retrieval, and auto-acceptance of pending invitations.

## Authentication Method

| Aspect | Detail |
|--------|--------|
| **Provider** | Microsoft Entra External ID (CIAM) |
| **Method** | Email + OTP (one-time passcode sent via email) |
| **Flow** | User enters email → receives 6-digit code → enters code → authenticated |
| **Passwords** | None — passwordless only |
| **Social login** | None — email OTP only |
| **Token format** | JWT (access token + ID token) |
| **Token storage** | localStorage via MSAL |
| **Session persistence** | Silent token refresh via MSAL |

## User Stories

| ID | Story | Priority |
|----|-------|----------|
| US-AUTH-01 | Login via email + OTP | Critical |
| US-AUTH-02 | Automatic user provisioning on first login | Critical |
| US-AUTH-03 | Token management and session persistence | High |
| US-AUTH-04 | User data synchronization (POST /api/auth/sync) | Critical |
| US-AUTH-05 | Pending invitation auto-accepted on login | High |
| US-AUTH-06 | Logout | High |
| US-AUTH-07 | Post-login navigation | High |
| US-AUTH-08 | Language preference | High |

---

## US-AUTH-01: User Login via Email + OTP

**As a** user
**I want to** log in by entering my email and a one-time code
**So that** I can securely access the platform without remembering a password

### Acceptance Criteria

- User clicks "Login" and is redirected to the Entra External ID login page.
- The login page asks for **email only** — no password field.
- After entering the email, the user receives a 6-digit OTP via email.
- User enters the OTP and is authenticated.
- After authentication, the frontend calls `POST /api/auth/sync` to synchronize the user with the backend.
- The sync response includes the user's organization memberships and roles.
- Session persists across browser refreshes (tokens stored in localStorage).
- Silent token refresh maintains the session without user intervention.
- If the user doesn't have an Entra account, one is created automatically on first login (Entra handles this via self-service sign-up).

### Access Control

Open — any user with a valid email address can log in.

### Business Rules

1. No passwords exist in the system — Entra External ID handles all credential management via OTP.
2. The OTP is valid for a limited time (configured in Entra, typically 5–10 minutes).
3. If the email doesn't exist in Entra, a new Entra account is created automatically (self-service sign-up enabled).
4. The login experience is branded with MyApp's custom domain via Azure Front Door.
5. Authentication redirects work from common auth paths (`/`, `/login`, `/auth/login`, `/auth-pages/login`).
6. Legitimate error pages (`/auth-pages/404`, `/auth-pages/unauthorized`) display without redirect loops.

### Implementation References

| Layer | File |
|-------|------|
| Controller | `MyApp.WebApi/Controllers/AuthController.cs` — `SyncUserAsync` |
| Service | `MyApp.Application/Services/AuthenticationService.cs` — `SyncUserAsync` |
| Config | `MyApp.WebApi/Configuration/ServiceCollectionExtensions.cs` — `AddAuthenticationAndAuthorization` |
| Frontend | `src/frontend/src/App/App.tsx` — routing/redirect logic |
| Frontend | `src/frontend/src/contexts/authContext.tsx` — MSAL integration |

---

## US-AUTH-02: User Provisioning (First Login)

**As a** new user
**I want to** be automatically provisioned on my first login
**So that** I can start using the platform immediately without a manual registration step

### Acceptance Criteria

- On first `POST /api/auth/sync`, a new `User` record is created in the database.
- The `User.AadId` is set to the `objectidentifier` claim from the JWT.
- `FirstName`, `LastName`, and `Email` are extracted from standard JWT claims (`given_name`, `family_name`, `emails`).
- `FirstName` and `LastName` are title-cased (via Humanizer) before being passed to the service.
- `User.Id` is a new server-generated GUID (not the AAD object identifier).
- Default values: `PhoneNumber = ""`, `CreatedAt = UtcNow`, `UpdatedAt = UtcNow`.
- If the email matches a pending organization invitation, it is auto-accepted (see US-AUTH-05).
- Subsequent logins do not create duplicate users — the system finds the existing user by `AadId`.

### Business Rules

1. User uniqueness is determined by `AadId` (Entra object identifier), not by email.
2. If the user's identity info (name or email) changes in Entra, it is updated in the local database on next sync.
3. No welcome email is sent by MyApp — Entra handles the OTP email.

### Error States

| Scenario | Behavior |
|----------|----------|
| Missing `objectidentifier` claim | 401 Unauthorized |
| Missing `email` claim | 401 Unauthorized |
| Database save failure | 500 Internal Server Error |

### Implementation References

| Layer | File |
|-------|------|
| Controller | `MyApp.WebApi/Controllers/AuthController.cs` — claim extraction + 401 guard |
| Service | `MyApp.Application/Services/AuthenticationService.cs` — `SyncUserAsync` (new user branch) |
| Entity | `MyApp.Domain/Entities/User.cs` |
| Repository | `MyApp.Infrastructure/Repositories/UsersRepository.cs` |

---

## US-AUTH-03: Token Management

**As a** logged-in user
**I want** my session to remain active
**So that** I don't need to re-enter my email and OTP frequently

### Acceptance Criteria

- ID token and access token are stored in localStorage by MSAL.
- Access token is automatically refreshed via MSAL silent acquisition before expiration.
- Preemptive token refresh runs periodically when authenticated.
- Token validation includes expiration check.
- User is logged out when token refresh fails completely.

### Implementation References

| Layer | File |
|-------|------|
| Frontend | `src/frontend/src/contexts/authContext.tsx` — MSAL config, silent refresh |
| Backend | `MyApp.WebApi/Configuration/ServiceCollectionExtensions.cs` — `AddMicrosoftIdentityWebApi` validates JWTs |

---

## US-AUTH-04: User Data Synchronization

**As a** logged-in user
**I want** my profile and organization memberships to be synchronized
**So that** I have the correct access level and see my organizations

### Acceptance Criteria

- User data syncs with backend on login via `POST /api/auth/sync`.
- Request body optionally accepts `organizationId` (reserved for future use).
- Sync response includes:
  - `Id` (internal user GUID)
  - `Email`
  - `Organizations[]` with `organizationId`, `organizationName`, `role`
- If the user's name or email changed in Entra, the local record is updated.
- Roles in the response are the string representation of `OrganizationRole` (Admin, Editor, Viewer).

### API Contract

**Request:** `POST /api/auth/sync`
```
Authorization: Bearer {jwt}
Content-Type: application/json

{ "organizationId": "{guid}" }   // optional, reserved for future use
```

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "organizations": [
    {
      "organizationId": "...",
      "organizationName": "Acme Corp",
      "role": "Admin"
    }
  ]
}
```

**Error Responses:**

| Status | Condition |
|--------|-----------|
| 401 | Missing/invalid JWT, or missing `objectidentifier` / `email` claims |
| 429 | Rate limit exceeded (100 req/min per user) |

### Business Rules

1. Sync is idempotent — calling it multiple times with no changes produces the same result.
2. Only organizations where the user has `Status = Active` are returned.
3. The `organizationId` parameter is accepted but currently unused in the implementation.

### Implementation References

| Layer | File |
|-------|------|
| Controller | `MyApp.WebApi/Controllers/AuthController.cs` |
| Service | `MyApp.Application/Services/AuthenticationService.cs` |
| DTO | `MyApp.Application/DTOs/Users/AuthenticatedUserDto.cs` |
| DTO | `MyApp.Application/DTOs/Users/AuthenticatedUserOrganizationDto.cs` |

---

## US-AUTH-05: Pending Membership Auto-Activation on Login

**As a** user added to an organization by an admin
**I want** my membership to be auto-activated when I log in
**So that** I immediately see and can access the organization

### Acceptance Criteria

- During `SyncUserAsync`, pending `OrganizationUser` records matching the user's email (case-insensitive) are detected.
- For each matching record:
  - The `OrganizationUser.UserId` is set to the user's `User.Id`.
  - The `OrganizationUser.Status` is set to `Active`.
- The newly joined organization appears in the sync response's `Organizations` list.
- Multiple pending memberships across different organizations are all activated in one sync.

### Business Rules

1. Auto-activation is triggered every time `SyncUserAsync` runs, not just on first login.
2. Email matching is case-insensitive.
3. Pending memberships do not expire — they are valid indefinitely until the user registers.
4. The role assigned is whatever the admin set when adding the collaborator.
5. If the user is already an active member of the organization, no duplicate membership is created.

### Error States

| Scenario | Behavior |
|----------|----------|
| No pending memberships | Normal login — no side effects |
| Organization deleted | Pending membership may still exist but org won't appear in active organizations |

### Implementation References

| Layer | File |
|-------|------|
| Service | `MyApp.Application/Services/AuthenticationService.cs` — `AutoActivatePendingMembershipsAsync` |
| Repository | `MyApp.Infrastructure/Repositories/OrganizationsRepository.cs` — `ActivatePendingMembershipsByEmailAsync` |
| Entity | `MyApp.Domain/Entities/OrganizationUser.cs` (Email, Status, UserId) |

---

## US-AUTH-06: Logout

**As a** user
**I want to** log out of the platform
**So that** my session is securely terminated

### Acceptance Criteria

- All tokens are cleared from localStorage.
- MSAL cache is cleared.
- User is redirected to the home page (`/`).
- Post-logout, accessing any protected route redirects to login.
- Post-logout API calls return `401 Unauthorized`.

### Business Rules

1. Logout is a client-side operation — no backend endpoint is called.
2. The next login will require entering the email and receiving a new OTP.

### Implementation References

| Layer | File |
|-------|------|
| Frontend | `src/frontend/src/contexts/authContext.tsx` — MSAL logout |

---

## US-AUTH-07: Post-Login Navigation

**As a** logged-in user
**I want to** be directed to the right page after login
**So that** I land where I need to be

### Acceptance Criteria

- User with organizations → redirected to `/company/dashboard`.
- User without organizations → redirected to `/company/organizations` (organization list page).
- No infinite redirect loops on auth paths (`/`, `/login`, `/auth/login`, `/auth-pages/login`).
- Error pages (`/auth-pages/404`, `/auth-pages/unauthorized`) render without redirect.

### Business Rules

1. Navigation logic runs after sync completes and organizations are loaded.
2. Organization context must be fully loaded before redirect decisions are made.
3. Users without organizations see the org list page — they cannot create orgs themselves (activation is manual by the operator).

### Implementation References

| Layer | File |
|-------|------|
| Frontend | `src/frontend/src/App/App.tsx` — redirect logic |

---

## US-AUTH-08: Language Preference

**As a** user in Switzerland
**I want to** choose my preferred language (DE, FR, or IT)
**So that** the entire platform UI and AI responses are in my language

### Acceptance Criteria

- User can set preferred language from profile settings or during onboarding.
- Supported languages: German (DE), French (FR), Italian (IT).
- Language preference is stored in the `User` entity (`PreferredLanguage` field).
- All UI text is rendered in the selected language via i18n.
- Navigation menu items (sidebar and bottom tab bar) update immediately when the user changes language.
- AI chat responses respect the user's language preference.
- Default language is German (DE) if not explicitly selected.
- Language preference is returned in the sync response and persisted across sessions.
- Changing language takes effect immediately (no page reload required).

### Access Control

Any authenticated user can change their own language preference.

### Business Rules

1. Only three languages are supported: `de`, `fr`, `it`.
2. Default language is `de` (German).
3. Language preference is per-user, not per-organization.
4. The AI agent receives the user's language as context and responds accordingly.

### Implementation References

| Layer | File |
|-------|------|
| Entity | `MyApp.Domain/Entities/User.cs` — `PreferredLanguage` |
| Service | `MyApp.Application/Services/AuthenticationService.cs` — sync includes language |
| Frontend | `src/frontend/src/i18n/` — i18n configuration |
| Frontend | `src/frontend/src/components/app/SlimSidebar.tsx` — sidebar uses `useTranslation('menu')` |
| Frontend | `src/frontend/src/components/app/BottomTabBar.tsx` — bottom tab bar uses `useTranslation('menu')` |
| API | `PUT /api/users/me/language` |

---

## E2E Testing: Impersonation Pattern

For E2E tests, authentication is bypassed via an **impersonation** pattern:

- **Frontend**: `E2E_MOCK_USER` JSON in localStorage → frontend skips MSAL.
- **Backend**: `X-MyApp-Impersonate-UserId` header → `ImpersonationMiddleware` replaces `ClaimsPrincipal`.
- Impersonation is **only enabled** in Development (`appsettings.Development.json`).

### Implementation References

| Layer | File |
|-------|------|
| Middleware | `MyApp.WebApi/Middlewares/ImpersonationMiddleware.cs` |
| Config | `MyApp.WebApi/appsettings.Development.json` — `MyApp:Impersonation:Enabled` |
| E2E | `tests/e2e/MyApp.E2E/` |

---

*Last updated: March 2026*
