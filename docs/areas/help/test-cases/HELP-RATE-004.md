# HELP-RATE-004: The fixed reply also asks whether it helped

| Field | Value |
|-------|-------|
| User Story | US-HELP-06 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpRate004Tests |

## Description

Under the fixed reply for an out-of-scope question the panel asks Did this help.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The manual says nothing about paying invoices.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "Can I pay my invoice from here?" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "Can I pay my invoice from here?" in the "Your question" field.
5. Click the "Ask" button.

## Expected Results

- The message "The manual has nothing on this yet." appears.
- Under it the panel asks "Did this help?" with the buttons "Yes" and "No".
