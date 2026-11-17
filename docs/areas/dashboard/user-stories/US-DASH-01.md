# US-DASH-01: See my task counts

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-DASH-01 |
| User input | No |

## Story

**As a** team member
**I want** to see how many tasks my organization has in each status
**So that** I get a quick picture of the workload

## Acceptance Criteria

1. The Dashboard page shows four cards: "Total Tasks", "Todo", "In Progress" and "Done", each with a number.
2. "Total Tasks" counts every task of the organization, however many there are.
3. Each of the other cards counts the tasks that have that status.
4. A spinner is shown in place of the cards while the numbers load.
5. On wide screens the four cards sit in one row; on narrow screens they sit two per row.
6. An organization without tasks shows 0 on every card.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Business Rules

1. Tasks with the status "Cancelled" are counted in "Total Tasks" only.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the counts were loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Choosing a period or a person to count for.
- Opening the task list by clicking a card.

## Questions users ask

### en

1. How many tasks are still open in our team?
2. The numbers at the top of my home page are all zero. Is that right?
3. Why does the total not match the other three numbers?
4. Who can see the overview figures?

### it

1. Quante attività sono ancora aperte nel mio team?
2. Nella pagina iniziale vedo tutti zeri, è giusto?
3. Perché il totale non coincide con la somma degli altri tre numeri?
4. Anche chi può solo leggere vede questi numeri?
