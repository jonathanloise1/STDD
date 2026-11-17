# US-HELP-06: Say whether the answer helped

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-HELP-03 |
| User input | No |

## Story

**As a** team member
**I want** to tell the help whether it solved my problem
**So that** the manual gets better where it falls short

## Acceptance Criteria

1. Under every result of the "Ask" tab, the panel asks "Did this help?" with the buttons "Yes" and "No".
2. The question is also shown under the fixed reply "The manual has nothing on this yet."
3. Clicking "Yes" or "No" shows the message "Thanks for your feedback."
4. Clicking "No" does not change what the panel shows.
5. The administrators of the organization can see the question after a "No" in the list of unanswered questions.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. A "No" makes the question available to the administrators, without the name of the person who asked it.
2. A "Yes" is not passed on to anyone.

## Error States

None.

## Non-Goals

- Adding a comment to the vote.
- Changing the vote after it was given.
- Getting a reply from the administrators.

## Questions users ask

### en

1. How do I tell you that the answer was no use?
2. Does anyone read it when I say it didn't help?
3. Will the administrators know my name if I say no?
4. I pressed the wrong button, can I change it?

### it

1. Come segnalo che la risposta non mi è servita?
2. Qualcuno legge quando dico che non è stata utile?
3. Gli amministratori sapranno il mio nome se rispondo di no?
4. Ho premuto il pulsante sbagliato, posso cambiarlo?
