# AUTH-LANG-002 — Navigation Menu Items Translate on Language Change

| Field | Value |
|-------|-------|
| **User Story** | US-AUTH-08 |
| **Type** | E2E |
| **Priority** | High |
| **Automated** | Planned |

## Description

Verify that navigation menu items (sidebar and bottom tab bar) update their labels immediately when the user changes language, without requiring a page reload.

## Preconditions

- User A exists and is authenticated (via impersonation).
- User A belongs to an organization.
- Default language is `de` (German).

## Test Data

| Field | Value |
|-------|-------|
| User | Test User A |
| Initial language | `de` |
| Target language | `fr` |
| Sidebar item to verify | "Tasks" menu item |

### Expected Menu Labels per Language

| Menu Item Key | Italian (`it`) | French (`fr`) |
|---------------|---------------|---------------|
| `menu.dashboard` | Dashboard | Tableau de bord |
| `menu.tasks` | Attività | Tâches |
| `Settings` | Impostazioni | Paramètres |

## Steps

1. Navigate to the dashboard.
2. Verify sidebar menu items display labels matching the current language.
3. Open the language selector and switch language to French.
4. Verify that sidebar menu items update immediately to French labels (e.g., "Tâches" instead of "Attività", "Tableau de bord" instead of "Dashboard").
5. No page reload should be needed.

## Expected Results

- Sidebar menu items react to language change via i18n.
- All sidebar labels match the selected language's translation file.
- The change is immediate (no reload required).

## Related

- userstories.md: US-AUTH-08
- Test: AUTH-LANG-001 (language selector and persistence)
- Components: `src/frontend/src/components/app/SlimSidebar.tsx`, `src/frontend/src/components/app/BottomTabBar.tsx`

---

*Last updated: June 2025*
