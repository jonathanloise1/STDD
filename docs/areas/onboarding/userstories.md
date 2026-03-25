# Onboarding — User Stories

## Overview

The onboarding flow guides first-time users through initial setup after their first login. Since an Organization (tenant) is created automatically on first login, the wizard focuses on:

1. **Language selection** — choose DE, FR, or IT.
2. **Welcome + first task CTA** — brief explanation of the platform and a call-to-action to create the first task.

The wizard is lightweight and can be skipped. Users who skip it land on the dashboard with an empty state prompting them to create a task.

| ID | Title | Priority |
|---|---|---|
| US-ONBOARD-01 | First-time onboarding wizard | High |
| US-ONBOARD-02 | Skip onboarding | Medium |

---

### US-ONBOARD-01 — First-Time Onboarding Wizard

**As a** new user logging in for the first time
**I want** a short guided setup
**So that** I can set my language and understand how to get started

#### Acceptance Criteria

- Wizard appears only on the user's very first login (when `User.HasCompletedOnboarding == false`).
- Step 1: Language selection — DE, FR, IT. Default is DE. Selection is saved to `User.PreferredLanguage`.
- Step 2: Welcome screen — brief overview of what MyApp does, with a prominent "Create your first task" button and a "Skip for now" link.
- If the user clicks "Create your first task", they are redirected to the task creation page.
- If the user clicks "Skip for now", they are redirected to the dashboard.
- After completing or skipping the wizard, `User.HasCompletedOnboarding` is set to `true`.
- The wizard never appears again after completion or skip.

#### Access Control

- Any authenticated user sees the wizard on first login.

#### Business Rules

1. The wizard is shown exactly once per user.
2. Language selection defaults to DE (German).
3. No organization details are collected — the tenant is auto-created during auth sync.
4. The wizard is purely client-side navigation with two API calls: language update and onboarding completion flag.

#### Non-Goals

- Organization creation (handled automatically in auth).
- Template or industry selection (not applicable).
- Fiscal year setup (not applicable).

### Implementation References

| Layer | File |
|-------|------|
| Frontend | `src/frontend/src/pages/OnboardingWizardPage.tsx` |
| Entity | `MyApp.Domain/Entities/User.cs` — `HasCompletedOnboarding`, `PreferredLanguage` |
| API | `PUT /api/users/me/language` |
| API | `POST /api/users/me/complete-onboarding` |

---

### US-ONBOARD-02 — Skip Onboarding

**As a** new user
**I want to** skip the onboarding wizard
**So that** I can explore the platform on my own

#### Acceptance Criteria

- A "Skip" button is visible on every step of the wizard.
- Clicking "Skip" marks `HasCompletedOnboarding = true` and redirects to the dashboard.
- Language remains the default (DE) if not explicitly changed.
- The wizard does not appear again.

#### Business Rules

1. Skipping still sets the completion flag — the wizard is one-shot.
2. The user can change their language later from profile settings (US-AUTH-08).

---

*Last updated: June 2025*
