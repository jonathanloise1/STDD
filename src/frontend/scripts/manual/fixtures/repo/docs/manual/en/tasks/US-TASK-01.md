---
schema_version: 1
kind: story
lang: en
page_id: tasks/US-TASK-01
source: docs/areas/tasks/user-stories/US-TASK-01.md
source_hash: 6962bf3bf2473f120bf647b367136284c23d43fe
steps_from: TASK-CREATE-001
steps_hash: 63a87f0b1479a65ff17efff3adfd9fd3b0413bd8
routes: [/tasks]
contexts: [task-form]
screenshots: [images/task-list-new-task-form-open.png]
---
# Create a task

Use this page to add a task with a title. The team can see what needs to be done.

## Before you start

- Only Admin and Editor can do this.

## Steps

1. Open the Tasks page.
2. Click "New Task".

   ![The New Task form above the list of tasks](images/task-list-new-task-form-open.png)
3. In the "Title" field, type a value such as "Prepare the quarterly report".
4. In the "Status" field, choose "Done".
5. Click "Create Task".

## What you will see

- The form closes.
- The first row of the list shows the new task.

## Good to know

- A new task always starts with status "Todo".
- You cannot choose who the task is assigned to.

## If something goes wrong

| What you see | What to do |
|---|---|
| "Your role does not allow this change." | Ask an admin of the organization for the Editor role. |
| "Enter a title." | Type a title in the **Title** field. |

## Related pages

- [Edit a task](US-TASK-02.md)
