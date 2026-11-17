# ORG-LOGO-003: An image larger than 5 MB is refused

| Field | Value |
|-------|-------|
| User Story | US-ORG-03 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/OrgLogo003Tests |

## Description

An image bigger than 5 MB chosen as logo is refused with a message.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Logo file | "large-photo.png" (6 MB) |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Upload the file "large-photo.png" with the "Click to upload logo" button.

## Expected Results

- The message "Maximum file size is 5MB" appears.
- The logo does not change.
