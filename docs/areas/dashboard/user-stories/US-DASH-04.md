# US-DASH-04: Read a greeting on the dashboard

| Field | Value |
|-------|-------|
| Priority | Low |
| Feature | F-DASH-02 |
| User input | No |

## Story

**As a** team member
**I want** to be greeted by name when I open the dashboard
**So that** the page feels personal and I know it is up to date

## Acceptance Criteria

1. The Dashboard page starts with "Good morning" before 12:00, "Good afternoon" from 12:00 to 17:59 and "Good evening" from 18:00, by the time on the user's device.
2. The greeting is followed by a comma and the name of the signed-in user.
3. If the user's name is not known, only the greeting is shown.
4. Under the greeting the page reads "Here is your task overview".
5. Users who use the app in Italian read the greeting and the line below it in Italian.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Business Rules

None.

## Error States

None.

## Non-Goals

- A different greeting on birthdays or holidays.
- Choosing the greeting text.
- Using the time zone of the organization instead of the user's device.

## Questions users ask

### en

1. Why does my home page say good evening when it is still afternoon?
2. Where does the name at the top come from?
3. Can I change the language of the welcome line?

### it

1. Perché la pagina iniziale mi saluta con buonasera se è ancora pomeriggio?
2. Da dove prende il mio nome il saluto in alto?
3. Il saluto può essere in italiano?
