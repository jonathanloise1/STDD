# HELP-ANSWER-007: Declining the large download keeps the section cards

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | UX |
| Priority | Medium |
| Automated by | e2e/HelpAnswer007Tests |

## Description

Before a large first download the panel asks, and declining still gives the best matches.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The device can run the assistant and the first download is 120 MB.

## Test Data

| Field | Value |
|-------|-------|
| Your question | "How do I create a task" |

## Steps

1. Open the Tasks page.
2. Click the "Help" button.
3. Click the "Ask" tab.
4. Type "How do I create a task" in the "Your question" field.
5. Click the "Ask" button.
6. Click the "Not now" button.

## Expected Results

- The message "Download the assistant to this device (120 MB)? Answers will then be generated on this device." appears with the buttons "Download" and "Not now".
- After "Not now", the section cards are shown under the header "Best matches in the manual".
