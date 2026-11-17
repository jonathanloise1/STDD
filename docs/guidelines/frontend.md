# MyApp - Frontend Guidelines

> The UI layer of MyApp: stack, folders, theme, building blocks, layout shell and the rules every screen follows.
> This document is the single source of truth for visual and interaction decisions. What a screen does is in
> its stories ([docs/areas](../areas/)); the technical constraints shared with the backend are in
> [architecture.md](architecture.md).

---

## 1. Principles

### 1.1 The user

A member of a small team who keeps a shared list of tasks. They use a desktop and a phone, work in English or
Italian, and expect to be productive without a tutorial.

### 1.2 Principles

| Principle | What it means |
|-----------|---------------|
| Plain Bootstrap first | The UI is Bootstrap 5 with a theme of our own. A screen is built from Bootstrap markup and the building blocks of section 7; nothing else |
| One place per decision | Colours, sizes and icons are tokens of the theme; a page never hard-codes them |
| Mobile-first | Every screen works at 390 px wide; larger screens add space, not features |
| Zero learning curve | Every action is visible and named; nothing is hidden behind gestures |
| Calm | No page transitions, no decorative motion, no hover tricks |
| Accessible by construction | Every control has a role and a name a screen reader and a test can find (section 13) |
| Same words everywhere | Labels and messages are the texts the stories fix, in both languages |

---

## 2. Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| Framework | React 19 | Function components and hooks |
| Language | TypeScript 5, strict | No `any` in new code |
| Build | Vite 6 | Every page lazy-loaded |
| Components | react-bootstrap 2 on Bootstrap 5.3 | Imported only inside `src/ui` (section 3) |
| Styles | Bootstrap SCSS with our own variables and partials | No CSS-in-JS, no CSS modules |
| Icons | lucide-react, through the icon registry | Section 5 |
| Font | Inter, self-hosted from an npm package | No font or icon CDN: the application must work without internet access |
| Messages | react-toastify | Wrapped by `src/ui/feedback` (section 10) |
| Routing | React Router 6 | |
| i18n | i18next | English and Italian (section 14) |
| State | React context, one per domain | No global store |
| Forms | Plain controlled inputs and the field blocks of section 9 | No form library |

Not used, and not to be added without a story that needs it: chart, calendar, rich-text, drag-and-drop or
animation libraries; dark mode; icon fonts.

Every package is under a permissive licence (MIT, ISC, BSD, Apache-2.0; OFL for fonts) and is listed in the
third-party notices at the repository root.

---

## 3. Folders and conventions

### 3.1 Folders

```
src/
  ui/                    The UI layer. Knows nothing of tasks, teams or roles
    theme/               Tokens, Bootstrap import, styles of the blocks and of the shell, fonts
    icons/               The icon registry and the Icon block
    components/          Building blocks: buttons, cards, fields, dialogs, menus, side panel, badges
    feedback/            Toast host, notify, error message with reference, loading blocks, notice
    layout/              Page and PageHeader
    index.ts             The only import path for code outside src/ui
  shell/                 The application frame: shell, header, organization switcher, language and user
                         menus, side navigation, bottom navigation
  pages/company/{area}/  One folder per area; pages and the dialogs only they use
  help/                  Manual, help panel and assistant
  contexts/              Auth, Permission, Organization, Help
  services/api/          API clients
  hooks/                 Application hooks (navigation entries, keyboard shortcuts, help data)
  routes/                Route definitions
  config/                Runtime configuration and sign-in configuration
```

- Code outside `src/ui` imports UI from `src/ui` only: never from `react-bootstrap`, `lucide-react` or
  `react-toastify` directly. Changing a library then touches one folder.
- `src/ui` imports nothing from `pages`, `shell`, `contexts` or `help`. Its one dependency on the application is
  the error shape of a failed request (`services/api/apiProblem`), used by the error message block.
- Text shown by a block comes from its props. The few words a block owns ("Close", "Loading", "Back",
  "Copy reference") come from the `common` namespace.

### 3.2 Naming

