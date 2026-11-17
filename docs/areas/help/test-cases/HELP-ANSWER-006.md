# HELP-ANSWER-006: A download in progress is shown while sections answer

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/HelpAnswer006Tests |

## Description

While the assistant is prepared on the device, the panel shows the progress and the section cards.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The device can run the assistant and the download is at 30 of 120 MB.

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

## Expected Results

- The message "Preparing the assistant on this device (30 of 120 MB)" appears.
- The section cards are shown under the header "Best matches in the manual".
