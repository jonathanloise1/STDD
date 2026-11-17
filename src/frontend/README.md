# MyApp Frontend

<p align="center">
  <img src="https://img.shields.io/badge/React-19-61DAFB?style=flat-square&logo=react" alt="React" />
  <img src="https://img.shields.io/badge/TypeScript-5.x-3178C6?style=flat-square&logo=typescript" alt="TypeScript" />
  <img src="https://img.shields.io/badge/Vite-6.x-646CFF?style=flat-square&logo=vite" alt="Vite" />
  <img src="https://img.shields.io/badge/MSAL-4.11-0078D4?style=flat-square&logo=microsoft" alt="MSAL" />
</p>

React 19 + TypeScript + Vite frontend for MyApp.

---

## Quick Start

### Prerequisites

- [Node.js](https://nodejs.org/) 22.18 or later (the manual and help scripts run TypeScript directly with Node)
- [npm](https://www.npmjs.com/) 10+

### 1. Install dependencies

```bash
npm install
```

> **Note**: the project uses npm. Do not use yarn. The checks (`tsc`, `lint`, `vitest`, `manual:check`) need this install first.

### 2. Configure local environment

Two `.env` files are committed and used for builds:

| File | Purpose | Tracked? |
|------|---------|----------|
| `.env.development` | DEV environment values (Azure AD DEV tenant, DEV API URL) | Yes |
| `.env.production` | PROD environment values (Azure AD PROD tenant, PROD API URL) | Yes |

These files contain **only public client-side values** (Client IDs, URLs). They are safe to commit because all `VITE_*` variables end up in the browser bundle anyway.

For **local development overrides**, create `.env.development.local` from the template:

```bash
cp .env.development.local.example .env.development.local
```

This file is **not tracked by git** and lets you override values for local work:

| Variable | Description | Default in template |
|----------|-------------|---------------------|
| `VITE_AZURE_AD_B2C_REDIRECT_URI` | Auth redirect (local) | `http://localhost:3147` |
| `VITE_API_BASE_URL` | API URL (local backend) | `https://localhost:7342` |
| `VITE_DEV_MOCK_USER_ID` | Skip Entra login (E2E/dev) | commented out |
| `VITE_DEV_MOCK_USER_EMAIL` | Mock user email | commented out |

> **Vite env priority**: `.env.development.local` > `.env.development` > `.env`

### 3. Start dev server

```bash
npm run dev
```

App: `http://localhost:3147`

---

## Available Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start Vite dev server |
| `npm start` | Alias for `npm run dev` |
| `npm run build` | TypeScript check + production build |
| `npm run build:dev` | Build with DEV config |
| `npm run build:prod` | Build with PROD config |
| `npm run preview` | Preview production build locally |
| `npm test` | Run tests (Vitest) |
| `npm run lint` | Run ESLint |
| `npm run lint:fix` | Auto-fix ESLint issues |
| `npm run translate:validate` | Validate translation files |
| `npm run manual:generate` | Generate the manual pages from the stories and the screenshots (needs an E2E capture first) |
| `npm run manual:check` | Check the manual pages against the stories and the code (`-- --strict` fails on warnings) |
| `npm run help:build` | Build the help pack from the manual pages |
| `npm run help:retrieve` | Run one retrieval query against the help pack |
| `npm run help:bench` | Measure retrieval quality against the benchmark |
| `npm run help:gaps` | List the questions the help could not answer |

---

## Configuration Files

| File | Tracked? | Purpose |
|------|----------|---------|
| `.env.development` | Yes | DEV build values (Azure AD DEV, DEV API URL) |
| `.env.production` | Yes | PROD build values (Azure AD PROD, PROD API URL) |
| `.env.development.local` | **No** | Local overrides (localhost API, mock user) |
| `.env.development.local.example` | Yes | Template for the above |
| `.env.example` | Yes | Template with all available variables |
| `package.json` | Yes | Dependencies and scripts |
| `package-lock.json` | Yes | Locked dependency tree |
| `tsconfig.json` | Yes | TypeScript configuration |
| `vite.config.ts` | Yes | Vite build configuration |
| `eslint.config.mjs` | Yes | ESLint flat config |
| `public/config.js` | Yes | Run time settings, empty here; the Docker image overwrites it at container start (below) |
| `Dockerfile`, `docker/` | Yes | Image: Node build, nginx runtime that forwards `/api` |

---

## Authentication

The app uses **Azure Entra External ID** with **MSAL** (`@azure/msal-browser`).

### Auth Flow

1. User visits the app: MSAL redirects to Entra login
2. User authenticates via email OTP
3. MSAL keeps its cache in browser storage, and the application also stores the access token and the ID token in `localStorage` (`src/services/api/authService.ts`, read by `src/services/api/apiClient.ts`). Any script on the page can read them: review this before production (`docs/guidelines/frontend.md` 17.1)
4. `authContext` manages auth state app-wide
5. `App` renders the application only when the user is authenticated and bootstrapped
6. `apiClient` (Axios) attaches Bearer token to all API calls

### Dev Auth Bypass

For local development without Entra login, set in `.env.development.local`:

```env
VITE_DEV_MOCK_USER_ID=00000000-0000-0000-0000-000000000001
VITE_DEV_MOCK_USER_EMAIL=dev@myapp.local
```

This requires the backend `MyApp:Impersonation:Enabled=true` in `appsettings.Development.json`.

### Settings at run time (Docker)

Vite bakes `VITE_*` values in at build time. The Docker image is built once and reads the settings that differ per
environment from the container's environment when it starts: `docker/40-runtime-config.sh` writes `/config.js`, which sets
`window.__MYAPP_CONFIG__`, and `src/config/runtimeConfig.ts` reads it. The keys are `VITE_API_BASE_URL` (empty means the
page's own origin, where nginx forwards `/api`), `VITE_API_SCOPE`, `VITE_API_AUDIENCE`, `VITE_AZURE_AD_B2C_CLIENT_ID`,
`VITE_AZURE_AD_B2C_AUTHORITY`, `VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES`, `VITE_DEV_MOCK_USER_ID` and `VITE_DEV_MOCK_USER_EMAIL`.
A key that is set, even empty, wins over the build value; the other keys stay build-time. A new setting that must differ per
environment goes in both files. See the root README, "Run with Docker".

---

## Internationalization

Using **i18next** with two supported languages:

- English (`en`)
- Italian (`it`)

Translation files: `public/locales/{lang}/`

---

## Styling

- **Bootstrap 5.3** through React-Bootstrap, wrapped by the UI layer in `src/ui` and the application shell in `src/shell`
- **SCSS** for the theme (`src/ui/theme`) and component styles
- The theme, the building blocks and the shell regions are described in `docs/guidelines/frontend.md` section 4 and following