| Element | Convention | Example |
|---------|------------|---------|
| Component file and export | PascalCase | `TaskListPage.tsx` |
| Page | PascalCase with a suffix | `TaskListPage`, `DashboardView` |
| Hook | `use` + camelCase | `useNavItems` |
| Context | PascalCase + `Context` | `OrganizationContext` |
| API service | camelCase + `Service` | `taskService` |
| Interface | `I` prefix | `ITask` |
| i18n key | camelCase or dot notation, in English | `tasks.createTask` |
| Route path | kebab-case | `/unanswered-questions` |
| CSS class of our own | kebab-case, BEM for parts | `.app-header__actions` |
| Icon name | PascalCase, a name of the registry | `TaskAlt` |

### 3.3 Component rules

1. One component per file; props typed with an explicit interface.
2. Pages are default exports (for `React.lazy`); blocks are named exports.
3. Components render; requests live in services, shared state in contexts.
4. Story ids go above page components, submit handlers and API functions, in the form of development.md 11.2.
5. Style with Bootstrap utility classes and theme classes. `style={{}}` only for a value computed at run time
   (a width from data), never for a colour, a font or a spacing that is a token.

---

## 4. Theme

### 4.1 How it is built

- One SCSS entry, `src/ui/theme/index.scss`, imported once by the application entry. It declares the Latin subset of Inter itself (`_fonts.scss`), which is all English and Italian need.
- The tokens are Bootstrap variables, set before Bootstrap is imported, so every Bootstrap component and utility
  uses them. Our own values are CSS custom properties prefixed `--app-`.
- Partials of our own (`_shell.scss`, `_blocks.scss`, `_toasts.scss`) use `var(--bs-...)` and `var(--app-...)`,
  never a hex value. No `!important`.
- `$enable-dark-mode: false`: there is no dark mode and no theme switch.

### 4.2 Colours

| Token | Value | Use |
|-------|-------|-----|
| `$primary` | `#1E3A5F` | Primary actions, active menu entry, brand |
| `$secondary` | `#64748B` | Neutral badges and secondary text actions |
| `$info` | `#2D6DB5` | Links, informative states, the "Todo" status |
| `$success` | `#047857` | Success, the "Done" status |
| `$warning` | `#F59E0B` | Attention, the "In Progress" status, unsaved changes; always with dark text |
| `$danger` | `#DC2626` | Errors, destructive actions, the "Cancelled" status |
| `$light` | `#F1F5F9` | Light buttons, table heads |
| `$dark` | `#1A1A2E` | Dark text on light or warning backgrounds (`$color-contrast-dark`) |
| `$body-bg` | `#F7F9FC` | Page background |
| `$body-color` | `#1A1A2E` | Text |
| `$body-secondary-color` | `#6B7280` | Muted text (`text-body-secondary`, `text-muted`) |
| `$border-color` | `#E5E7EB` | Borders and separators |
| `$link-color` | `$info` | Links |
| Surface | `#FFFFFF` | Cards, header, side navigation, dialogs, menus (`$card-bg`, `$modal-content-bg`, `$dropdown-bg`) |
| `--app-accent` | `#00B4D8` | Decoration only: the marker of the active menu entry, the avatar focus ring. Never text |

- Text on a coloured background uses `text-bg-{colour}`, which picks white or dark text by contrast
  (`$min-contrast-ratio: 4.5`). Every pair above reaches 4.5:1.
- Tinted backgrounds use Bootstrap's generated `-subtle` colours (`bg-primary-subtle`, `text-primary-emphasis`).
- Task status: Todo `info`, In Progress `warning`, Done `success`, Cancelled `danger`. The colour is never the
  only signal: the badge always carries the status name.

### 4.3 Typography

| Element | Rule |
|---------|------|
| Family | `'Inter Variable', system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif`, weights 400 to 700 |
| Base size | `0.9375rem` (15 px), line height 1.5 |
| Page title | One `h1` per page, styled `h4`, weight 700 |
| Section and card title | `h2`, styled `h6`, weight 600 |
| Dialog title | `h2`, styled `h5` |
| Count on a dashboard card | `display-6`, weight 700 |
| Small text, captions, hints | `small` (about 13 px) |
| Numbers in tables, counts, dates | `font-variant-numeric: tabular-nums` (class `app-tabular`) |

