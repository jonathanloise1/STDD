# Help - User Stories

## Overview

The Help area lets signed-in users learn the application without leaving it: they read and search the manual, open the help of the screen they are on and ask questions in their own words. Admins also download the questions the manual could not answer, to see what is missing from it.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-HELP-01](user-stories/US-HELP-01.md) | Read the manual | High |
| [US-HELP-02](user-stories/US-HELP-02.md) | Search the manual | Medium |
| [US-HELP-03](user-stories/US-HELP-03.md) | Open help for the screen I am on | High |
| [US-HELP-04](user-stories/US-HELP-04.md) | Ask a question about the application | Critical |
| [US-HELP-05](user-stories/US-HELP-05.md) | Get a short answer with its source | High |
| [US-HELP-06](user-stories/US-HELP-06.md) | Say whether the answer helped | Medium |
| [US-HELP-07](user-stories/US-HELP-07.md) | Download the unanswered questions | Medium |

## Business Rules

1. The manual, the help panel and the assistant use the language of the application.
2. Only signed-in users can use them; a visitor is asked to sign in first.
3. Opening or closing the help panel never changes the screen behind it or what the user typed there.
4. Every written answer shows its source; when no answer can be given, the user still gets the closest sections of the manual.
5. A question the manual could not answer, or that the user rated "No", is kept for the Admins without the name of the person or the organization.

## UI Flow

1. Any screen, "Help" button: opens the help panel on the tab "This page"; the tab "Ask" is next to it.
2. Help panel, "This page": lists the manual pages of the screen; clicking one opens it on the Manual page.
3. Help panel, "Ask": the user types a question and clicks "Ask"; the cards, or the fixed reply, appear in the panel with "Did this help?" below.
4. Manual page: the home page lists the areas; an area page lists its pages; a page shows its sections; the "Search the manual" field opens the search results.
5. Search results: clicking a result opens its page; with no result the user can return to the manual home.
6. Unanswered questions (Admin): the user sets "From" and "To", clicks "Download" and receives the file.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Open Questions

1. How does a user reach the Manual page directly? The specification defines the pages of the manual and the links from the panel but no menu entry (US-HELP-01).
2. Which forms and dialogs have their own "Help" button is a project choice; this area assumes the "New Task" form has one (US-HELP-03).
3. The panel opens on "This page"; the specification does not say which tab opens first (US-HELP-03).
4. When a question is outside the application, the specification does not say whether the section cards stay visible next to the fixed reply; this area shows the reply only (US-HELP-04).
5. The labels "Your question", "Search the manual", "Search", "Nothing found in the manual.", "Go to the manual home", "Search the manual for this", "Help is unavailable.", "Open the manual", "The manual could not be loaded.", "Thanks for your feedback.", "Unanswered questions", "From", "To", "Download", "The end date cannot be before the start date." and "The questions could not be downloaded." are proposed here and must be confirmed (all stories).
6. How long unanswered questions are kept (90 days unless changed) and whether recording can be switched off are settings of the product, not of this area (US-HELP-07).

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
