---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-05
source: docs/areas/authentication/user-stories/US-AUTH-05.md
source_hash: a146e911e350f8f6dbe2acf6afe798c48e839b56
steps_from: AUTH-INVITE-001
steps_hash: 6a04473cd19dccf104fd4e9e7c5c582edd116155
routes: [/dashboard]
contexts: []
screenshots: []
---

# Join an organization you were invited to

If someone invited you to their organization, you join it by signing in. There is nothing to accept.

## Before you start

- Your email address must be the one the invitation was sent to.

## Steps

1. Open the address of the app in your browser. The sign-in page opens.
2. Sign in with the email address of the invitation and the code you receive.

## What you will see

- The Dashboard page of the organization opens and the header shows its name.
- The "Set Up Your Organization" page is not shown.
- You have the role given in the invitation. An Editor sees the "New Task" button on the Tasks page but no "Settings" menu entry.

## Good to know

- The invitation is accepted by your first sign-in with the invited email address, whether you are new or already have an account.
- An Admin of the organization sends the invitation and chooses your role. You cannot decline an invitation.

## Related pages

- [Sign in with your email address](US-AUTH-01.md)
- [Start using the app on first sign-in](US-AUTH-04.md)
- [Open only the pages your role allows](US-AUTH-07.md)
