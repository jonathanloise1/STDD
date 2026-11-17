# Manual tooling

Implements `docs/guidelines/manual.md` sections 2, 3 and 10: `manual:generate` and `manual:check`. Scripts are
TypeScript with erasable syntax only, run directly by Node 22.18 or later (no build step, no new dependency);
imports use explicit `.ts` extensions. Parsing code shared with the help scripts lives in `../lib/`.

## Commands (run in `src/frontend`)

| Command | What it does |
|---------|--------------|
| `npm run manual:generate` | Creates missing pages as drafts (both languages), refreshes derived front matter, prints stale pages with the source diff and pages without a source |
| `npm run manual:generate -- --accept {story id or area folder}` | Sets `source_hash` and `steps_hash` of both language pages; refuses, changing nothing, on a draft marker or a finding other than M05 |
| `npm run manual:check` | Checks M01 to M16; M05, M06 and M14 are warnings. Exit 1 on any failure |
| `npm run manual:check -- --strict` | Every finding fails |
| `npm run manual:check -- --root {dir} --no-images` | Checks a docs tree laid out like the repository (`docs/`, optionally `src/frontend/`); `--no-images` skips M08 and M09 |

`--root {dir}` is accepted by both commands. Generating or accepting regenerates the manual: it needs the user's
approval (CLAUDE.md). Exit codes: 0 ok, 1 findings or refusal, 2 usage error.

## Files

| File | Role |
|------|------|
| `generate.ts`, `draft.ts`, `staleness.ts` | Generator, draft content and step table, M05 logic and source diff |
| `check.ts` | Checks M01 to M16, CLI |
| `args.ts` | Option parsing |
| `*.test.ts`, `testutil.ts` | Tests (`npx vitest run scripts`) |
| `fixtures/repo/` | A small repository with a valid manual: must pass `--strict` with no finding |
| `fixtures/golden/` | Expected output of `manual:generate` on `fixtures/repo` without its manual |

When a story, test case or draft rule changes the golden output, regenerate `fixtures/golden/` from a temporary
copy of `fixtures/repo` (without `docs/manual`) and review the diff.

## Library (`../lib/`)

Import from `../lib/index.ts`: `loadRepo` (areas, stories, test cases), `parseStory`, `parseTestCase`,
`parseAreaIndex`, `loadPages` and `parsePage` (front matter and body structure), `parseMarkdown` (block tree and
violations of the 2.5 subset), `gitBlobId`, `sha256Hex`, `splitQuestions` (6.2), `loadLocales`, `loadStopwords`.
