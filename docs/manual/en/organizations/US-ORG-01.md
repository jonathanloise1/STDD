---
schema_version: 1
kind: story
lang: en
page_id: organizations/US-ORG-01
source: docs/areas/organizations/user-stories/US-ORG-01.md
source_hash: 9dae24d161400273ea2031c5cab9e960c203386b
steps_from: ORG-VIEW-001
steps_hash: 1ddfd6273c300472b7cad9de533b743820929601
routes: [/settings/organization]
contexts: []
screenshots: [images/organization-default.png, images/organization-unavailable.png]
---
# See the organization details

Check the profile and billing details of your organization, so you know the company data is correct.

## Before you start

- Only Admins can do this. Editors and Viewers do not see "Settings"; if they open the page by its address, they are taken to the Dashboard with the message "You don't have access to this page."

## Steps

1. In the menu, click "Settings".

   ![The Organization page with the billing information](images/organization-default.png)

## What you will see

- The page "Billing Information", with the logo (or the first two letters of the name when there is no logo) and the organization name.
- Under "Fiscal Information": "VAT Number" and "Fiscal Code".
- Under "Billing Address": "Email", "Address", "City" with the province in brackets, "ZIP Code" and "Country".
- A detail that has not been filled in shows "-".
- The "Edit" button, which opens the page where you change the details.

## Good to know

- The page shows the organization currently selected in the header.
- You cannot delete the organization here, see its members (the Team page does that) or manage billing plans and invoices.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The service did not answer. Try again in a few minutes." | Reload the page later. |

![The Organization page with a message that the service did not answer](images/organization-unavailable.png)

## Related pages

- [Edit the organization details](US-ORG-02.md)
- [Upload the organization logo](US-ORG-03.md)
- [See the team members](US-ORG-04.md)
- [Invite a team member](US-ORG-05.md)
- [Change the role of a team member](US-ORG-06.md)
- [Remove a team member](US-ORG-07.md)