### 4.4 Spacing, radius, shadow

| Token | Value |
|-------|-------|
| `$spacer` | `1rem`; use the Bootstrap scale 0 to 5 through utilities (`p-3`, `gap-2`) |
| Grid gutter | `g-3` |
| Card padding | `1rem`, `1.5rem` from `md` |
| Page side padding | `1rem`, `1.5rem` from `md` |
| `$border-radius` | `0.5rem`: buttons, inputs, menus |
| `$border-radius-lg` | `0.75rem`: cards, dialogs |
| `$border-radius-sm` | `0.375rem`: small buttons |
| Badges | pill |
| `$box-shadow-sm` | `0 1px 2px rgba(16, 24, 40, 0.06)`: cards, together with a 1 px border |
| `$box-shadow` | `0 8px 24px rgba(16, 24, 40, 0.12)`: menus, dialogs, side panel, toasts |

### 4.5 Layout sizes and stacking

| Token | Value |
|-------|-------|
| `--app-header-height` | `52px`, `56px` from `md` |
| `--app-sidenav-width` | `60px` collapsed, `220px` expanded or pinned |
| `--app-bottomnav-height` | `56px` plus the bottom safe area |
| Help panel width | `min(400px, 100vw)` |

Stacking follows the Bootstrap scale: page header and app header `$zindex-sticky` (1020), side and bottom
navigation `$zindex-fixed` (1030), side panel 1045, dialog 1055, toasts 1090.

### 4.6 Motion

- `$enable-transitions: false`: buttons, menus, dialogs and the side panel change state at once.
- Dialogs open without the fade (`animation={false}`); pages appear without a transition.
- The only motion left: spinners, and the slide-in of a toast (a CSS animation of the toast library).
- `$enable-reduced-motion: true`. Choice: animations driven by script clocks stop under the fixed clock of the
  manual capture and leave blank screenshots; a still interface also reads faster.

---

## 5. Icons

- One set: lucide-react (ISC), imported icon by icon so the bundle holds only the icons used.
- `src/ui/icons/registry.ts` maps our names (PascalCase: `TaskAlt`, `Edit`, `Delete`) to the icons of the set.
  The registry is the list of icons the application may use; a new icon is one line there. Code names icons by
  registry name only, so the set can be replaced in one file.
- `<Icon icon='Edit' size='lg' />`. Sizes are relative to the text: `sm` 1em, `md` 1.25em (default), `lg` 1.5em,
  `2x` 2em, `3x` 3em, `5x` 5em. Colour comes from the text colour (`className='text-primary'`).
- An icon is decorative: it renders `aria-hidden="true"`, adds no text to the accessible name of its parent and
  has no `title`. The meaning is carried by the visible text or by the `label` of the icon button that holds it.
- Languages are shown by their name, never by a flag.

---

## 6. Layout shell

### 6.1 Regions

Inside the root element `.app`, in this DOM order:

| Order | Region | Element | Shown |
|-------|--------|---------|-------|
| 1 | Side navigation | `nav.slim-sidebar` | From `md`, to a member of an organization |
| 2 | Content column | `div.app-shell__main`, offset by `--app-sidenav-width` from `md` | Always |
| 2.1 | Header | `header.app-header`, sticky at the top | Always |
| 2.2 | Content | `main.app-content`, holding the page | Always |
| 3 | Bottom navigation | `nav.bottom-tab-bar` | Below `md`, to a member of an organization |
| 4 | Help panel, keyboard shortcuts dialog | Portals | When opened |

The toast container is rendered by the application root next to `.app`, so messages show even when the
shell is not (a failed start). The side navigation is the first `nav` element of the page; no other `nav`
(a breadcrumb, a pager) may come before it.

A user who belongs to no organization sees the header and the setup page, and no navigation.

### 6.2 Header

`header.app-header`, surface background, bottom border, height `--app-header-height`.

