---
schema_version: 1
kind: story
lang: en
page_id: help/US-HELP-07
source: docs/areas/help/user-stories/US-HELP-07.md
source_hash: cf2e7bb74ec63d20dbb21e78af8f92ad5f2d5841
steps_from: HELP-DOWNLOAD-001
steps_hash: 6b1024adc417f42c23d3627d83310efa72005696
routes: [/unanswered-questions]
contexts: []
screenshots: []
---

# Download the unanswered questions

As an Admin, download the questions the manual could not answer, to see what is missing from it and have it written.

## Before you start

- Only Admins see the menu item "Unanswered questions". Editors and Viewers do not.

## Steps

1. Click "Unanswered questions" in the menu. The page of the same name opens.
2. Choose the first day of the period in the "From" field.
3. Choose the last day of the period in the "To" field.
4. Click "Download". You get a file with the questions of the period that were not answered.

## What you will see

- Each entry shows the question, the date and time, the language, the screen where it was asked and why it was not answered.
- A period with no unanswered questions gives a file with no entries.

## Good to know

- The reason of each entry is one of "no_match" (the manual had nothing), "refused" (the assistant declined), "invalid" (the answer could not be used), "not_helpful" (the person pressed "No") or "timeout" (the answer took too long).
- Before a question is kept, e-mail addresses are replaced by "[email]", web addresses by "[url]" and numbers of four or more digits by "[number]". A question is kept with at most 300 characters.
- Entries show no name of a person and no organization, so you cannot find out who asked.
- You cannot read or edit the questions on screen, and you cannot switch the recording of questions on or off.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The end date cannot be before the start date." | The period ends before it starts. Correct the "To" field so that it is not before the "From" field. |
| "The questions could not be downloaded." | Try again later. |

## Related pages

- [Ask a question about the application](US-HELP-04.md)
- [Say whether the answer helped](US-HELP-06.md)
