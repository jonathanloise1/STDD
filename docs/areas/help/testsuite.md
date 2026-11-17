# Help - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [HELP-BROWSE-001](test-cases/HELP-BROWSE-001.md) | Open the manual and read a page | US-HELP-01 | Happy path | High |
| [HELP-BROWSE-002](test-cases/HELP-BROWSE-002.md) | The pages of an area follow the order of the stories | US-HELP-01 | Business rule | Medium |
| [HELP-BROWSE-003](test-cases/HELP-BROWSE-003.md) | The manual follows the language of the application | US-HELP-01 | Business rule | Medium |
| [HELP-BROWSE-004](test-cases/HELP-BROWSE-004.md) | A visitor cannot read the manual | US-HELP-01 | Security | High |
| [HELP-BROWSE-005](test-cases/HELP-BROWSE-005.md) | The manual shows a message when its content cannot be loaded | US-HELP-01 | Error handling | High |
| [HELP-SEARCH-001](test-cases/HELP-SEARCH-001.md) | Searching shows the matching sections of the manual | US-HELP-02 | Happy path | Medium |
| [HELP-SEARCH-002](test-cases/HELP-SEARCH-002.md) | A search finds other forms of the same word | US-HELP-02 | Business rule | Medium |
| [HELP-SEARCH-003](test-cases/HELP-SEARCH-003.md) | The results show at most ten sections | US-HELP-02 | Business rule | Low |
| [HELP-SEARCH-004](test-cases/HELP-SEARCH-004.md) | A search with no match shows a message and a link | US-HELP-02 | UX | Low |
| [HELP-SEARCH-005](test-cases/HELP-SEARCH-005.md) | A visitor cannot search the manual | US-HELP-02 | Security | High |
| [HELP-SEARCH-006](test-cases/HELP-SEARCH-006.md) | A search shows a message when the manual cannot be loaded | US-HELP-02 | Error handling | High |
| [HELP-OPEN-001](test-cases/HELP-OPEN-001.md) | The Help button opens the pages of the current screen | US-HELP-03 | Happy path | High |
| [HELP-OPEN-002](test-cases/HELP-OPEN-002.md) | Clicking a page in the panel opens it in the manual | US-HELP-03 | Happy path | Medium |
| [HELP-OPEN-003](test-cases/HELP-OPEN-003.md) | The page about a form comes first in the panel | US-HELP-03 | Business rule | Medium |
| [HELP-OPEN-004](test-cases/HELP-OPEN-004.md) | A screen without a manual page shows the manual home | US-HELP-03 | Business rule | Medium |
| [HELP-OPEN-005](test-cases/HELP-OPEN-005.md) | Every screen has a Help button in its header | US-HELP-03 | UX | Medium |
| [HELP-OPEN-006](test-cases/HELP-OPEN-006.md) | Closing the panel returns to the unchanged screen | US-HELP-03 | UX | Low |
| [HELP-OPEN-007](test-cases/HELP-OPEN-007.md) | A visitor does not get the Help button | US-HELP-03 | Security | High |
| [HELP-OPEN-008](test-cases/HELP-OPEN-008.md) | The panel shows a message when the help cannot be loaded | US-HELP-03 | Error handling | High |
| [HELP-ASK-001](test-cases/HELP-ASK-001.md) | A question shows the matching sections of the manual | US-HELP-04 | Happy path | Critical |
| [HELP-ASK-002](test-cases/HELP-ASK-002.md) | The question field accepts at most 300 characters | US-HELP-04 | Validation | High |
| [HELP-ASK-003](test-cases/HELP-ASK-003.md) | A question outside the application gets a fixed reply | US-HELP-04 | Business rule | High |
| [HELP-ASK-004](test-cases/HELP-ASK-004.md) | The matches come from screens other than the current one | US-HELP-04 | Business rule | Medium |
| [HELP-ASK-005](test-cases/HELP-ASK-005.md) | A question shows at most three cards | US-HELP-04 | Business rule | Medium |
| [HELP-ASK-006](test-cases/HELP-ASK-006.md) | The fixed reply links to a search for the same words | US-HELP-04 | UX | Low |
| [HELP-ASK-007](test-cases/HELP-ASK-007.md) | A visitor cannot ask a question | US-HELP-04 | Security | High |
| [HELP-ASK-008](test-cases/HELP-ASK-008.md) | Asking shows a message when the help cannot be loaded | US-HELP-04 | Error handling | High |
| [HELP-ANSWER-001](test-cases/HELP-ANSWER-001.md) | An answer shows its source and where it was written | US-HELP-05 | Happy path | High |
| [HELP-ANSWER-002](test-cases/HELP-ANSWER-002.md) | A source link opens the cited section | US-HELP-05 | Business rule | Medium |
| [HELP-ANSWER-003](test-cases/HELP-ANSWER-003.md) | An answer without a source is never shown | US-HELP-05 | Business rule | High |
| [HELP-ANSWER-004](test-cases/HELP-ANSWER-004.md) | A declined answer shows the closest sections | US-HELP-05 | Edge case | High |
| [HELP-ANSWER-005](test-cases/HELP-ANSWER-005.md) | A slow answer shows the best matches instead | US-HELP-05 | Edge case | High |
| [HELP-ANSWER-006](test-cases/HELP-ANSWER-006.md) | A download in progress is shown while sections answer | US-HELP-05 | UX | Low |
| [HELP-ANSWER-007](test-cases/HELP-ANSWER-007.md) | Declining the large download keeps the section cards | US-HELP-05 | UX | Medium |
| [HELP-ANSWER-008](test-cases/HELP-ANSWER-008.md) | A visitor cannot reach the assistant | US-HELP-05 | Security | High |
| [HELP-RATE-001](test-cases/HELP-RATE-001.md) | Saying yes shows a thank-you message | US-HELP-06 | Happy path | Medium |
| [HELP-RATE-002](test-cases/HELP-RATE-002.md) | A no reaches the administrators without a name | US-HELP-06 | Business rule | High |
| [HELP-RATE-003](test-cases/HELP-RATE-003.md) | A yes is not passed on to the administrators | US-HELP-06 | Business rule | Medium |
| [HELP-RATE-004](test-cases/HELP-RATE-004.md) | The fixed reply also asks whether it helped | US-HELP-06 | UX | Low |
| [HELP-RATE-005](test-cases/HELP-RATE-005.md) | A visitor cannot rate an answer | US-HELP-06 | Security | High |
| [HELP-DOWNLOAD-001](test-cases/HELP-DOWNLOAD-001.md) | An Admin downloads a question the manual could not answer | US-HELP-07 | Happy path | Medium |
| [HELP-DOWNLOAD-002](test-cases/HELP-DOWNLOAD-002.md) | A period ending before it starts shows a field message | US-HELP-07 | Validation | Medium |
| [HELP-DOWNLOAD-003](test-cases/HELP-DOWNLOAD-003.md) | E-mail addresses, web addresses and numbers are replaced | US-HELP-07 | Business rule | High |
| [HELP-DOWNLOAD-004](test-cases/HELP-DOWNLOAD-004.md) | The downloaded entries show no person and no organization | US-HELP-07 | Business rule | High |
| [HELP-DOWNLOAD-005](test-cases/HELP-DOWNLOAD-005.md) | A period with no questions gives a file with no entries | US-HELP-07 | UX | Low |
| [HELP-DOWNLOAD-006](test-cases/HELP-DOWNLOAD-006.md) | A visitor cannot open the unanswered questions | US-HELP-07 | Security | High |
| [HELP-DOWNLOAD-007](test-cases/HELP-DOWNLOAD-007.md) | An editor cannot see or open the unanswered questions | US-HELP-07 | Security | High |
| [HELP-DOWNLOAD-008](test-cases/HELP-DOWNLOAD-008.md) | A viewer cannot see or open the unanswered questions | US-HELP-07 | Security | High |
| [HELP-DOWNLOAD-009](test-cases/HELP-DOWNLOAD-009.md) | A failed download shows a message | US-HELP-07 | Error handling | High |

