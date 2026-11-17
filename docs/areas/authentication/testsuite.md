# Authentication - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [AUTH-LOGIN-001](test-cases/AUTH-LOGIN-001.md) | Signing in opens the Dashboard page | US-AUTH-01 | Happy path | Critical |
| [AUTH-LOGIN-003](test-cases/AUTH-LOGIN-003.md) | The sign-in page asks for an email and no password | US-AUTH-01 | Business rule | Medium |
| [AUTH-LOGIN-004](test-cases/AUTH-LOGIN-004.md) | Reloading the page keeps the user signed in | US-AUTH-01 | Business rule | High |
| [AUTH-LOGIN-006](test-cases/AUTH-LOGIN-006.md) | Signing in when the service does not answer shows a message | US-AUTH-01 | Error handling | High |
| [AUTH-LOGIN-002](test-cases/AUTH-LOGIN-002.md) | Opening the Tasks page when not signed in asks to sign in | US-AUTH-02 | Happy path | High |
| [AUTH-LOGIN-005](test-cases/AUTH-LOGIN-005.md) | A visitor sees no part of the app before signing in | US-AUTH-02 | Business rule | High |
| [AUTH-LOGOUT-001](test-cases/AUTH-LOGOUT-001.md) | Signing out opens the sign-in page | US-AUTH-03 | Happy path | High |
| [AUTH-LOGOUT-002](test-cases/AUTH-LOGOUT-002.md) | After signing out, a page asks to sign in again | US-AUTH-03 | Business rule | High |
| [AUTH-PROVISION-001](test-cases/AUTH-PROVISION-001.md) | First sign-in goes straight into the app | US-AUTH-04 | Happy path | Critical |
| [AUTH-PROVISION-002](test-cases/AUTH-PROVISION-002.md) | Signing in again does not create a second account | US-AUTH-04 | Business rule | High |
| [AUTH-PROVISION-003](test-cases/AUTH-PROVISION-003.md) | A new account starts in English | US-AUTH-04 | Business rule | Medium |
| [AUTH-INVITE-001](test-cases/AUTH-INVITE-001.md) | Signing in joins the organization one was invited to | US-AUTH-05 | Happy path | Critical |
| [AUTH-INVITE-002](test-cases/AUTH-INVITE-002.md) | An invited member has the invited role | US-AUTH-05 | Business rule | High |
| [AUTH-NAV-001](test-cases/AUTH-NAV-001.md) | A member with an organization lands on the Dashboard page | US-AUTH-06 | Happy path | Critical |
| [AUTH-NAV-002](test-cases/AUTH-NAV-002.md) | The open page is highlighted in the menu | US-AUTH-06 | Business rule | Medium |
| [AUTH-NAV-003](test-cases/AUTH-NAV-003.md) | An unknown address shows Page not found | US-AUTH-06 | UX | Medium |
| [AUTH-ACCESS-001](test-cases/AUTH-ACCESS-001.md) | An Admin opens the management pages from the menu | US-AUTH-07 | Happy path | High |
| [AUTH-ACCESS-002](test-cases/AUTH-ACCESS-002.md) | An Editor sees no management pages | US-AUTH-07 | Security | High |
| [AUTH-ACCESS-003](test-cases/AUTH-ACCESS-003.md) | A Viewer sees no management pages | US-AUTH-07 | Security | High |
| [AUTH-ACCESS-004](test-cases/AUTH-ACCESS-004.md) | A non-admin opening the Audit Log returns to the Dashboard | US-AUTH-07 | Business rule | Medium |
| [AUTH-LANG-001](test-cases/AUTH-LANG-001.md) | Choosing a language switches the app at once | US-AUTH-08 | Happy path | High |
| [AUTH-LANG-002](test-cases/AUTH-LANG-002.md) | Menu entries change language with the app | US-AUTH-08 | UX | High |
| [AUTH-LANG-003](test-cases/AUTH-LANG-003.md) | The chosen language is kept at the next sign-in | US-AUTH-08 | Business rule | High |
| [AUTH-LANG-004](test-cases/AUTH-LANG-004.md) | Only English and Italiano are offered | US-AUTH-08 | Business rule | Medium |
| [AUTH-LANG-005](test-cases/AUTH-LANG-005.md) | Saving the language when the service does not answer shows a message | US-AUTH-08 | Error handling | Medium |
| [AUTH-SHORTCUT-001](test-cases/AUTH-SHORTCUT-001.md) | G then T opens the Tasks page | US-AUTH-09 | Happy path | Low |
| [AUTH-SHORTCUT-002](test-cases/AUTH-SHORTCUT-002.md) | An Editor has only the shortcuts of Dashboard and Tasks | US-AUTH-09 | Business rule | Low |
| [AUTH-SHORTCUT-003](test-cases/AUTH-SHORTCUT-003.md) | Typing in a field does not trigger shortcuts | US-AUTH-09 | Business rule | Low |
| [AUTH-SHORTCUT-004](test-cases/AUTH-SHORTCUT-004.md) | The question mark opens the shortcuts help | US-AUTH-09 | UX | Low |

## Seed Data

- The organization "MyApp Dev Org" with the members "Dev User" (Admin, its only Admin), "Dev Member" (Editor) and "Dev Viewer" (Viewer). Their language is English.
- "New User" (new@myapp.test) has never signed in and belongs to no organization; no test creates an organization with this user.
- The organization "E2E Invited Org" with the Admin "Invite Admin", who never signs in, and a pending invitation as Editor for "Test UserC" (testc@myapp.test), who belongs to no other organization.
