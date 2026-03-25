# MyApp - Frontend & UI/UX Guidelines

> Design system, UI/UX principles, and frontend conventions for MyApp.
> This document is the single source of truth for every visual and interaction decision.

---

## 1. Design Philosophy

### 1.1 Our User

The MyApp user is a **team member** managing tasks and projects within their organisation. They:

- Work on multiple tasks with varying priorities
- Need a simple, clean interface to stay productive
- Use their phone and desktop equally
- Speak Italian, English, German, or French (all four must be equally supported)
- Expect zero learning curve and instant productivity

### 1.2 Design Principles

| Principle | What it means |
|-----------|---------------|
| **Mobile-first** | Design for 375px first, then scale up. Every screen must be usable on a phone. |
| **Zero learning curve** | If the user needs a tutorial, the design has failed. Every action is obvious. |
| **Trust through simplicity** | Blue tones, clean surfaces, clear layout. Professional and trustworthy. |
| **Progressive disclosure** | Show only what is needed. Details on demand. Never 10 fields on screen at once. |
| **Quality** | Precise, reliable, multilingual. No flashy gimmicks - subtle polish. |

### 1.3 Visual References

Inspired by:
- **Linear** - clean task management, card-based layout, smooth transitions
- **Notion** - minimal workspace, clean typography, whitespace
- **Vercel Dashboard** - developer-grade quality, dark mode, minimal UI

NOT inspired by:
- Enterprise admin panels with 50-field forms
- Generic Bootstrap dashboards with heavy tables
- Complex data-visualization tools

---

## 2. Tech Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| Framework | **React 19** | Functional components, hooks |
| Language | **TypeScript 5.x** | Strict mode, no `any` |
| Build | **Vite 6.x** | Fast refresh, CSS code-splitting |
| UI Kit | **Bootstrap 5.3** | Via SCSS. Facit template wrappers. |
| Styling | **SCSS (ITCSS)** + CSS custom properties | Dark mode via `[data-bs-theme="dark"]` |
| State | **React Context** | One context per domain. No Redux. |
| Forms | **Formik** | Inline validation |
| Charting | **ApexCharts** | Donut chart on dashboard (minimal use) |
| i18n | **i18next** | IT + EN + DE + FR. Keys always in English. |
| Routing | **React Router v6** | Lazy loading per page |
| Icons | **Material Icons** (via Facit SVG components) | Consistent set |
| Font | **Inter** (Google Fonts) | Best multilingual support |

---

## 3. Color Palette

Defined in `styles/settings/_index.scss`. Do not use raw hex values in components - always reference tokens.

### 3.1 Core Colors

| Token | Hex | Role |
|-------|-----|------|
| `$primary` | `#1E3A5F` | Header, sidebar, primary CTA |
| `$primary-light` | `#2D6DB5` | Hover states, links, secondary actions |
| `$accent` | `#00B4D8` | Highlights, badges, notification dots |
| `$background` | `#F7F9FC` | Page background |
| `$surface` | `#FFFFFF` | Cards, panels, modals |
| `$text-primary` | `#1A1A2E` | Main body text |
| `$text-muted` | `#6B7280` | Secondary labels, captions |
| `$success` | `#10B981` | Completed items, success states |
| `$warning` | `#F59E0B` | Pending items, approaching deadlines |
| `$danger` | `#EF4444` | Errors, overdue, critical alerts |

### 3.2 Semantic Usage

| Element | Color | Example |
|---------|-------|---------|
| Done badge | `$success` | Completed |
| In Progress badge | `$warning` | In Progress |
| Overdue badge | `$danger` | Overdue |
| Primary action | `$primary` | Button |
| Secondary action | `$primary-light` | Link |
| Notification badge | `$danger` | Red dot |

### 3.3 Dark Mode

Supported via `ThemeContext.darkModeStatus` and `[data-bs-theme="dark"]`.

