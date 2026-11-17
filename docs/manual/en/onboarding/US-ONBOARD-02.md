---
schema_version: 1
kind: story
lang: en
page_id: onboarding/US-ONBOARD-02
source: docs/areas/onboarding/user-stories/US-ONBOARD-02.md
source_hash: f69659acd2437ef3ea0b525a5fe19b6e18c78ad4
steps_from: ONBOARD-WIZ-001
steps_hash: 27584d137e2fee35024622f9b650810e6799acd5
routes: [/onboarding]
contexts: []
screenshots: [images/setup-default.png, images/setup-name-missing.png, images/setup-create-unavailable.png]
---

# Create my organization in the wizard

Enter the basic details of your organization in the wizard to get an organization to work in.

## Before you start

- You belong to no organization yet, so the "Set Up Your Organization" page is open.

## Steps

1. Type the name of your organization in "Organization Name". This is the only required field.

   ![The wizard with the Organization Name, Legal Name, VAT Number and Billing Email fields](images/setup-default.png)

2. If you want, fill in "Legal Name", "VAT Number" and "Billing Email".
3. Click "Submit". If "Organization Name" is empty, the message "Organization name is required" appears and nothing is created.

   ![The wizard with the message under Organization Name saying that the name is required](images/setup-name-missing.png)

4. While the organization is created the page reads "Creating your organization...".
5. The message "Organization created successfully!" appears and the Dashboard page of the new organization opens.

## What you will see

- The header shows the name of your new organization.
- You are the Admin of the new organization and you see "Settings" in the menu.

## Good to know

- A name made only of spaces counts as empty.
- If you leave "Legal Name" empty, it is filled in with the organization name. The country starts as "IT".
- The other fiscal and billing details are completed later in Settings.
- The wizard does not ask for an industry, a template, a language or a fiscal year, and it cannot create a second organization: it is only for people who have none.

## If something goes wrong

| What you see | What to do |
|---|---|
| "Organization name is required" | Type a name that is not only spaces in "Organization Name", then click "Submit" again. |
| "The service did not answer. Try again in a few minutes." | Click "Submit" again later. The wizard keeps what you typed. |

![The wizard with the typed name and an error message saying that the service did not answer](images/setup-create-unavailable.png)

## Related pages

- [Get asked to set up an organization](US-ONBOARD-01.md)
