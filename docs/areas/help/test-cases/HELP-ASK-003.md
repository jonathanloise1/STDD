# HELP-ASK-003: A question outside the application gets a fixed reply

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/HelpAsk003Tests |

## Description

A question about something the application does not do shows the fixed reply and two links.

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
- The panel shows the links "Go to the manual home" and "Search the manual for this".
- No answer card is shown.
