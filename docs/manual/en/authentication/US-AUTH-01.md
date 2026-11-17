---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-01
source: docs/areas/authentication/user-stories/US-AUTH-01.md
source_hash: d65099123badd36430656db598d29bf9d20f2ade
steps_from: AUTH-LOGIN-001
steps_hash: f27ec690c6a9d3d178a125050dfe9fcfbab9408d
routes: [/dashboard]
contexts: []
screenshots: []
---

# Sign in with your email address

Sign in with your email address and a code sent to you. You do not need a password.

## Steps

1. Open the address of the app in your browser. The sign-in page opens.
2. Type your email address and confirm.
3. Open your mailbox, find the message with your code and type the code on the sign-in page.
4. Look at the top right of the page: the round button shows your initials.

## What you will see

- The Dashboard page opens.
- The header shows the name of your organization.
- The button at the top right shows your initials.

## Good to know

- Your email address and a code are the only way in. There are no passwords and no sign-in with another service such as a social account.
- The sign-in page, the time a code stays valid and the message for a wrong code come from the sign-in service, not from the app.
- You stay signed in while you use the app, and a reload does not ask for a new code. If the session can no longer be kept, the app asks you to sign in again.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The service did not answer. Try again in a few minutes." | Wait a few minutes, then reload the page. |

## Related pages

- [Get asked to sign in before using the app](US-AUTH-02.md)
- [Sign out of the app](US-AUTH-03.md)
- [Start using the app on first sign-in](US-AUTH-04.md)
- [Join an organization you were invited to](US-AUTH-05.md)
- [Move between the sections of the app](US-AUTH-06.md)
- [Open only the pages your role allows](US-AUTH-07.md)
- [Change the app language](US-AUTH-08.md)
- [Move between pages with the keyboard](US-AUTH-09.md)
