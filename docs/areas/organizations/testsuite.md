# Organizations - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [ORG-VIEW-001](test-cases/ORG-VIEW-001.md) | Settings shows the profile and billing details | US-ORG-01 | Happy path | High |
| [ORG-VIEW-002](test-cases/ORG-VIEW-002.md) | A viewer cannot open the Organization page | US-ORG-01 | Security | High |
| [ORG-VIEW-003](test-cases/ORG-VIEW-003.md) | Loading the details when the service does not answer shows a message | US-ORG-01 | Error handling | Medium |
| [ORG-UPDATE-001](test-cases/ORG-UPDATE-001.md) | Edit the city of the organization | US-ORG-02 | Happy path | Critical |
| [ORG-UPDATE-002](test-cases/ORG-UPDATE-002.md) | An editor cannot open the Update Organization page | US-ORG-02 | Security | High |
| [ORG-UPDATE-003](test-cases/ORG-UPDATE-003.md) | Saving with empty required fields shows field messages | US-ORG-02 | Validation | High |
| [ORG-UPDATE-004](test-cases/ORG-UPDATE-004.md) | An invalid billing email shows a field message | US-ORG-02 | Validation | Medium |
| [ORG-UPDATE-005](test-cases/ORG-UPDATE-005.md) | A ZIP code that is not five digits shows a field message | US-ORG-02 | Validation | Medium |
| [ORG-UPDATE-006](test-cases/ORG-UPDATE-006.md) | The Unsaved Changes badge follows the edited values | US-ORG-02 | Business rule | Low |
| [ORG-UPDATE-007](test-cases/ORG-UPDATE-007.md) | Saving when the service does not answer shows a message | US-ORG-02 | Error handling | Medium |
| [ORG-LOGO-001](test-cases/ORG-LOGO-001.md) | Upload an image as the organization logo | US-ORG-03 | Happy path | Medium |
| [ORG-LOGO-002](test-cases/ORG-LOGO-002.md) | A file that is not an image is refused | US-ORG-03 | Validation | Medium |
| [ORG-LOGO-003](test-cases/ORG-LOGO-003.md) | An image larger than 5 MB is refused | US-ORG-03 | Validation | Medium |
| [ORG-LOGO-004](test-cases/ORG-LOGO-004.md) | Uploading when the service does not answer shows a message | US-ORG-03 | Error handling | Low |
| [ORG-TEAM-001](test-cases/ORG-TEAM-001.md) | The Team page lists the members with their roles | US-ORG-04 | Happy path | High |
| [ORG-TEAM-002](test-cases/ORG-TEAM-002.md) | The search ignores upper and lower case | US-ORG-04 | Business rule | Medium |
| [ORG-TEAM-003](test-cases/ORG-TEAM-003.md) | A search without results shows a message | US-ORG-04 | UX | Low |
| [ORG-TEAM-004](test-cases/ORG-TEAM-004.md) | An editor cannot open the Team page | US-ORG-04 | Security | High |
| [ORG-TEAM-005](test-cases/ORG-TEAM-005.md) | Loading the team when the service does not answer shows a message | US-ORG-04 | Error handling | Medium |
| [ORG-INVITE-001](test-cases/ORG-INVITE-001.md) | Invite a colleague and see the invitation as pending | US-ORG-05 | Happy path | Critical |
| [ORG-INVITE-002](test-cases/ORG-INVITE-002.md) | Sending an empty invitation shows field messages | US-ORG-05 | Validation | High |
| [ORG-INVITE-003](test-cases/ORG-INVITE-003.md) | An invalid email shows a field message | US-ORG-05 | Validation | Medium |
| [ORG-INVITE-004](test-cases/ORG-INVITE-004.md) | An invited person is not listed as a member yet | US-ORG-05 | Business rule | Medium |
| [ORG-INVITE-005](test-cases/ORG-INVITE-005.md) | Inviting an existing member shows a message | US-ORG-05 | Error handling | Medium |
| [ORG-INVITE-006](test-cases/ORG-INVITE-006.md) | Inviting an email twice shows a message | US-ORG-05 | Error handling | Medium |
| [ORG-INVITE-007](test-cases/ORG-INVITE-007.md) | The invitation dialog starts with the Viewer role | US-ORG-05 | UX | Low |
| [ORG-ROLE-001](test-cases/ORG-ROLE-001.md) | Give a viewer the Editor role | US-ORG-06 | Happy path | High |
| [ORG-ROLE-003](test-cases/ORG-ROLE-003.md) | Lowering your own role asks for confirmation | US-ORG-06 | Business rule | High |
| [ORG-ROLE-004](test-cases/ORG-ROLE-004.md) | Save stays disabled until something changes | US-ORG-06 | UX | Low |
| [ORG-ROLE-005](test-cases/ORG-ROLE-005.md) | The only Admin cannot give up the Admin role | US-ORG-06 | Edge case | High |
| [ORG-ROLE-006](test-cases/ORG-ROLE-006.md) | Saving the role of a removed member shows a message | US-ORG-06 | Error handling | Medium |
| [ORG-REMOVE-001](test-cases/ORG-REMOVE-001.md) | Remove a member from the team | US-ORG-07 | Happy path | High |
| [ORG-REMOVE-002](test-cases/ORG-REMOVE-002.md) | Admin rows have no remove button | US-ORG-07 | Business rule | Medium |
| [ORG-REMOVE-003](test-cases/ORG-REMOVE-003.md) | Cancelling the removal keeps the member | US-ORG-07 | UX | Low |
| [ORG-REMOVE-004](test-cases/ORG-REMOVE-004.md) | Removing a member already removed shows a message | US-ORG-07 | Error handling | Medium |

## Seed Data

- "MyApp Dev Org" has the organization details: Legal Name "MyApp Dev Org S.r.l.", VAT Number "IT00000000000", Fiscal Code "00000000000", Email "dev@myapp.local", Address "Via dello Sviluppo 1", City "Milano", Province "MI", ZIP Code "20100", Country "IT", and no logo.
- "MyApp Dev Org" has the members "Dev User" (Admin, dev@myapp.local), "Dev Member" (Editor, member@myapp.local) and "Dev Viewer" (Viewer, viewer@myapp.local); "Dev User" is its only Admin.
- "MyApp Dev Org" also has three Viewers who never sign in, each removed by one test only: "Spare Member" (ORG-REMOVE-001), "Leaving Member" (ORG-ROLE-006) and "Former Member" (ORG-REMOVE-004).
- "MyApp Dev Org" has no pending invitations at the start.