| Side | Content |
|------|---------|
| Left | The current organization: its name with a building icon; for a member of several organizations the name is a button that opens the list, headed "Switch organization", the current one marked with a check |
| Right | "Help" icon button; the language button; the avatar button |

- Language button: globe icon and the code of the current language ("EN", "IT"); accessible name "Change
  language", translated; test id `btn-language-selector`. Its menu lists "English" and "Italiano", always in
  their own language. Choosing one switches the language at once, closes the menu, shows "Language changed to
  {language}" and then saves the choice; when saving fails an error message follows and the language stays
  (US-AUTH-08).
- Avatar button: the initials of the user on a primary circle; accessible name the full name of the user (the
  email when there is no name). Its menu: "Settings" for Admins, then "Logout" (test id `btn-logout`).
- Header buttons are at least 40 x 40 px. Shown at every screen size; the organization name truncates first.
- No search in the header.

### 6.3 Side navigation (from `md`)

- Fixed on the left, full height, surface background, right border.
- Collapsed: `60px`, icons only, the label in a native `title`. Expanded to `220px` with labels while the pointer
  is over it or keyboard focus is inside it, overlaying the content. The pin button ("Pin menu", "Unpin menu")
  keeps it expanded and moves the content; the choice is kept on the device.
- Brand row on top (shield icon, and "MyApp" when expanded), then the entries, then the bottom group.
- Entries, from the role (US-AUTH-06, US-AUTH-07): Dashboard, Tasks, Manual for everyone; Team, Audit Log,
  Unanswered questions for Admins; Settings, at the bottom, for Admins.
- Each entry is a link with id `sidebar-{id}`, `aria-label` equal to its label, and `aria-current="page"` when
  its page is open. The active entry has primary text on `primary-subtle` and the accent marker on its left.

### 6.4 Bottom navigation (below `md`)

- Fixed at the bottom, `--app-bottomnav-height`, padded by the safe area.
- Tabs: the first two entries (Dashboard, Tasks) and "More" (a button with `aria-expanded`). "More" opens a
  bottom side panel with the remaining entries of the role; choosing one closes it.
- The tabs follow the same rules as the side navigation entries (link, `aria-label`, `aria-current`).
- Content keeps a bottom padding of `72px` so nothing hides behind the bar.

### 6.5 Page and page header

- Every page renders `<Page title>`: root element `.page-wrapper`, sets the document title, holds a
  `container-xxl` (or fluid with `fluid`).
- A page with its own toolbar renders `<PageHeader>` first: the `h1`, an optional back icon button ("Back"), an
  optional badge next to the title ("Unsaved Changes"), and the page actions on the right ("Save Changes",
  "Invite"). From `md` the page header sticks under the app header, so its actions stay reachable.
- A page without a toolbar puts its `h1` at the top of its content (Dashboard greeting, setup page).

### 6.6 Help panel

The help button opens the help panel (manual.md 5.4, US-HELP-03), a side panel on the right:

- Role `dialog`, accessible name "Help" from its title, `aria-modal="false"`, no backdrop; the page behind
  stays visible, scrollable and usable.
- Opening moves focus into the panel; Escape or its close button ("Close") closes it and returns focus to the
  button that opened it.
- Two tabs, "This page" and "Ask"; both panels stay mounted, so a question survives a visit to the other tab.

### 6.7 Keyboard shortcuts

"?" opens the "Keyboard Shortcuts" dialog; G then a letter opens a section the role allows (US-AUTH-09). The
shortcuts are ignored while typing in a field and while a dialog is open (an element `.modal.show` exists).

### 6.8 Starting the application

Until the user, the account and the organizations are known, the application shows the loading screen: the
shield logo, breathing slowly, with the alternative text of `common:loading`. A failure to start shows the
error message of section 10.2 in place of the shell.

---

## 7. Building blocks

All exported from `src/ui`. Use the block when one exists; use plain Bootstrap markup (grid, `table`, `badge`
utilities, `list-group`) for the rest. Never re-create a block in a page.