Rules:
- Never hardcode hex in components. Use `var(--bs-body-bg)`, `var(--bs-body-color)`.
- Test every component in both modes.
- Charts use transparent background, text color from context.

---

## 4. Typography

| Element | Font | Weight | Size | Notes |
|---------|------|--------|------|-------|
| H1 (page title) | Inter | 700 | 28px | Max 1 per page |
| H2 (section) | Inter | 600 | 22px | Card titles |
| Body | Inter | 400 | 15px | Main text |
| Label / Caption | Inter | 500 | 13px | Form labels, secondary info |
| KPI Number | Inter | 800 | 48px | Dashboard stats |
| Button | Inter | 600 | 15px | CTA text |
| Disclaimer | Inter | 400 | 12px | Small print |

**Numeric formatting**: Always use `font-variant-numeric: tabular-nums` for aligned numbers. Use `Intl.NumberFormat` with locale from i18next - never format manually.

**Multilingual**: Inter has full support for German, French, Italian, and English characters.

---

## 5. Spacing & Layout

### 5.1 Base Units

| Property | Value |
|----------|-------|
| Base spacing unit | `8px` |
| Card padding | `16px` (mobile), `24px` (desktop) |
| Card border-radius | `16px` |
| Button border-radius | `12px` |
| Input border-radius | `10px` |
| Card shadow | `0 2px 8px rgba(0,0,0,0.08)` |
| Card shadow (hover) | `0 4px 16px rgba(0,0,0,0.12)` |
| Page margin (mobile) | `16px` horizontal |
| Page margin (desktop) | `24px` horizontal |

### 5.2 Breakpoints (Mobile-First)

Design for mobile first, then progressively enhance.

| Breakpoint | Range | Layout |
|------------|-------|--------|
| **xs** (default) | 0-575px | Single column, bottom tab bar, no sidebar |
| **sm** | 576-767px | Single column, slightly wider cards |
| **md** | 768-991px | Two-column where useful, sidebar as drawer |
| **lg** | 992-1199px | Desktop layout, slim sidebar (icons only, 60px) |
| **xl** | >=1200px | Full sidebar (220px with labels), wider content |

### 5.3 Mobile Navigation - Bottom Tab Bar

On `xs`/`sm`, the sidebar is replaced by a bottom tab bar:

- 4 tabs max (iOS HIG recommendation)
- Active tab: `$primary` icon + label
- Inactive: `$text-muted`

### 5.4 Desktop Navigation - Slim Sidebar

On `lg`+, a sidebar on the left:

- Default: collapsed (icons only, 60px wide)
- On hover/pin: expands to 220px with labels (slide-in animation, 200ms ease)
- Menu items: Dashboard, Tasks, Settings
- Active item: left border accent `$accent`, icon color `$primary`

### 5.5 Top Bar

- Height: `52px` (mobile), `56px` (desktop)
- Background: `$surface` with subtle bottom border (`1px solid #E5E7EB`)
- Left: Logo (icon only on mobile, logo + text "MyApp" on desktop)
- Right: Language selector, notification bell (with badge), user avatar (dropdown: Profile, Settings, Logout)

---

## 6. Page Patterns

Every page follows one of these patterns. Do not invent custom layouts.

### 6.1 Card List Page

Used for: Task list, any entity list.

Rules:
- Cards, not tables (tables only in admin audit page)
- Each card: title, status badge, key metadata
- Status badge: color-coded (`$success`/`$warning`/`$danger`)
- Tap card to open detail or edit
- "+" button top-right opens creation form
- Filter chips are scrollable horizontally on mobile
- Search bar has debounce (300ms)
- Empty state: illustration + "Create your first [entity]" CTA
- Skeleton loading: 3 placeholder cards with shimmer animation

### 6.2 Detail / Edit Page

Rules:
- Clean vertical layout, one field per row
- Edit and Delete are secondary actions (outline style)
- Delete requires confirmation modal
- Back arrow returns to list

### 6.3 Dashboard Page

