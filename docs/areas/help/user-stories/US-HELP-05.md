# US-HELP-05: Get a short answer with its source

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-HELP-03 |
| User input | No |

## Story

**As a** team member
**I want** a short written answer that tells me where it comes from
**So that** I can act at once and check the source in the manual

## Acceptance Criteria

1. When an answer is given, a card labelled "Answer from the manual" appears above the section cards and fills in as it is written.
2. Source numbers in the answer are shown as links "[1]", "[2]" that open the cited sections, and the cited sections are the first cards.
3. A footer line reads "Answered on this device" or "Answered by the server".
4. An answer without a source is never shown.
5. When the assistant declines or its answer cannot be used, the panel shows "The assistant could not answer this from the manual. These sections are the closest:" and the section cards.
6. When the answer takes too long, the panel shows the section cards headed "Best matches in the manual".
7. Before a first large download, the panel asks "Download the assistant to this device ({size} MB)? Answers will then be generated on this device." with "Download" and "Not now"; "Not now" is remembered.
8. During the download the panel shows "Preparing the assistant on this device ({done} of {total} MB)" and answers meanwhile with the section cards.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. Every answer shown has at least one source link.
2. The sentence the assistant uses to decline is never shown; the panel always replaces it with the message of criterion 5.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The assistant declines, or its answer cannot be used | "The assistant could not answer this from the manual. These sections are the closest:" | Open one of the cards below |
| The answer takes too long and no other way of answering is left | "Best matches in the manual" | Open one of the cards below |

## Non-Goals

- Answers without a source.
- Choosing which assistant answers.
- Downloading anything without being asked first when it is large.

## Questions users ask

### en

1. Where does this answer come from, can I check it?
2. The panel just gives me links and no written answer, is it broken?
3. It asks whether to download something to my computer, should I accept?
4. Why does it say the closest sections instead of answering?

### it

1. Da dove viene questa risposta, posso verificarla?
2. Il pannello mi dà solo dei link e nessuna risposta scritta, è guasto?
3. Mi chiede se scaricare qualcosa sul mio computer, devo accettare?
4. Perché dice le sezioni più vicine invece di rispondere?