| Block | Use for | Main props | Rules |
|-------|---------|------------|-------|
| `Button` | Every action with a visible label | `variant`, `size`, `icon`, `busy`, `disabled`, `type`, `to` | `type` defaults to `button`. `busy` disables it (native `disabled`) and shows a spinner in place of the icon. `to` renders a router link with role `link` |
| `IconButton` | An action shown as an icon only | `icon`, `label`, `variant`, `busy` | `label` is required: it becomes `aria-label` and `title` |
| `Icon` | Decoration next to text | `icon`, `size`, `className` | Section 5 |
| `Card`, `CardHeader`, `CardBody`, `CardFooter` | A region of a page | `as` (`form`), header `title`, `icon`, `actions`, `titleAs` | Renders `.card`. The title is an `h2` unless `titleAs` says otherwise |
| `Badge` | Status, role, count | `tone`, `children` | Renders `span.badge.text-bg-{tone}`, pill. Always carries text |
| `TextField` | Text, email, date, number, multi-line input | `id`, `label`, `value`, `onChange`, `onBlur`, `type`, `error`, `hint`, `as='textarea'`, `rows`, input attributes | Section 9 |
| `SelectField` | A choice from a list | `id`, `label`, `value`, `onChange`, `options`, `placeholder`, `error`, `hint` | Native `select`; section 9 |
| `Dialog` | A task or a question that needs the user's attention | `show`, `onHide`, `title`, `icon`, `size`, `footer`, `id` | Section 11 |
| `ConfirmDialog` | A yes or no question before an irreversible action | `show`, `title`, `message`, `confirmLabel`, `confirmVariant`, `onConfirm`, `onCancel`, `busy` | Section 11 |
| `SidePanel` | Content next to the screen (help) or a bottom sheet (More) | `show`, `onHide`, `title`, `placement`, `modal`, `id` | Section 11 |
| `Menu`, `Menu.Toggle`, `Menu.Items`, `Menu.Item`, `Menu.Header` | A button that opens a short list of actions | toggle `id`, `aria-label`, `className`; items `icon`, `onSelect`, `active`; `align` | Renders `ul.dropdown-menu`; entries are buttons; the menu closes when an entry is chosen; Escape closes and returns focus |
| `Page`, `PageHeader` | The frame of a page | `title`, `fluid`; header `title`, `back`, `extra`, `actions` | Section 6.5 |
| `Notice` | A static note on a page | `tone`, `icon`, `children` | `div.alert` without `role="alert"`: it is not news |
| `ApiErrorMessage` | A region that could not load or save | `problem` | Section 10.3 |
| `LoadingBlock` | A region waiting for data | `label` | Centered spinner, `role="status"`, visually hidden label |
| `Spinner` | A spinner inside another element | `size` | Decorative, `aria-hidden` |
| `Loading` | The start of the application | | Section 6.8 |
| `EmptyState` | A list with nothing to show | `icon`, `title`, `text`, `action` | Section 8.4 |
| `notify`, `showApiError`, `ToastHost` | Messages after an action | | Section 10 |

Button variants: the theme colours (`primary`, `secondary`, `danger`, `light`), `link`, `outline-{colour}`, and
`subtle-{colour}` (tinted background, coloured text) for row actions and quiet buttons. One primary button per
region.

---

## 8. Page patterns

Every page follows one of these. Do not invent a layout.

### 8.1 List page

- A card with the title and, for roles that may change data, the create button ("New Task") in its header.
- A table where columns carry the content (Tasks, Team, Audit Log); cards otherwise. A table sits in
  `.table-responsive` inside its card, so a phone scrolls the table and not the page.
- Row actions are icon buttons with a label ("Edit", "Delete", "Change status", "Details"), in the last column
  ("Actions"), shown only to roles that may use them.
- Filters sit above the list in their own card; a "Reset" button clears them.
- Paging: "Previous", the position "1 / 3", "Next"; a button that leads nowhere is disabled.

### 8.2 Detail page

- One value per row in a definition list (`dl` of `div` rows, each `dt` then `dd`), grouped under `h3`
  sub-titles.
