---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-03
source: docs/areas/authentication/user-stories/US-AUTH-03.md
source_hash: 9880d9873bdf2571b31fad74ac8c1242c55845a1
steps_from: AUTH-LOGOUT-001
steps_hash: 0316395cb0cdbe257b2be6af6105428c8fd8ae7a
routes: [/dashboard]
contexts: []
screenshots: [images/header-menus-avatar-menu-open.png, images/header-menus-avatar-menu-open-as-editor.png]
---

# Sign out of the app

Sign out when you finish, so that nobody else can use your account on this device.

## Steps

1. Click the round button with your initials at the top right of any page. A menu opens.

   ![The avatar menu open at the top right, with Settings and Logout](images/header-menus-avatar-menu-open.png)

2. Click "Logout". If you are not an Admin, the menu offers only this entry.

   ![The avatar menu of an Editor, with only Logout](images/header-menus-avatar-menu-open-as-editor.png)

3. The sign-in page opens.

## What you will see

- The sign-in page opens.
- Opening any page of the app now asks you to sign in again.

## Good to know

- Signing out ends the session on this device only. It does not sign you out on your other devices.
- Signing out does not delete your account or the data of your organization.
- The app does not ask you to confirm before signing you out.
- Admins also find "Settings" in this menu.

## Related pages

- [Sign in with your email address](US-AUTH-01.md)
- [Get asked to sign in before using the app](US-AUTH-02.md)
