---
schema_version: 1
kind: story
lang: en
page_id: dashboard/US-DASH-01
source: docs/areas/dashboard/user-stories/US-DASH-01.md
source_hash: 2beedcb51de818d0858a86e3a8bda8e6c33c6f7d
steps_from: DASH-KPI-001
steps_hash: c92f39abed3583767d1e39a7b609c22b66530bb3
routes: [/dashboard]
contexts: []
screenshots: [images/dashboard-default.png, images/dashboard-default-mobile.png, images/dashboard-counts-unavailable.png]
---
# See my task counts

See how many tasks your organization has in each status, for a quick picture of the workload.

## Steps

1. In the menu, click "Dashboard". It is also the first page you see after signing in.

   ![The Dashboard page with four cards of task counts](images/dashboard-default.png)

## What you will see

- Four cards: "Total Tasks", "Todo", "In Progress" and "Done", each with a number.
- While the numbers load, a spinner is shown in their place.
- On a wide screen the cards sit in one row; on a narrow screen they sit two per row.
- An organization without tasks shows 0 on every card.

![The Dashboard page on a narrow screen with two cards per row](images/dashboard-default-mobile.png)

## Good to know

- "Total Tasks" counts every task of the organization, including the cancelled ones. Cancelled tasks are not counted on the other cards.
- The numbers always describe the organization currently selected, and everyone in it sees the same figures.
- You cannot choose a period or a person, and clicking a card does not open the task list.

## If something goes wrong

| What you see | What to do |
|---|---|
| "The service did not answer. Try again in a few minutes." | Reload the page later. |

![The Dashboard page with a message that the service did not answer](images/dashboard-counts-unavailable.png)

## Related pages

- [See my latest tasks](US-DASH-02.md)
- [Open the task list from the dashboard](US-DASH-03.md)
- [Read a greeting on the dashboard](US-DASH-04.md)