## Seed Data

- "MyApp Dev Org" has the members "Dev User" (Admin), "Dev Member" (Editor) and "Dev Viewer" (Viewer); the test cases use them as Admin, Editor and Viewer.
- The manual of the application is built before the run, in English and Italian. It has the home page, an area page for each area and a page for each story. The Tasks area has the area page "Tasks" (in Italian "Attività") and the pages "Create a task", "Edit a task", "Change the status of a task", "Delete a task" and "See the list of tasks", in this order; "Create a task" has the sections "Steps" and "What you will see".
- More than ten sections of the manual contain the word "task". No section of the manual mentions paying invoices, emails on assignment or booking rooms, so questions about them are outside the manual.
- The Tasks page has the manual pages of the Tasks area. The "New Task" form has its own "Help" button, and its own page is "Create a task". The Billing page has no manual page and no area page, so its help falls back to the manual home.
- By default no assistant writes answers: the panel shows the best matches only. A test case that needs an assistant states it in its Preconditions: the seed data provides a stand-in on the server that answers with a given source, answers with no source, declines, or answers too slowly, and a stand-in device that can run the assistant with a first download of 120 MB, either not started or at 30 MB.
- Test cases whose Preconditions say that the help or the manual content cannot be loaded, or that the file with the questions cannot be prepared, use a stand-in that fails on purpose.
- The list of unanswered questions is empty before the run. Test cases add their own questions with distinct wording and never remove entries.
