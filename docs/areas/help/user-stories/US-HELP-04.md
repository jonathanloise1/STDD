# US-HELP-04: Ask a question about the application

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-HELP-03 |
| User input | Yes |

## Story

**As a** team member
**I want** to type a question in my own words in the help panel
**So that** I see the parts of the manual that answer it

## Acceptance Criteria

1. The "Ask" tab of the help panel has a "Your question" field and an "Ask" button.
2. The "Your question" field accepts at most 300 characters; further typing is ignored.
3. Clicking "Ask" shows at once up to three cards, each with the page title, the section heading, the first 160 characters and an "Open in the manual" link to that section.
4. When no short answer is given, the cards are headed "Best matches in the manual" and nothing suggests that an assistant answered.
5. The cards come from the whole manual, not only from the pages of the current screen.
6. A question about something the application does not do shows "The manual has nothing on this yet." with the links "Go to the manual home" and "Search the manual for this".
7. The "Search the manual for this" link opens the search results for the words of the question.
8. The panel works with no download on the device; when the help cannot be loaded it shows "Help is unavailable."

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. The panel shows at most three cards for a question.
2. A question outside the scope of the application always gets the same fixed reply.
3. The question is looked up in the manual of the language of the application.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The help content could not be loaded when the question was asked | "Help is unavailable." | Open the manual later |

## Non-Goals

- Chatting: each question is answered on its own, without memory of the previous ones.
- Answering about the user's own data, such as the tasks they created.
- Sending the question to a person.

## Questions users ask

### en

1. Can I just type what I need and get the right instructions?
2. I asked about something the app can't do and got a standard sentence, why?
3. Is there a limit to how long my question can be?
4. Why do I get suggestions from other screens too?

### it

1. Posso scrivere quello che mi serve e ricevere le istruzioni giuste?
2. Ho chiesto una cosa che l'app non fa e mi è comparsa una frase fissa, perché?
3. C'è un limite alla lunghezza della domanda?
4. Perché mi vengono suggerite anche parti di altre schermate?
