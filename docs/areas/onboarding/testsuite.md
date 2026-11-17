# Onboarding - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [ONBOARD-START-001](test-cases/ONBOARD-START-001.md) | A user without an organization is taken to the setup page | US-ONBOARD-01 | Happy path | High |
| [ONBOARD-START-002](test-cases/ONBOARD-START-002.md) | A member of an organization is not sent to the setup page | US-ONBOARD-01 | Business rule | High |
| [ONBOARD-START-003](test-cases/ONBOARD-START-003.md) | A visitor cannot open the setup page | US-ONBOARD-01 | Security | High |
| [ONBOARD-START-004](test-cases/ONBOARD-START-004.md) | Any page opened without an organization shows the setup page | US-ONBOARD-01 | Business rule | Medium |
| [ONBOARD-WIZ-001](test-cases/ONBOARD-WIZ-001.md) | Submitting the details creates the organization | US-ONBOARD-02 | Happy path | High |
| [ONBOARD-WIZ-002](test-cases/ONBOARD-WIZ-002.md) | Submitting without a name shows a message | US-ONBOARD-02 | Validation | High |
| [ONBOARD-WIZ-003](test-cases/ONBOARD-WIZ-003.md) | A name made only of spaces counts as empty | US-ONBOARD-02 | Business rule | Medium |
| [ONBOARD-WIZ-004](test-cases/ONBOARD-WIZ-004.md) | Creating when the service does not answer shows a message | US-ONBOARD-02 | Error handling | High |

## Seed Data

- "MyApp Dev Org" has an Admin, an Editor and a Viewer.
- "New User" (new@myapp.test) belongs to no organization; no test creates an organization with this user.
- "Setup User" (setup@myapp.test) belongs to no organization and is used only by ONBOARD-WIZ-001, which makes them the Admin of "Globex Corp".
- No organization named "Globex Corp" or "Initech" exists before the tests run.
