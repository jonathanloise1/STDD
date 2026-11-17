---
schema_version: 1
kind: story
lang: en
page_id: help/US-HELP-05
source: docs/areas/help/user-stories/US-HELP-05.md
source_hash: 44d3b0f2bf945165c19bb6878aa079ddf555b2ac
steps_from: HELP-ANSWER-001
steps_hash: 032b8f40358047457943c07dc079021b895a1318
routes: [/manual]
contexts: []
screenshots: []
---

# Get a short answer with its source

When the assistant can answer, you get a short written answer that tells you where it comes from, so you can act at once and check the source in the manual.

## Steps

1. Open the "Ask" tab of the help panel, type your question in the "Your question" field and click "Ask".
2. Read the card "Answer from the manual". It appears above the section cards and fills in as it is written.
3. Click a source link such as **[1]** to open the section it cites. The cited sections are the first cards.
4. Look at the footer line of the answer: it reads "Answered on this device" or "Answered by the server".

## What you will see

- Every answer shown has at least one source link; an answer without a source is never shown.
- If the assistant declines or its answer cannot be used, the panel shows "The assistant could not answer this from the manual. These sections are the closest:" followed by the section cards.
- If the answer takes too long, the panel shows the section cards headed "Best matches in the manual".

## Good to know

- Before a first large download, the panel asks "Download the assistant to this device ({size} MB)? Answers will then be generated on this device." with the buttons "Download" and "Not now". Nothing large is downloaded without your choice, and "Not now" is remembered. Whether to accept is up to you: without the download you still get the section cards.
- During the download the panel shows "Preparing the assistant on this device ({done} of {total} MB)" and answers meanwhile with the section cards.
- You cannot choose which assistant answers.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The assistant could not answer this from the manual. These sections are the closest:" | Open one of the cards below it. |
| "Best matches in the manual" | The answer took too long. Open one of the cards below. |

## Related pages

- [Ask a question about the application](US-HELP-04.md)
- [Say whether the answer helped](US-HELP-06.md)
