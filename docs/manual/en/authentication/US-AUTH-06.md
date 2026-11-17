---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-06
source: docs/areas/authentication/user-stories/US-AUTH-06.md
source_hash: df80286bb3b4930d14d24fbee6107c4cff9831a3
steps_from: AUTH-NAV-001
steps_hash: f62ee830eb04dbc1e1037cbf468fc74989c21798
routes: [/dashboard]
contexts: []
screenshots: [images/sidebar-menu-as-admin.png, images/not-found-default.png]
---

# Move between the sections of the app

Use the menu on the left to go to the sections of the app. The header shows the organization you are working in.

## Steps

1. Sign in. You land on the Dashboard page.
2. Look at the menu on the left. It lists "Dashboard" and "Tasks"; Admins also see "Team" and "Audit Log", and "Settings" at the bottom.

   ![The menu on the left of the Dashboard page as an Admin sees it, with the Dashboard entry highlighted](images/sidebar-menu-as-admin.png)

3. Click an entry to open its page. The entry of the open page is highlighted.
4. If you belong to several organizations, click the name of the current one in the header and choose another. Every page then shows the data of the chosen organization.
5. If you end up on an address that does not exist, click "Homepage" to go back to the Dashboard page.

   ![The "Page not found" page with the Homepage button](images/not-found-default.png)

## What you will see

- The header of every page shows the name of the current organization.
- Only the entry of the page that is open is highlighted.

## Good to know

- Searching the app from the header and changing the order of the menu entries are not available.

## Related pages

- [Open only the pages your role allows](US-AUTH-07.md)
- [Move between pages with the keyboard](US-AUTH-09.md)