Rules:
- KPI cards at top (card row, horizontally scrollable on mobile)
- Recent items list below
- CTA buttons for quick actions
- Greeting with time-based message

### 6.4 Form Page

Rules:
- One column on mobile, two on desktop (if useful)
- Labels above inputs
- Inline validation (below field, red text, on blur + on submit)
- Save button sticky-bottom on mobile (above safe area)
- Cancel always available, requires confirmation if dirty

---

## 7. Component Conventions

### 7.1 File Structure

```
src/
  components/
    common/                     <- Generic reusable components
    bootstrap/                  <- Bootstrap wrappers
    shared/                     <- Shared widgets
  pages/
    company/
      dashboards/               <- Dashboard view
      tasks/                    <- Task list and management
      onboarding/               <- First-time wizard
      settings/                 <- User settings, profile
    admin/
      audit/                    <- Audit trail (admin only)
```

### 7.2 Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Component file | PascalCase | `TaskListPage.tsx` |
| Component export | PascalCase | `export default TaskListPage` |
| Page | PascalCase + suffix | `TaskListPage.tsx`, `DashboardView.tsx` |
| Hook | camelCase with `use` | `useTasks.ts` |
| Context | PascalCase + suffix | `TaskContext.tsx` |
| API Service | camelCase + `Service` | `taskService.ts` |
| Type/Interface | PascalCase, `I` prefix for interface | `ITask`, `TaskStatus` |
| i18n key | camelCase or dot notation | `tasks.createTask` |
| Route path | kebab-case | `/tasks`, `/dashboard` |
| CSS class | kebab-case | `.task-card`, `.task-card__badge` |

### 7.3 Component Rules

1. **One component = one file.** No multiple component definitions in one file.
2. **Typed props.** Every component has an explicit `Props` interface.
3. **Default export** for pages (required for `React.lazy()`).
4. **Named export** for reusable components.
5. **No business logic in components.** Components render. Logic lives in services or contexts.
6. **No `useEffect` for data fetching** in pages - use context + dedicated hook. With React 19, consider `use()` for simple cases.

### 7.4 State Management

```
Context hierarchy:

ThemeContext              <- UI (dark mode, sidebar collapse)
  AuthContext             <- Authentication, MSAL token, user profile
    PermissionContext     <- RBAC (Admin/Editor/Viewer)
      OrganizationContext <- Current organization
```

Rules:
- Each context exposes: `data`, `isLoading`, `error`, and CRUD methods
- Contexts communicate via `CustomEvent` (existing pattern)
- Never duplicate state. If it is in context, do not also put it in local state.

---

## 8. Interactions & Feedback

### 8.1 Loading States

| Context | Pattern |
|---------|---------|
| First page load | Skeleton cards (shimmer rectangles, not spinners) |
| CRUD action | Button with inline spinner + disabled state |
| Page transition | Fade-in (200ms ease) - never a hard jump |

**Never** show a centred spinner on a blank page. Always show page structure during loading.

### 8.2 Notifications & Toasts

| Type | Component | Duration |
|------|-----------|----------|
| Success (CRUD) | Toast (slide from top on mobile) | 3s auto-dismiss |
| Error (API) | Toast danger | Persistent, manual dismiss |
| Validation error | Inline below field, red | Persistent until fixed |
| Delete confirmation | Modal with "Delete" and "Cancel" | Blocking |

### 8.3 Empty States

Every empty list shows:
1. Line-art illustration (single colour, `$text-muted`, 120px)
2. Title: "No [entities] yet"
3. Subtitle: one-line explanation
4. CTA: primary button "Create your first [entity]"

Never show an empty card or a table with only headers.

### 8.4 Micro-interactions

| Element | Interaction |
|---------|-------------|
| Card hover (desktop) | Shadow grows (`0 4px 16px`), left border appears in `$accent` |
| Card tap (mobile) | Brief scale-down (0.98) then navigate |
| Tab switch | Content cross-fade (150ms) |
| Notification bell | Brief shake when new notification arrives |

### 8.5 Destructive Confirmations

