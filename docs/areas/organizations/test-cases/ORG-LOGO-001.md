# ORG-LOGO-001: Upload an image as the organization logo

| Field | Value |
|-------|-------|
| User Story | US-ORG-03 |
| Type | Happy path |
| Priority | Medium |
| Automated by | e2e/OrgLogo001Tests |

## Description

An admin uploads an image and sees it as the logo of the organization.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Logo file | "logo.png" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Upload the file "logo.png" with the "Click to upload logo" button.
4. Click the "Save Changes" button.

## Expected Results

- The message "Logo uploaded successfully" appears and the logo replaces the placeholder.
- After saving, the Organization page shows the logo instead of the first two letters of the name.
