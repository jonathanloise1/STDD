---
schema_version: 1
kind: story
lang: en
page_id: organizations/US-ORG-02
source: docs/areas/organizations/user-stories/US-ORG-02.md
source_hash: 14f370813fc476914226e7d7ac23f7160a0a15bc
steps_from: ORG-UPDATE-001
steps_hash: 3173f7cf3949ad97dae2b39392ac858b539bb26a
routes: [/settings/organization/edit]
contexts: []
screenshots: [images/update-organization-default.png, images/update-organization-unsaved-changes.png, images/update-organization-required-field-missing.png, images/update-organization-save-unavailable.png]
---
# Edit the organization details

Change the name and the billing details of your organization, so contracts and invoices carry the correct company data.

## Before you start

- Only Admins can do this.

## Steps

1. In the menu, click "Settings".
2. Click "Edit". The "Update Organization" page opens with the current values.

   ![The Update Organization page with the current values](images/update-organization-default.png)
3. Change what you need. Every field is required: "Organization Name", "Legal Name", "VAT Number", "Fiscal Code", "Billing Email", "Street Address", "City", "Province", "ZIP / Postal Code" and "Country".

   ![The page with the badge Unsaved Changes next to the title](images/update-organization-unsaved-changes.png)
4. Click "Save Changes".

## What you will see

- The message "Organization has been updated successfully".
- The Organization page opens with the new values.
- While the values differ from the saved ones, the badge "Unsaved Changes" is shown next to the page title.

## Good to know

- The back arrow returns to the Organization page without saving.
- The application does not check that the VAT number or the fiscal code really exist, and there are no industry templates.

## If something goes wrong

| What you see | What to do |
|---|---|
| "Organization name is required" | Fill in the field under which the message appears. Other required fields show a similar message, such as "Legal name is required". |
| "Please fix validation errors before submitting" | Correct the fields marked in red, then click "Save Changes" again. |
| "Invalid email format" | Type a valid email address in "Billing Email". |
| "ZIP code must be 5 digits" | Type exactly five digits in "ZIP / Postal Code". |
| "The service did not answer. Try again in a few minutes." | Click "Save Changes" again later. The page keeps the values you typed. |

![The Organization Name field marked in red and the warning to fix the errors](images/update-organization-required-field-missing.png)

![The Update Organization page after the service did not answer](images/update-organization-save-unavailable.png)

## Related pages

- [See the organization details](US-ORG-01.md)
- [Upload the organization logo](US-ORG-03.md)
- [See the team members](US-ORG-04.md)
- [Invite a team member](US-ORG-05.md)
- [Change the role of a team member](US-ORG-06.md)
- [Remove a team member](US-ORG-07.md)
