# ORG-LOGO-002: A file that is not an image is refused

| Field | Value |
|-------|-------|
| User Story | US-ORG-03 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/OrgLogo002Tests |

## Description

A text file chosen as logo is refused with a message.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Logo file | "notes.txt" |

## Steps

1. Click the "Settings" link in the menu.
2. Click the "Edit" button.
3. Upload the file "notes.txt" with the "Click to upload logo" button.

## Expected Results

- The message "Only image files are allowed" appears.
- The logo does not change.