For irreversible actions (delete task, delete entity):
1. Bottom sheet (mobile) or modal (desktop)
2. Title: "Delete [entity name]?"
3. Text: "This action cannot be undone."
4. Two buttons: "Cancel" (`$surface`) and "Delete" (`$danger`)
5. No typing confirmation - keep it simple

---

## 9. Responsiveness Rules

### 9.1 Cards, Not Tables

All data lists use cards. Never use `<table>` for user-facing data (tables only acceptable in admin Audit Trail page).

### 9.2 Touch Targets

- Minimum tap target: **44x44px** (Apple HIG)
- Buttons: minimum height `48px` on mobile
- Card tap areas: full card width
- Spacing between interactive elements: minimum `8px`

### 9.3 Scrolling

- Vertical scroll: natural document flow, no fixed-height containers with internal scroll
- Horizontal scroll: only for filter chips and KPI card carousel

### 9.4 Safe Areas

Account for mobile safe areas (notch, home indicator):
- Bottom tab bar: `padding-bottom: env(safe-area-inset-bottom)`

---

## 10. Accessibility (a11y)

WCAG 2.1 AA minimum:

| Requirement | How |
|-------------|-----|
| Text contrast | >= 4.5:1 normal text, >= 3:1 large text. Verify on dark mode. |
| Focus visible | Every interactive element has visible focus outline |
| Aria labels | Every icon-only button has `aria-label`. Every form field has `<label>`. |
| Keyboard nav | Tab order logical. Escape closes modals. Enter submits. |
| Screen reader | Badges have `aria-label` (not just colour for status). |
| Motion | Respect `prefers-reduced-motion`. Disable pulse animations. |
| Language | `<html lang="...">` updates on language change |

---

## 11. i18n Conventions

### 11.1 Four Languages, Day One

Italian (IT), English (EN), German (DE), and French (FR) are all primary languages. No language is "default with translations" - all four are equal.

### 11.2 Key Structure

```json
{
  "tasks": {
    "title": "Tasks",
    "createTask": "New Task",
    "taskTitle": "Title",
    "taskStatus": "Status",
    "statusTodo": "Todo",
    "statusInProgress": "In Progress",
    "statusDone": "Done",
    "noTasks": "No tasks yet",
    "noTasksDescription": "Create your first task to get started."
  }
}
```

### 11.3 Rules

- **Never** hardcode strings in UI. Everything goes through `t('key')`.
- Keys are in **English**. Translations live in `locales/{lang}/{namespace}.json`.
- Use **interpolation** for dynamic values: `t('expiresIn', { days: 28 })`
- Use **pluralization**: `_one` / `_other` suffixes
- Dates follow locale conventions using `Intl.DateTimeFormat`

---

## 12. Performance

### 12.1 Targets

| Metric | Target |
|--------|--------|
| First Contentful Paint (FCP) | < 1.5s |
| Largest Contentful Paint (LCP) | < 2.5s |
| Bundle JS per route | < 150KB gzipped |
| Bundle CSS total | < 80KB gzipped |
| Time to Interactive | < 3s on 4G |

### 12.2 Techniques

- **Code splitting**: every page lazy-loaded (`React.lazy()`) - already implemented
- **Skeleton loaders**: always, never blank page
- **Debounce**: 300ms on all search/filter inputs
- **Memoisation**: `React.memo()` for heavy list items
- **Image optimisation**: WebP, lazy loading, max 200KB
- **Font**: Inter loaded via `font-display: swap`, subset for Latin

---

## 13. Security (Frontend)

| Rule | Detail |
|------|--------|
| No tokens in code | JWT in memory (MSAL cache), never in global variables |
| XSS | No `dangerouslySetInnerHTML`. React escapes by default. |
| CORS | Managed by backend. Frontend does not proxy. |
| Roles in frontend | Frontend hides elements by role, but **every check is replicated server-side**. Frontend is NOT a security boundary. |

---

*Last updated: March 2026*