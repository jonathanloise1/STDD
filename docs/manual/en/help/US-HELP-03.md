---
schema_version: 1
kind: story
lang: en
page_id: help/US-HELP-03
source: docs/areas/help/user-stories/US-HELP-03.md
source_hash: 271fa49fbc21a850146efa1abf0493e3a99137a2
steps_from: HELP-OPEN-001
steps_hash: 01de31ede605a68000d5068d7a05efeed88f2311
routes: [/manual]
contexts: []
screenshots: []
---

# Open help for the screen I am on

Open the help that belongs to the screen in front of you, without searching.

## Steps

1. Open the screen you need help with, for example the Tasks page.
2. Click the "Help" button in the page header, the round button with a question mark. A side panel opens next to the screen.
3. Read the list in the tab "This page". The panel opens on this tab. The other tab is "Ask".
4. Click a page in the list to open it in the manual.
5. To go back to your work, close the panel. The screen is still as you left it.

## What you will see

- The tab "This page" lists the manual pages that describe the current screen.
- A screen with no manual page of its own lists the page of its area; when there is none, it lists the manual home page.

## Good to know

- A form or a dialog may have its own "Help" button. The panel then lists the page about that form first, followed by the other pages of the screen.
- The list does not include pages of unrelated screens, except for the fallback to the area or to the manual home.
- The panel opens in the language of the application. It cannot be moved or resized, and it is not shown to people who are not signed in.

## If something goes wrong

| What you see | What to do |
|---|---|
| "Help is unavailable." | Click "Open the manual", or try again later. |

## Related pages

- [Read the manual](US-HELP-01.md)
- [Ask a question about the application](US-HELP-04.md)
