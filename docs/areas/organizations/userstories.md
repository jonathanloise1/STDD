# Organizations - User Stories

## Overview

The Organizations area lets the admin of an organization keep its profile and billing details up to date and manage the people who belong to it. Admins invite colleagues, give them a role, and remove them. Editors and Viewers do not use this area and do not see its menu items.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-ORG-01](user-stories/US-ORG-01.md) | See the organization details | High |
| [US-ORG-02](user-stories/US-ORG-02.md) | Edit the organization details | High |
| [US-ORG-03](user-stories/US-ORG-03.md) | Upload the organization logo | Low |
| [US-ORG-04](user-stories/US-ORG-04.md) | See the team members | High |
| [US-ORG-05](user-stories/US-ORG-05.md) | Invite a team member | High |
| [US-ORG-06](user-stories/US-ORG-06.md) | Change the role of a team member | Medium |
| [US-ORG-07](user-stories/US-ORG-07.md) | Remove a team member | Medium |

## Business Rules

1. Every member has one of three roles: "Admin" has full access and manages the organization and its team, "Editor" creates, edits and deletes data, "Viewer" has read-only access.
2. Only Admins see the "Team" and "Settings" menu items and can open the Organization, Update Organization and Team pages. Anyone else who opens one of them by its address is taken to the Dashboard page with the message "You don't have access to this page."
3. The Settings pages and the Team page always show the organization currently selected in the header.
4. Creating an organization belongs to the Onboarding area; choosing the current organization in the header belongs to the Authentication area.

## UI Flow

1. Organization: the "Settings" menu item opens the page with the billing details; the "Edit" button opens the Update Organization page.
2. Update Organization: the admin changes the details or the logo and clicks "Save Changes", which brings back the Organization page; the back arrow returns without saving.
3. Team: the "Team" menu item opens the page with the "Members" table and, when there are any, the "Pending Invitations" table; the back arrow opens the Organization page.
4. Team, Invite Team Member dialog: the "Invite" button opens it; sending or cancelling closes it and the Team page is shown again.
5. Team, Edit Team Member dialog: the "Edit" button of a row opens it; saving or cancelling closes it.
6. Team, Confirm Removal dialog: the "Remove" button of a row opens it; confirming or cancelling closes it.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
