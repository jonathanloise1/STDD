# HELP-ASK-001: A question shows the matching sections of the manual

| Field | Value |
|-------|-------|
| User Story | US-HELP-04 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/HelpAsk001Tests |

## Description

A question typed in the Ask tab shows cards with the best matching sections at once.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- No assistant writes answers.

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

- The header "Best matches in the manual" appears.
- At most three cards are shown.
- The first card shows the page title "Create a task", a section heading and a short text.
- Each card has an "Open in the manual" link.
- No card is labelled "Answer from the manual".
