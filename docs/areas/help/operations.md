# Help - Operations

## help-answer-declined

| Field | Value |
|-------|-------|
| Log key | `help.answer.declined` |
| Test case | [HELP-ANSWER-004](test-cases/HELP-ANSWER-004.md) |
| Expected rate | 20 per hour |
| Fingerprint by | None |
| Cause category | expected_behaviour |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

The assistant declined to answer a question, or its answer could not be used (no valid source), so the panel showed the closest manual sections instead. Expected for questions the manual does not cover; the user is not blocked.

### Check

1. Do not treat a single entry as a fault: declining is the designed answer when the manual does not support one.
2. Watch the rate. A sudden rise right after a release suggests a new assistant or a new manual that no longer matches: compare the model version and the pack version in the release notes with the previous release.
3. To see which questions are declined, ask an Admin to download the unanswered questions from the Unanswered questions page and look at the outcomes `refused` and `invalid`.
4. A question the manual does answer but the assistant keeps declining is a documentation or model gap, not an incident.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| signature_lookup | log_key=help.answer.declined | found == true |

### Resolve

1. No action for a normal rate.
2. When the rate stays high, send the downloaded questions to the documentation owner: add the user's wording to the story's questions, or record an out-of-scope question; hand over to development when the assistant model should be re-evaluated.

### History

None.

## help-answer-timeout

| Field | Value |
|-------|-------|
| Log key | `help.answer.timeout` |
| Test case | [HELP-ANSWER-005](test-cases/HELP-ANSWER-005.md) |
| Expected rate | 2 per hour |
| Fingerprint by | None |
| Cause category | capacity |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

The assistant took longer than its time limit, so the panel showed the best matching manual sections under "Best matches in the manual". The user still gets useful links, but no written answer.

### Check

1. Open the trace with `logs_by_correlation` and note how long the request waited before the limit was reached.
2. Read `health_status`. If the application is slow in general, the assistant is slow for the same reason.
3. Check the model host: it must answer on the endpoint of `MyApp:Assistant:Server:Endpoint`, and it handles only `MyApp:Assistant:Server:MaxConcurrent` questions at once. Many entries within a short time mean the questions queued up behind each other.
4. Check the device side: on a user's device the model may still be preparing or may run slowly on the processor. These entries are scattered over many users and are normal at a low rate.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |

### Resolve

1. No action at the expected rate or for a burst that ended.
2. When the rate stays above normal, hand over to the platform owner with the time window to check the model host (restart it, give it more resources, or raise `MyApp:Assistant:Server:MaxConcurrent`). The setting `MyApp:Assistant:Order` set to `retrieval-only` keeps help working without a model while the host is repaired.

### History

None.

## help-ask-unavailable

