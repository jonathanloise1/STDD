# Onboarding - User Stories

## Overview

Onboarding guides a signed-in user who belongs to no organization through a one-step wizard in which they create their organization. It appears instead of any other page until the user has an organization, and never to people who already belong to one, such as invited colleagues.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-ONBOARD-01](user-stories/US-ONBOARD-01.md) | Get asked to set up an organization | High |
| [US-ONBOARD-02](user-stories/US-ONBOARD-02.md) | Create my organization in the wizard | High |

## Business Rules

1. The wizard is shown to a signed-in user exactly while the user belongs to no organization; it cannot be skipped.
2. The wizard asks only for organization details; the app language is chosen with the "Change language" button in the header.
3. The app ships in English and Italian, English being the default language.

## UI Flow

1. Set Up Your Organization: the user fills in the "Organization Details" step and clicks "Submit"; the Dashboard page of the new organization opens.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
