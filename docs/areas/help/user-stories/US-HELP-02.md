# US-HELP-02: Search the manual

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-HELP-01 |
| User input | No |

## Story

**As a** team member
**I want** to search the manual for a few words
**So that** I reach the right section without knowing where it is

## Acceptance Criteria

1. The Manual page has a "Search the manual" field and a "Search" button; clicking "Search" opens the search results.
2. The results show at most 10 sections, grouped by page, with the page that matches best first.
3. Each result shows the page title, the section heading and the first 160 characters of the section.
4. The words of the result that match the search are shown in bold, including other forms of the same word.
5. Clicking a result opens that page in the manual.
6. When nothing matches, the page shows "Nothing found in the manual." and a "Go to the manual home" link.
7. The search looks in the manual of the language of the application.
8. When the search cannot be done, the page shows "Help is unavailable."

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. A search for "tasks" also finds sections that say "task".
2. Words that carry no meaning, such as "the" or "a", are ignored in the search.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The content of the manual could not be loaded for the search | "Help is unavailable." | Open the manual later |

## Non-Goals

- Searching the data of the application, such as task titles.
- Filtering results by area.
- Saving or sharing a search.

## Questions users ask

### en

1. How can I look up a word in the guide?
2. I typed a few words and got nothing back, why?
3. Can I find the part about deadlines quickly?
4. Why are some words highlighted in the results?

### it

1. Come cerco una parola nella guida?
2. Ho scritto qualche parola e non trovo niente, come mai?
3. Posso trovare subito la parte che parla delle scadenze?
4. Perché alcune parole sono in grassetto tra i risultati?
