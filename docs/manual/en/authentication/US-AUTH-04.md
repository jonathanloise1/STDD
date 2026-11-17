---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-04
source: docs/areas/authentication/user-stories/US-AUTH-04.md
source_hash: 11c7c7ebb2f5dfccc4b020bbc7a58131b275c8cf
steps_from: AUTH-PROVISION-001
steps_hash: 9216a18b07b8240d5fc16aa33022681f6bae1625
routes: [/onboarding]
contexts: []
screenshots: []
---

# Start using the app on first sign-in

You do not register separately: the first time you sign in with your email address, you can use the app.

## Steps

1. Open the address of the app in your browser. The sign-in page opens.
2. Sign in with your email address and the code you receive. No registration form appears.
3. If you belong to no organization yet, the "Set Up Your Organization" page opens.

## What you will see

- No registration form is shown.
- The "Set Up Your Organization" page opens if you belong to no organization.

## Good to know

- Signing in creates your account; nothing else is needed.
- Signing in again with the same email address opens the same account with the same organizations. You never get a second account.
- The "Set Up Your Organization" page appears only to people who belong to no organization, and it is the only page they can use until they create one.
- A new account starts in English until you choose another language.
- The app does not send a welcome email and does not merge accounts that use different email addresses.

## Related pages

- [Sign in with your email address](US-AUTH-01.md)
- [Join an organization you were invited to](US-AUTH-05.md)
- [Change the app language](US-AUTH-08.md)
