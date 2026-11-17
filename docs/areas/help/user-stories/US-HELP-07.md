# US-HELP-07: Download the unanswered questions

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-HELP-04 |
| User input | Yes |

## Story

**As an** Admin
**I want** to download the questions the manual could not answer
**So that** I can see what is missing from the manual and have it written

## Acceptance Criteria

1. Admins see the menu item "Unanswered questions", which opens the page of the same name; other people do not see it.
2. The page has the fields "From" and "To" for the period and a "Download" button.
3. Clicking "Download" gives a file with the questions of the period that were not answered.
4. Each entry shows the question, the date and time, the language, the screen and why it was not answered: "no_match", "refused", "invalid", "not_helpful" or "timeout".
5. In the questions, e-mail addresses are replaced by "[email]", web addresses by "[url]" and numbers of four or more digits by "[number]".
6. Entries show no name of a person and no organization.
7. A period that ends before it starts is refused: the "To" field shows "The end date cannot be before the start date."
8. When the file cannot be prepared, the page shows "The questions could not be downloaded."

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

1. A question is kept with at most 300 characters, after the replacements of criterion 5.
2. A period with no unanswered questions gives a file with no entries.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The file with the questions could not be prepared | "The questions could not be downloaded." | Try again later |

## Non-Goals

- Reading or editing the questions on screen.
- Finding out who asked a question.
- Switching the recording of questions on or off.

## Questions users ask

### en

1. How can I see what people ask that the guide can't answer?
2. Where is the list of things users could not find?
3. Why can't I see this page as an editor?
4. Does the list show who typed each question?

### it

1. Come vedo cosa chiedono gli utenti che la guida non sa spiegare?
2. Dov'è l'elenco delle cose che gli utenti non hanno trovato?
3. Perché non vedo questa pagina come editor?
4. L'elenco mostra chi ha scritto ogni domanda?