| Field | Value |
|-------|-------|
| Log key | `help.ask.unavailable` |
| Test case | [HELP-ASK-008](test-cases/HELP-ASK-008.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A user typed a question in the Ask tab and the help content needed to answer it could not be loaded, so the panel says help is unavailable and shows no sections. Asking works for nobody on this release until the content is back.

### Check

1. Open the trace with `logs_by_correlation` when the user gave a Reference. The entry names the route the user was on.
2. Read `health_status`. A failing check means the application itself is down, which also explains the missing help.
3. Confirm that the help files are deployed: the manual bundle and the help pack of the user's language (`manual.json` and `pack.json` under the help folder of the web site) must open in a browser, and the backend must be able to read the same files from the folder in the setting `MyApp:Help:PackDirectory`. They are produced by `npm run help:build` as part of the build, so a missing file means the release was built or deployed without them.
4. Check the release: the help pack records the project version it was built for. A pack from another version, or an empty one, is also reported as unavailable.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. When a help file is missing or empty, hand over to development to rebuild and redeploy the release with `npm run help:build`; support cannot repair it. Tell the user the manual is temporarily unavailable.
2. When the files are present and healthy but the entry repeats, hand over to development with the trace id: the browser could not read or parse the content.
3. No action for an isolated entry that does not repeat (a network blip on the user's side).

### History

None.

## help-browse-load-failed

| Field | Value |
|-------|-------|
| Log key | `help.browse.load_failed` |
| Test case | [HELP-BROWSE-005](test-cases/HELP-BROWSE-005.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A user opened the Manual page and the manual content could not be loaded, so the page shows "The manual could not be loaded." and no list of areas.

### Check

1. Open the trace with `logs_by_correlation` when the user gave a Reference. The entry names the route the user was on.
2. Read `health_status`. A failing check means the application itself is down, which also explains the missing help.
3. Confirm that the help files are deployed: the manual bundle and the help pack of the user's language (`manual.json` and `pack.json` under the help folder of the web site) must open in a browser, and the backend must be able to read the same files from the folder in the setting `MyApp:Help:PackDirectory`. They are produced by `npm run help:build` as part of the build, so a missing file means the release was built or deployed without them.
4. Check the release: the help pack records the project version it was built for. A pack from another version, or an empty one, is also reported as unavailable.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. When a help file is missing or empty, hand over to development to rebuild and redeploy the release with `npm run help:build`; support cannot repair it. Tell the user the manual is temporarily unavailable.
2. When the files are present and healthy but the entry repeats, hand over to development with the trace id: the browser could not read or parse the content.
3. No action for an isolated entry that does not repeat (a network blip on the user's side).

### History

None.

## help-download-failed

| Field | Value |
|-------|-------|
| Log key | `help.download.failed` |
| Test case | [HELP-DOWNLOAD-009](test-cases/HELP-DOWNLOAD-009.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | dependency_unavailable |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

An Admin asked to download the unanswered questions and the file could not be prepared; no file was downloaded. Only that Admin is affected.

### Check

1. Open the trace with `logs_by_correlation` and read the exception: the file is built from the stored questions of the chosen period.
2. Read `health_status`: the stored questions live in the application's storage, so an unhealthy storage check is the first suspect.
3. Check the setting `MyApp:Help:Gaps:Enabled`: when recording is off there is nothing to export, and the period may be empty rather than failing.
4. Ask the Admin which period was chosen. A very long period holds more questions; try a shorter one.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. Ask the Admin to retry with a shorter period once the checks are healthy.
2. When storage is unhealthy, hand over to the platform owner. When storage is healthy and the entry repeats with the same period, hand over to development with the trace id.

### History

None.

## help-open-unavailable

| Field | Value |
|-------|-------|
| Log key | `help.open.unavailable` |
| Test case | [HELP-OPEN-008](test-cases/HELP-OPEN-008.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A user clicked Help on a screen and the help content could not be loaded, so the panel says help is unavailable and offers a link to the manual. The screen itself keeps working.

### Check

1. Open the trace with `logs_by_correlation` when the user gave a Reference. The entry names the route the user was on.
2. Read `health_status`. A failing check means the application itself is down, which also explains the missing help.
3. Confirm that the help files are deployed: the manual bundle and the help pack of the user's language (`manual.json` and `pack.json` under the help folder of the web site) must open in a browser, and the backend must be able to read the same files from the folder in the setting `MyApp:Help:PackDirectory`. They are produced by `npm run help:build` as part of the build, so a missing file means the release was built or deployed without them.
4. Check the release: the help pack records the project version it was built for. A pack from another version, or an empty one, is also reported as unavailable.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. When a help file is missing or empty, hand over to development to rebuild and redeploy the release with `npm run help:build`; support cannot repair it. Tell the user the manual is temporarily unavailable.
2. When the files are present and healthy but the entry repeats, hand over to development with the trace id: the browser could not read or parse the content.
3. No action for an isolated entry that does not repeat (a network blip on the user's side).

### History

None.

## help-search-unavailable

| Field | Value |
|-------|-------|
| Log key | `help.search.unavailable` |
| Test case | [HELP-SEARCH-006](test-cases/HELP-SEARCH-006.md) |
| Expected rate | 0 per hour |
| Fingerprint by | None |
| Cause category | data_missing |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A user searched the manual and the content needed for the search could not be loaded, so the page says help is unavailable and shows no results.

### Check

1. Open the trace with `logs_by_correlation` when the user gave a Reference. The entry names the route the user was on.
2. Read `health_status`. A failing check means the application itself is down, which also explains the missing help.
3. Confirm that the help files are deployed: the manual bundle and the help pack of the user's language (`manual.json` and `pack.json` under the help folder of the web site) must open in a browser, and the backend must be able to read the same files from the folder in the setting `MyApp:Help:PackDirectory`. They are produced by `npm run help:build` as part of the build, so a missing file means the release was built or deployed without them.
4. Check the release: the help pack records the project version it was built for. A pack from another version, or an empty one, is also reported as unavailable.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |
| logs_by_correlation | trace_id={signal.TraceId} | records exists |

### Resolve

1. When a help file is missing or empty, hand over to development to rebuild and redeploy the release with `npm run help:build`; support cannot repair it. Tell the user the manual is temporarily unavailable.
2. When the files are present and healthy but the entry repeats, hand over to development with the trace id: the browser could not read or parse the content.
3. No action for an isolated entry that does not repeat (a network blip on the user's side).

### History

None.