- "Edit" is a secondary action, shown only to roles that may change the data.

### 8.3 Form page, inline form and form dialog

- Labels above inputs, one column on a phone, two on wider screens where the fields are short.
- The save button is primary; "Cancel" is `light` and never asks for confirmation.
- A page form keeps its save button in the page header (section 6.5) and shows "Unsaved Changes" while the
  values differ from the saved ones.
- Forms and dialogs may carry their own "Help" button (`HelpButton` with a context, manual.md 5.4).

### 8.4 Empty state

An icon (`3x`, muted), the line the story fixes ("No tasks yet") and, for roles that may create, the hint or the
action ("Create your first task to get started."). Never a table with only headers, never an empty card.

### 8.5 Loading state

The page frame, its title and its cards show at once; a region waiting for data shows `LoadingBlock` in place
of its content. Never a blank page, never a spinner over the whole screen after start.

### 8.6 Dashboard

Greeting (`h1`), the overview line, the count cards (two per row on a phone, four from `lg`; each a `group`
named by its label whose first element is the number), then the "Recent Tasks" card with "View All".

### 8.7 Page not found

"404", the `h1` "Page not found" and the "Homepage" link to the Dashboard page (US-AUTH-06).

---

## 9. Forms and field messages

| Rule | Detail |
|------|--------|
| Label | A `label` bound to the control by `for`/`id`. Its text is the field name only, exactly as the story names it: no asterisk, no icon, no hint inside it |
| Order | `label`, control, then the field message as the next element, then the hint. Nothing else between the control and its message |
| Field message | `div.invalid-feedback` with the text the story fixes ("Title is required"); the control gets `is-invalid`, `aria-invalid="true"` and `aria-describedby` pointing at the message |
| Hint | Visible `div.form-text` under the field. No tooltips for hints: they hide on touch screens and from screen readers |
| When | On submit for every field; on blur for a field already touched; a message clears when the value changes |
| Submit | `type="submit"`; while saving it is disabled with the native attribute and shows a spinner; its label may change ("Saving...", "Sending...") |
| Lengths | `maxLength` on the control enforces the limits the stories fix |
| Server errors | A failure of the save is a message (section 10.2), not a field message, unless the story says otherwise |

---

## 10. Messages

### 10.1 Toasts

| Kind | Call | Lifetime |
|------|------|----------|
| Success ("Organization has been updated successfully", "Language changed to English") | `notify.success(text)` | Closes after 5 s |
| Information, warning | `notify.info(text)`, `notify.warning(text)` | Closes after 5 s |
| A check made in the browser ("Maximum file size is 5MB") | `notify.error(text)` | Closes after 5 s |
| Error of a request | `showApiError(problem)` | Stays until the user closes it; a click on it does not close it |

- One container, bottom right, above everything else (`z-index` 1090). It never covers the header or the page
  header, where the actions are. Below `md` it sits above the bottom navigation.
- Each toast has `role="alert"`; its close button is named "Close".
- A toast slides in from the right; the theme gives it the surface background, a coloured left border by kind,
  the radius and the shadow of section 4.4.

### 10.2 Error of a request

Every message about a failed request shows (operations.md 4.3):

1. the translated message for the code of the problem;
2. under it, in `div.small.text-muted`, a `span` holding only "Reference: {id}", followed by the icon button
   "Copy reference", which copies the id and shows a check for 2 s.

The same body is used in a toast and in an inline error. The `span` holds nothing but the reference, because the
manual capture masks it.

### 10.3 Inline error

A region whose data could not be loaded shows `ApiErrorMessage` in place of its content: `div.alert.alert-danger`
with `role="alert"` and the body of 10.2. The rest of the page keeps working.

### 10.4 Notices and page alerts

