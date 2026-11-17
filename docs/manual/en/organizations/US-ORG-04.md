---
schema_version: 1
kind: story
lang: en
page_id: organizations/US-ORG-04
source: docs/areas/organizations/user-stories/US-ORG-04.md
source_hash: 7080bb6f1bec9cc6ba5086b3ce948b6fc20d84e8
steps_from: ORG-TEAM-001
steps_hash: 6f2009da8b3e181315ed82a22890f15308af3722
routes: [/settings/team]
contexts: []
screenshots: [images/team-default.png, images/team-list-unavailable.png]
---
# See the team members

See who belongs to your organization and with which role, so you know who can do what.

## Before you start

- Only Admins can do this. Editors and Viewers do not see "Team"; if they open the page by its address, they are taken to the Dashboard with the message "You don't have access to this page."

## Steps

1. In the menu, click "Team".

   ![The Team page with the table of members](images/team-default.png)
2. To find someone, type part of a name or an email in "Search team members...".

## What you will see

- The page "Team Management" followed by the name of the organization.
- The "Members" table with the name, email and role ("Admin", "Editor" or "Viewer") of each member.
- Under "Pending Invitations", the people who were invited and have not signed in yet, with name, email and the date sent.
- When nothing matches your search, the table reads "No team members match your search".
- The back arrow opens the Organization page.

## Good to know

- The search ignores upper and lower case.
- "Members" lists only people who have joined; invited people appear only under "Pending Invitations".
- You cannot sort the table, show it in pages, or resend or cancel an invitation.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The service did not answer. Try again in a few minutes." | Reload the page later. |

![The Team page with a message that the service did not answer](images/team-list-unavailable.png)

## Related pages

- [See the organization details](US-ORG-01.md)
- [Edit the organization details](US-ORG-02.md)
- [Upload the organization logo](US-ORG-03.md)
- [Invite a team member](US-ORG-05.md)
- [Change the role of a team member](US-ORG-06.md)
- [Remove a team member](US-ORG-07.md)
