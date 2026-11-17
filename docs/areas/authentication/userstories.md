# Authentication - User Stories

## Overview

Authentication lets anyone with an email address sign in with a one-time code, with no password and no separate registration. It keeps the app closed to visitors, brings invited people into their organization, shows each user the menu their role allows, and lets them move between the sections of the app, work in English or Italian, and sign out.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-AUTH-01](user-stories/US-AUTH-01.md) | Sign in with your email address | Critical |
| [US-AUTH-02](user-stories/US-AUTH-02.md) | Get asked to sign in before using the app | Critical |
| [US-AUTH-03](user-stories/US-AUTH-03.md) | Sign out of the app | High |
| [US-AUTH-04](user-stories/US-AUTH-04.md) | Start using the app on first sign-in | Critical |
| [US-AUTH-05](user-stories/US-AUTH-05.md) | Join an organization you were invited to | High |
| [US-AUTH-06](user-stories/US-AUTH-06.md) | Move between the sections of the app | High |
| [US-AUTH-07](user-stories/US-AUTH-07.md) | Open only the pages your role allows | High |
| [US-AUTH-08](user-stories/US-AUTH-08.md) | Change the app language | High |
| [US-AUTH-09](user-stories/US-AUTH-09.md) | Move between pages with the keyboard | Low |

## Business Rules

1. Signing in needs an email address and a code sent to it; there are no passwords.
2. Every page of the app needs a signed-in user; a visitor sees only the sign-in page.
3. The app is available in English and Italian; English is the default until the user chooses.
4. The role a user has in the current organization (Admin, Editor or Viewer) decides which menu entries they see and which pages open for them. The Team, Audit Log, Organization and Update Organization pages are for Admins only.
5. Opening a page the role does not allow takes the user to the Dashboard page with the message "You don't have access to this page."
6. A person who belongs to no organization sees only the "Set Up Your Organization" page (Onboarding area).

## UI Flow

1. Sign-in page: the user enters an email address and then the code received by email; the page asked for, or the Dashboard page, opens.
2. Set Up Your Organization: shown instead of any other page to a user who belongs to no organization; the Dashboard page of the new organization follows.
3. Dashboard: the first page for a member; the menu leads to Tasks and, for Admins, to Team, Audit Log and Settings; the avatar button opens "Logout" (and "Settings" for Admins), and the "Change language" button changes the language.
4. Tasks: the list of the organization's tasks, reached from the menu.
5. Team, Audit Log and Settings: management pages for Admins; anyone else is taken back to the Dashboard page with a message.
6. Keyboard Shortcuts dialog: opened with the question mark key; lists the shortcuts of the pages the role allows.
7. Page not found: shown for an unknown address, with a "Homepage" link back to the Dashboard page.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | Yes |
| Signed-in user | Yes |
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
