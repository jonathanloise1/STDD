# Tasks - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [TASK-CREATE-001](test-cases/TASK-CREATE-001.md) | Create a task with a title | US-TASK-01 | Happy path | Critical |
| [TASK-CREATE-002](test-cases/TASK-CREATE-002.md) | Saving without a title shows a field message | US-TASK-01 | Validation | High |
| [TASK-CREATE-003](test-cases/TASK-CREATE-003.md) | A new task starts with status Todo | US-TASK-01 | Business rule | Medium |
| [TASK-CREATE-004](test-cases/TASK-CREATE-004.md) | A title made only of spaces shows a field message | US-TASK-01 | Validation | Medium |
| [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) | Saving after the role became Viewer shows a message | US-TASK-01 | Error handling | High |
| [TASK-CREATE-006](test-cases/TASK-CREATE-006.md) | Cancelling closes the form without adding a task | US-TASK-01 | UX | Low |
| [TASK-CREATE-007](test-cases/TASK-CREATE-007.md) | A viewer cannot create tasks | US-TASK-01 | Security | High |
| [TASK-CREATE-008](test-cases/TASK-CREATE-008.md) | Saving when the service does not answer shows a message | US-TASK-01 | Error handling | High |
| [TASK-EDIT-001](test-cases/TASK-EDIT-001.md) | Edit the title and due date of a task | US-TASK-02 | Happy path | High |
| [TASK-EDIT-002](test-cases/TASK-EDIT-002.md) | Updating with an empty title shows a field message | US-TASK-02 | Validation | High |
| [TASK-EDIT-003](test-cases/TASK-EDIT-003.md) | Choosing Cancelled in the form changes the row status | US-TASK-02 | Business rule | Medium |
| [TASK-EDIT-004](test-cases/TASK-EDIT-004.md) | Updating a task deleted by someone else shows a message | US-TASK-02 | Error handling | High |
| [TASK-EDIT-005](test-cases/TASK-EDIT-005.md) | Cancelling an edit keeps the previous values | US-TASK-02 | UX | Low |
| [TASK-EDIT-006](test-cases/TASK-EDIT-006.md) | A viewer cannot edit tasks | US-TASK-02 | Security | High |
| [TASK-EDIT-007](test-cases/TASK-EDIT-007.md) | Updating after the role became Viewer shows a message | US-TASK-02 | Error handling | Medium |
| [TASK-STATUS-001](test-cases/TASK-STATUS-001.md) | Move a task from Todo to In Progress | US-TASK-03 | Happy path | Medium |
| [TASK-STATUS-002](test-cases/TASK-STATUS-002.md) | The status goes round from Todo to Done and back | US-TASK-03 | Business rule | Medium |
| [TASK-STATUS-003](test-cases/TASK-STATUS-003.md) | A cancelled task goes back to Todo | US-TASK-03 | Business rule | Low |
| [TASK-STATUS-004](test-cases/TASK-STATUS-004.md) | Changing the status of a deleted task shows a message | US-TASK-03 | Error handling | Medium |
| [TASK-STATUS-005](test-cases/TASK-STATUS-005.md) | A viewer cannot change the status of a task | US-TASK-03 | Security | High |
| [TASK-STATUS-006](test-cases/TASK-STATUS-006.md) | Changing the status keeps the other details of the task | US-TASK-03 | Business rule | High |
| [TASK-DELETE-001](test-cases/TASK-DELETE-001.md) | Delete a task after confirming | US-TASK-04 | Happy path | High |
| [TASK-DELETE-002](test-cases/TASK-DELETE-002.md) | Deleting a task already deleted shows a message | US-TASK-04 | Error handling | Medium |
| [TASK-DELETE-003](test-cases/TASK-DELETE-003.md) | A viewer cannot delete tasks | US-TASK-04 | Security | High |
| [TASK-DELETE-004](test-cases/TASK-DELETE-004.md) | Cancelling the confirmation keeps the task | US-TASK-04 | UX | Low |
| [TASK-LIST-001](test-cases/TASK-LIST-001.md) | The Tasks page lists the tasks of the organization | US-TASK-05 | Happy path | Critical |
| [TASK-LIST-002](test-cases/TASK-LIST-002.md) | A newly created task is at the top of the list | US-TASK-05 | Business rule | Medium |
| [TASK-LIST-003](test-cases/TASK-LIST-003.md) | A missing due date is shown as a dash | US-TASK-05 | UX | Low |
| [TASK-LIST-004](test-cases/TASK-LIST-004.md) | Loading when the service does not answer shows a message | US-TASK-05 | Error handling | High |
| [TASK-LIST-005](test-cases/TASK-LIST-005.md) | An organization without tasks shows an empty message | US-TASK-05 | UX | Low |

## Seed Data

- "MyApp Dev Org" has the members "Dev User" (Admin), "Dev Member" (Editor) and "Dev Viewer" (Viewer).
- "MyApp Dev Org" has nine demo tasks with a description: "Set up CI/CD pipeline" (Done), "Design database schema" (Done), "Implement user authentication" (In Progress), "Create task management UI" (In Progress), "Write E2E tests for task CRUD" (Todo), "Add audit logging" (Todo), "Set up monitoring dashboard" (Todo, no due date), "Review security configuration" (Todo) and "Cancelled: Migrate to PostgreSQL" (Cancelled, no due date). All were created before the test run; "Set up monitoring dashboard" is the newest of them.
- Tests never rename or delete the demo tasks; they create their own tasks with distinct titles.
- "Empty Org" has one member, "Empty Admin" (Admin), and no tasks.
