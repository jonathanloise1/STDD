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

- [Node.js](https://nodejs.org/) 18+
- [npm](https://www.npmjs.com/) 9+

### 1. Install dependencies

```bash
npm install
```

> **Note**: the project uses npm with `legacy-peer-deps=true` (configured in `.npmrc`). Do not use yarn.

### 2. Configure local environment

Two `.env` files are committed and used for builds:

| File | Purpose | Tracked? |
|------|---------|----------|
| `.env.development` | DEV environment values (Azure AD DEV tenant, DEV API URL) | Yes |
| `.env.production` | PROD environment values (Azure AD PROD tenant, PROD API URL) | Yes |

These files contain **only public client-side values** (Client IDs, URLs, theme colors). They are safe to commit because all `VITE_*` variables end up in the browser bundle anyway.

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
| `npm run lint:scss` | Run Stylelint for SCSS |
| `npm run lint:fix-scss` | Auto-fix SCSS lint issues |
| `npm run icon` | Generate icon components from SVGs |
| `npm run storybook` | Start Storybook on port 6006 |
| `npm run build-storybook` | Build static Storybook |
| `npm run translate:validate` | Validate translation files |

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
| `.npmrc` | Yes | npm config (`legacy-peer-deps=true`) |
| `tsconfig.json` | Yes | TypeScript configuration |
| `vite.config.ts` | Yes | Vite build configuration |
| `eslint.config.mjs` | Yes | ESLint flat config |

---

## Authentication

The app uses **Azure Entra External ID** with **MSAL** (`@azure/msal-browser`).

### Auth Flow

1. User visits the app ? MSAL redirects to Entra login
2. User authenticates via email OTP
3. Token stored by MSAL in browser storage
4. `authContext` manages auth state app-wide
5. `ProtectedRoute` guards authenticated routes
6. `apiClient` (Axios) attaches Bearer token to all API calls

### Dev Auth Bypass

For local development without Entra login, set in `.env.development.local`:

```env
VITE_DEV_MOCK_USER_ID=00000000-0000-0000-0000-000000000001
VITE_DEV_MOCK_USER_EMAIL=dev@MyApp.local
```

This requires the backend `MyApp:Impersonation:Enabled=true` in `appsettings.Development.json`.

---

## Internationalization

Using **i18next** with two supported languages:

- English (`en`)
- Italian (`it`)

Translation files: `public/locales/{lang}/`

---

## Styling

- **SCSS** for component styles
- **Bootstrap 5** for layout and utilities
- Themed via `VITE_THEME` variable (`MyApp-professional` or `facit-default`)
