# US-HELP-01: Read the manual

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-HELP-01 |
| User input | No |

## Story

**As a** team member
**I want** to read the manual of the application without leaving it
**So that** I can learn how a screen works while I keep my place in the app

## Acceptance Criteria

1. The Manual page opens on the home page of the manual, which lists the areas of the application as links.
2. Clicking an area opens its page, which lists the pages of that area as links.
3. Clicking a page opens it with its title, an introduction and sections such as "Steps" and "What you will see".
4. A link to a section opens the page already scrolled to that section.
5. The manual is shown in the language of the application; changing the language shows the manual in the new language.
6. When the manual cannot be loaded, the page shows "The manual could not be loaded." instead of its content.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. The pages of an area are listed in the order of the stories they describe.
2. The manual is read-only: nobody edits it from the application.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The content of the manual could not be loaded | "The manual could not be loaded." | Reload the page later |

## Non-Goals

- Printing the manual or saving it as a file.
- Linking to websites outside the application.
- Choosing a language for the manual different from the language of the application.

## Questions users ask

### en

1. Where do I find instructions on how the app works?
2. Is there a user guide I can read inside the app?
3. Can I read the instructions in Italian?
4. The guide stays blank, what can I do?

### it

1. Dove trovo le istruzioni per usare l'applicazione?
2. C'è una guida da leggere direttamente qui dentro?
3. Posso leggere le istruzioni in italiano?
4. La guida non si apre e resta vuota, cosa faccio?
