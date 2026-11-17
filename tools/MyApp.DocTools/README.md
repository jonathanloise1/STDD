# MyApp.DocTools

Checks the repository against the STDD documentation contract of `docs/guidelines/development.md`
(sections 3 to 13): rules R01 to R23, the baseline of section 12.3 and the derived "Automated by" field.
Section 12.2 says what each rule checks.
The repository root is found by walking up from the current folder to the one containing `docs/contracts.json`,
or passed as the second argument.

```
dotnet run --project tools/MyApp.DocTools -- check [root] [--baseline <file>] [--target-ref <git ref>]
dotnet run --project tools/MyApp.DocTools -- sync-automated-by [root]
dotnet run --project tools/MyApp.DocTools -- write-baseline [root] [--baseline <file>]
```

## check

Runs every rule, prints the findings grouped by rule (subject path, then a message saying what to fix) and
marks the ones listed in the baseline `tests/unit/MyApp.UnitTests/Architecture/baseline.json`.
Exit code 1 when a finding is not in the baseline, when a baseline entry matches no finding (stale: remove it),
or when the baseline is unsorted or has duplicates. Exit 0 otherwise.

To compare with a target branch, pass it with `--target-ref origin/{branch}`. The command then also fails on a baseline entry the
target branch does not have, unless section 12.2 of that branch does not list its rule yet. It reads the two files
with `git rev-parse` and `git show` only. A target with no baseline file counts as first adoption and is not compared.
An unreadable ref exits with 2.

## sync-automated-by

Rewrites the `Automated by` row of every test case to the value derived from the test tags (section 8.4):
`None`, or `e2e/{Class}` items first, then `integration/{Class}` items, each level in ordinal order.
Run it after adding or renaming an E2E or integration test class. Rule R18 fails on any difference.

## write-baseline

Writes every current finding to the baseline file, sorted by rule then subject. **For first adoption only**:
the baseline may only shrink, and an entry is never added to make a change pass. The command prints this warning.

## Layout

`Markdown/` and `CSharp/` hold the small parsers (text scanning, no compiler dependency), `Model/` the repository
model (areas, stories, test cases, registries, roadmap, test classes), `Rules/` one class per rule.
Tests are in `tests/unit/MyApp.UnitTests`: fixture repositories prove each rule, `Architecture/` runs the rules
against the real repository.
