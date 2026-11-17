---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-02
source: docs/areas/authentication/user-stories/US-AUTH-02.md
source_hash: d49206fc7c548268288582d2d18981e0a308786c
steps_from: AUTH-LOGIN-002
steps_hash: aaead0cc145ff94637ff6dfa51246cea51cfc805
routes: [/dashboard]
contexts: []
screenshots: []
---

# Get asked to sign in before using the app

If you are not signed in, the app asks you to sign in first, so that the information of your organization stays private.

## Steps

1. Open a page of the app, for example the Tasks page, while you are not signed in. The sign-in page opens instead.
2. Sign in with your email address and the code you receive.
3. The page you asked for opens.

## What you will see

- Before you sign in the app shows no menu, no organization name and no data.
- After you sign in, the page you asked for opens. If you opened the home address, you see the Dashboard page.

## Good to know

- Every page of the app needs a signed-in user. The sign-in page is the only page a visitor can reach.
- If your role does not allow the page you asked for, the Dashboard page opens with the message "You don't have access to this page."
- There is no public home page and no preview of the app, and the app does not say why the sign-in page opened.

## Related pages

- [Sign in with your email address](US-AUTH-01.md)
- [Open only the pages your role allows](US-AUTH-07.md)