- A static note on a page is a `Notice` with no live role.
- A message that results from navigation (the Dashboard page after a refused page: "You don't have access to
  this page.") is a `div.alert.alert-warning` with `role="alert"` and a close button named "Close".

---

## 11. Dialogs and panels

| Rule | Detail |
|------|--------|
| Role and name | `role="dialog"`, named by its title through `aria-labelledby`; the title text is exactly the one the story names ("Delete Task", "Invite Team Member") |
| Structure | Header with the title and the close button ("Close"), body, footer with the actions, the primary or destructive one last |
| Opening | No fade. Focus moves into the dialog; Escape and the close button close it; focus returns to the element that opened it |
| Size | Default width; `lg` for forms with several fields; centred |
| Confirmation | Before an irreversible action: title naming the action ("Delete Task"), a question naming the consequence, "Cancel" (`light`) and the action (`danger`). No typing to confirm |
| While working | The action button is busy and disabled; the dialog stays open until the request ends; it closes on success |
| Side panel | Non-modal (help) or modal (the bottom "More" sheet); same naming and focus rules |

---

## 12. Roles in the UI

- The role in the selected organization decides what a user sees: menu entries (section 6.3), header menu
  entries, page actions, row actions and the "Actions" column. A Viewer sees no "New", "Edit", "Change status" or
  "Delete" control at all; hiding, not disabling.
- A page only Admins may open sends anyone else to the Dashboard page with the message of 10.4.
- The UI is not a security boundary: every check is repeated by the backend.

---

## 13. Accessibility

WCAG 2.1 AA.

| Requirement | How |
|-------------|-----|
| Names | Every control has an accessible name: its visible text, its `label`, or the `label` prop of `IconButton`. Icons add nothing to names |
| Roles | Native elements first: `button` for actions, `a` for navigation, `table` for tables, `nav` for menus, `dialog` for dialogs, `tablist`/`tab`/`tabpanel` for tabs, `region` (`section` with `aria-labelledby`) for named blocks inside a dialog |
| Headings | One `h1` per page; card and dialog titles are `h2`; no level skipped inside a card |
| Contrast | 4.5:1 for text, 3:1 for large text and control borders; the tokens of 4.2 meet it |
| Focus | Every control shows the Bootstrap focus ring; the order follows the reading order |
| Keyboard | Everything works with the keyboard; Escape closes menus, dialogs and panels; Enter submits forms |
| Status | Never colour alone: badges carry text, invalid fields carry a message |
| Live messages | Toasts, page alerts and inline errors are `role="alert"`; loading regions `role="status"`; static notes have no live role |
| Targets | At least 40 x 40 px for icon buttons, 44 px high for buttons below `md` |
| Language | `<html lang>` always equals the language shown (section 14) |
| Motion | Section 4.6 |

---

## 14. Languages

- English and Italian are both primary. English is the default and the fallback; both locale folders always hold
  the same keys (`npm run translate:validate`).
- Never hard-code text in a component: everything goes through `t('key')`. Keys are English; translations live in
  `public/locales/{lang}/{namespace}.json`. New keys are camelCase or dot notation. The one exception is
  `organization.json`, whose keys are the English sentence itself (`"Legal name is required"`); leave them as they
  are and do not copy the style into other namespaces.
- Interpolation for values (`t('expiresIn', { days: 28 })`), `_one` and `_other` suffixes for plurals.
- Dates follow the stories and test cases, which state them as they appear: a due date is shown as stored,
  `2030-01-15`, in both languages. Timestamps of the audit log use `toLocaleString` with the current i18next
  language. The sent date of a pending invitation uses `toLocaleDateString()`, so it follows the browser's format.
  A new screen that shows a date states the format in its story first.
- The application sets `document.documentElement.lang` to the two-letter code of the current language when
  i18next starts and on every change, in the i18n set-up, not in a component, so every screen (setup page and
  loading screen included) has it right.
- Language names are written in their own language ("English", "Italiano") and are not translated.
- A label that tests find in both languages is found by role and name, or by the test id of section 16 when its
  name changes with the language.

---

## 15. Responsive rules

| Width | Layout |
|-------|--------|
| Below `md` (768 px) | One column; bottom navigation; header 52 px; tables scroll inside their card; dialogs full width with margins |
| From `md` | Side navigation; header 56 px; page header sticky |
| From `lg` | Wider grids (four count cards per row, two-column forms) |

- Breakpoints are Bootstrap's. Design at 390 px first; the manual is captured at 390 x 844 and 1280 x 800
  (manual.md).
- The page itself never scrolls sideways; only a table inside its card or a row of chips may.
- Sticky and fixed elements at the bottom use `env(safe-area-inset-bottom)`.

---

## 16. Markup contracts with the E2E tests

The page objects find elements by role, label and text (development.md 10.2). A few hooks are markup; changing
one means changing the page object in the same change.

| Hook | Contract |
|------|----------|
| `.app` | Root element of the application, present once the application has started |
| `header.app-header` | The header, containing the organization name or switcher, "Help", the language button and the avatar button |
| `nav` (first of the page) | The side navigation; entries are links with `id="sidebar-{id}"`, `aria-label` and `aria-current="page"` |
| `ul.dropdown-menu` | An open header menu; its entries are buttons named by their text |
| Test ids | `btn-language-selector`, `btn-logout`, `audit-total`, `btn-help`, `btn-help-{context}`, and the `help-*`, `manual-*`, `search-*`, `tab-help-*`, `input-*`, `btn-gaps-*`, `gaps-*`, `error-gaps-*` ids of the help screens: kept exactly |
| `.card` | Every card; the dashboard card holding "Recent Tasks" contains its table |
| Count card | Element with role `group` named by the label; its first child `div` holds the number and nothing else |
| Field message | The next element sibling of the control |
| `.page-wrapper`, `.modal` | Root of a page and of a dialog; fully opaque once shown |
| `.modal.show` | Present while a dialog is open |
| `.Toastify__toast-container` | The toast container; each toast holds an element with `role="alert"` |
| `[role=alert] .small.text-muted > span` | The reference of an error message, alone in its `span` |
| `form button[type=submit]`, `tbody button` | Disabled with the native attribute while a request runs |
| `#onboarding-wizard` | The setup page form area, holding its submit button |

---

## 17. Performance and security

- Every page is lazy-loaded; a route chunk stays under 150 KB gzipped, all CSS under 80 KB gzipped.
- Icons are imported one by one; fonts come with their Latin subset; no image over 200 KB.
- Search and filter fields debounce at 300 ms.
- Tokens: MSAL keeps its own cache, and in addition the application stores the access token and the ID token in
  `localStorage` (`src/services/api/authService.ts`, read by `src/services/api/apiClient.ts` to attach the Bearer
  header). Any script running on the page can read them, so a cross-site scripting flaw would expose them. This is
  a known limit of the template, listed in 17.1; it is not a rule to imitate for other data.
- No `dangerouslySetInnerHTML`. Manual pages render through the manual blocks, never as raw HTML.
- Browser storage holds per-device conveniences (the pinned menu, the language detected before sign-in) and, as
  said above, the two tokens.

### 17.1 Known limits to review before production

| Limit | Where | What to decide |
|-------|-------|----------------|
| Access and ID tokens in `localStorage` | `src/services/api/authService.ts` (`setIdToken`, `setAccessToken`), `src/services/api/apiClient.ts` (request interceptor) | Move to the MSAL cache only (acquire the token silently before each request), or accept the risk with a strict Content Security Policy |
| The real sign-in is not tested by the E2E suite | `src/contexts/authContext.tsx` | The suite signs in through the impersonation header; test the real flow with your identity provider |

---

## 18. Never

- Import `react-bootstrap`, `lucide-react` or `react-toastify` outside `src/ui`.
- Write a hex colour, a pixel spacing or a font outside the theme.
- Add `!important`, a CSS-in-JS library, an icon font, a CDN link, a page transition or a dark mode.
- Put an icon, an asterisk or a hint inside a `label`, or anything between a control and its field message.
- Make an icon-only control without a `label`, or give an icon a `title`.
- Give a static note `role="alert"`.
- Disable a control for a role instead of hiding it.
- Show a flag for a language, or translate a language name.
- Copy code, styles, images or names from a commercial template or a demo kit into this repository.
