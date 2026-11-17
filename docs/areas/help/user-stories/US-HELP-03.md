# US-HELP-03: Open help for the screen I am on

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-HELP-02 |
| User input | No |

## Story

**As a** team member
**I want** to open the help that belongs to the screen in front of me
**So that** I get the right instructions without searching

## Acceptance Criteria

1. Every screen of the application has a "Help" button in the page header.
2. Clicking "Help" opens a side panel next to the screen with the tabs "This page" and "Ask"; it opens on "This page".
3. The tab "This page" lists the manual pages that describe the current screen.
4. A screen with no manual page of its own lists the page of its area; when there is none, it lists the manual home page.
5. A form or dialog may have its own "Help" button; the panel then lists the page about that form first, followed by the other pages of the screen.
6. Clicking a page in the list opens it in the manual.
7. Closing the panel returns to the same screen, unchanged.
8. When the help cannot be loaded, the panel shows "Help is unavailable." and an "Open the manual" link.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. The list never mixes in pages of screens that have nothing to do with the current one, except for the fallback of criterion 4.
2. The panel opens in the language of the application.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The help content could not be loaded when the panel opened | "Help is unavailable." | Click "Open the manual" or try again later |

## Non-Goals

- Moving or resizing the panel.
- Help texts that change with the data on the screen.
- Showing the panel to people who are not signed in.

## Questions users ask

### en

1. Where is the button that explains the page I'm looking at?
2. How do I get instructions for this form only?
3. The side panel shows the manual home, why not my page?
4. How do I close the help and go back to my work?

### it

1. Dov'è il pulsante che spiega la pagina che sto guardando?
2. Come ottengo le istruzioni solo per questo modulo?
3. Il pannello mostra la home della guida e non la mia pagina, perché?
4. Come chiudo l'aiuto e torno a lavorare?
