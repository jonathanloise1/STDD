# MyApp - Operations Guidelines

> How a project built on this template is monitored and supported after release: log call sites, correlation,
> signatures and runbooks, the analyser, read-only tools, masking, the incident store, notifications, the
> investigator model, profiles, the operations pack and the return loop.
> Formats defined here are contracts; their versions are in [docs/contracts.json](../contracts.json).
> The rules for test cases and their Observable trace are in [development.md](development.md) section 8.5.

**How to read this.** Sections 3 to 5 apply to every feature (backend and docs). Sections 6 to 14 specify the
monitor application and its files. Section 15 is the return loop. Section 16 lists the checks. "Exactly" means
a validator checks it.

---

## 1. Principles

| # | Rule |
|---|------|
| P1 | Signatures come from test cases. Every Observable trace (development.md 8.5) becomes a signature, joined with its log call site. Nothing is catalogued by hand except template signatures (5.5) |
| P2 | Deterministic first. Sources, analyser, rules, masking, dedup, gates and notifications work without a model. The investigator is called only on residual cases (6.8) |
| P3 | Read-only. No component writes to an application source. The monitor writes only to its own store, its log, e-mail and the ticket webhook |
| P4 | Masking by allow-list in the tool layer. A field that the masking policy does not list never leaves the tool. The model never masks |
| P5 | Tools are generic by source type. A project binds its entities by configuration (7.2), never by writing a tool the model would have to learn |
| P6 | Knowledge lives in the operations pack, not in the weights. The investigator is trained on the template's protocol with synthetic data (section 11, `models.md`) |
| P7 | Every outcome returns to the repository as a pull request (section 15). Signatures kept in people's heads are the failure mode of this design |

**Why this exists.** In maintenance the cost is rarely the monitoring: it is the chain of people who rebuild the context of
an error and explain it to each other. The design removes hand-offs: wherever one person translates something for another
today, the monitor produces a structured artifact complete enough to need no questions.

**Guiding metric.** The share of incidents closed without a human question. Everything here exists to raise it, release after
release. The working assumption, not yet measured: below about 70% the human chain stays in place, above about 80% it stops being
needed for recurrent incidents. Measure it from the first month of the first adopting project.

**Why rules come before the model.** Most incidents are recurrent, and a model on each would be slow and costly. A rule verdict can be
audited, while a model verdict can only be evaluated statistically. And when the model is unavailable, every known failure still
gets its incident.

**Build order for an adopting project:** correlation and log call sites (sections 3, 4), signatures and runbooks (section 5), analyser and
sources (section 6), tools and masking (sections 7, 8), store and notifications (section 10), investigator (section 11), return loop (section 15).
Each step is useful without the next one. Most of the value arrives before the model: do not start from the investigator.

**Limits, stated plainly.**

- The saving applies to recurrent incidents. A novel incident still needs a developer, who receives it with the context already built.
- A small model does not understand an unseen problem by reading logs. It isolates it, describes it and prepares the reproduction.
  Novelty is detected statistically (new templates, abnormal rates); the model explains.
- The bottleneck is not technical: it is the discipline of routing every outcome through the repository (P7).
- No learning in production: continuous weight updates have no ground truth, create feedback loops, cannot be reproduced and would
  put personal data in the weights. Learning lives in the pack and the evaluation suite, through pull requests.
- The on-premises profile gives up free-text search over long periods of raw logs across hosts, and APM features such as distributed
  tracing and profiling, if a project uses them.

---

## 2. The incident loop

```
1  SIGNAL    source -> log record in normal form (6.1), with trace id and log key when present
2  ANALYSE   template -> signature match -> rate and severity -> fingerprint -> escalate or count   (no model)
3  RULES     rule checks of the runbook (5.4) -> rule verdict                                       (no model)
4  RESIDUAL  unknown, abnormal rate, contradicted runbook -> investigator with read-only tools      (model)
5  REPORT    incident report, fixed schema (10.1)
6  WRITE     one write to the incident store -> dashboard, e-mail, ticket (dedup, gates, suppression)
7  RETURN    outcome -> pull request: test case, log call site, runbook                             (human-gated)
```

| Step | Component (section) | Model | Output |
|------|---------------------|-------|--------|
| 1 | Sources (6.2 to 6.4) | No | Log records |
| 2 | Analyser (6.5 to 6.8) | No | Counter update, or an escalation |
| 3 | Rule engine (6.9) | No | Rule verdict and evidence |
| 4 | Investigator host (11) | Yes | Investigation (11.3) merged into the report |
| 5, 6 | Incident store, notifiers (10) | No | Incident, notifications |
| 7 | Dashboard review queue, `fix.agent.md` (15) | Optional | Pull request |

If the investigator is off, missing or fails, step 4 is skipped and the incident keeps the rule verdict.

---

## 3. Log call sites

### 3.1 Log definition classes

Every keyed log entry of the backend is a source-generated `LoggerMessage` method in one class per area.

| Element | Rule |
|---------|------|
| File | `src/backend/MyApp.Application/Logging/{AreaPascal}Log.cs` (`tasks` -> `TasksLog.cs`); where that class name collides with a domain type, the area title in PascalCase (`audit` -> `AuditTrailLog.cs`); platform events in `PlatformLog.cs` (3.5) |
| Class | `public static partial class {AreaPascal}Log` |
| Key constant | In the nested `public static class Keys`: `public const string {MethodName} = "{log key}";`. This is the only string literal of the key under `src/backend/` (rule R15) |
| Method name | The log key in PascalCase, splitting on `.` and `_` (`task.create.not_allowed` -> `TaskCreateNotAllowed`) |
| Attribute | `[LoggerMessage(EventId = n, EventName = Keys.{MethodName}, Level = LogLevel.{Level}, Message = "...")]` |
| `EventId` | Unique within the class, numbered from 1 in order of addition, never reused. It carries no meaning; tools match on `EventName` only |
| `Level` | From the test case Severity: `Information` -> `Information`, `Warning` -> `Warning`, `Error` -> `Error`, `Critical` -> `Critical` |
| Story comment | The line immediately above the attribute is the story-id comment (development.md 11.2) and contains the test case's User Story |
| Signature | `public static partial void {MethodName}(this ILogger logger, [Exception exception,] {parameters});` |

```csharp
public static partial class TasksLog
{
    public static class Keys
    {
        public const string TaskCreateNotAllowed = "task.create.not_allowed";
    }

    // US-TASK-01
    [LoggerMessage(EventId = 1, EventName = Keys.TaskCreateNotAllowed, Level = LogLevel.Warning,
        Message = "Task not created: the role {Role} of user {UserId} in organization {OrganizationId} does not allow it")]
    public static partial void TaskCreateNotAllowed(this ILogger logger, OrganizationRole role, Guid userId, Guid organizationId);
}
```

Call site, at the point where the situation is detected: `logger.TaskCreateNotAllowed(role, userId, organizationId);`
then `throw new OutcomeException(TasksLog.Keys.TaskCreateNotAllowed, StatusCodes.Status403Forbidden, "Your role does not allow this change.");`
when the request cannot complete (4.2).

### 3.2 Message template rules

| Rule | Detail |
|------|--------|
| Language | English, one sentence, starts with the outcome ("Task not created: ...") |
| Placeholders | PascalCase, named after what they hold; types: identifiers (`Guid`, `int`), enum values, counts, durations, route templates |
| No personal data | No placeholder for e-mail, names, phone, address, titles, descriptions or any user free text. Log the identifier; the tools read the rest through masking |
| No secrets | No tokens, connection strings, keys |
| Stability | Changing the template text is allowed (the signature matches the key, not the text); renaming or removing a key is a format change of the signature and follows section 15 |

### 3.3 Structured fields

Two infrastructure pieces make the key travel as data, without a second literal:

| Piece | Location | Behaviour |
|-------|----------|-----------|
| `LogKeyProcessor` | `src/backend/MyApp.Infrastructure/Logging/LogKeyProcessor.cs`, registered in `ConfigureOpenTelemetry` | An OpenTelemetry `BaseProcessor<LogRecord>`: when `EventId.Name` is not empty, adds the attribute `LogKey` = `EventId.Name`. Application Insights stores it in `Properties.LogKey` of `AppTraces` and `AppExceptions` |
| JSON lines file sink | `src/backend/MyApp.Infrastructure/Logging/JsonFileLoggerProvider.cs` (3.4) | Writes `LogKey` from `EventId.Name` |

Fields every keyed entry carries in both destinations:

| Field | Application Insights (workspace tables) | Log file (3.4) |
|-------|-----------------------------------------|----------------|
| Log key | `Properties.LogKey` | `LogKey` |
| Trace id | `OperationId` | `TraceId` |
| Span id | `ParentId` | `SpanId` |
| Template | `Properties["{OriginalFormat}"]` when present | `MessageTemplate` |
| Template parameters | `Properties.{Name}` | `Properties.{Name}` |
| Level | `SeverityLevel` (0 Verbose, 1 Information, 2 Warning, 3 Error, 4 Critical) | `Level` |
| Category | `Properties.CategoryName` | `Category` |
| Version | `AppVersion` | `Version` |

The story id is not emitted at runtime (rule R22 forbids story-id literals in `src/`). It is resolved through the
signature: log key -> test case -> User Story (5.2).

### 3.4 Log file format

The file sink is enabled for on-premises deployments and for local development. It uses no external library.

| Setting (backend configuration) | Default | Meaning |
|---------------------------------|---------|---------|
| `MyApp:Logging:File:Enabled` | `false` (`true` in `appsettings.Development.json`) | Turns the sink on |
| `MyApp:Logging:File:Directory` | `logs` (relative to the content root) | Folder |
| `MyApp:Logging:File:FilePrefix` | `myapp-api` | File name prefix |
| `MyApp:Logging:File:MaxFileSizeMB` | `100` | Rolls over above this size |
| `MyApp:Logging:File:RetainedFiles` | `14` | Older files are deleted |
| `MyApp:Logging:File:MinimumLevel` | `Information` | Lowest level written |

- File name: `{prefix}-{yyyyMMdd}.jsonl`, then `{prefix}-{yyyyMMdd}-{n}.jsonl` (n from 1) after a size roll. A new
  file starts at the UTC date change.
- Encoding UTF-8 without BOM, one JSON object per line, LF. The file is opened with `FileShare.ReadWrite | FileShare.Delete`
  so a tailer can read it. A line is written whole with one write call.
- One object per entry, fields in this order; absent values are omitted, never `null`:

| Field | Type | Content |
|-------|------|---------|
| `Timestamp` | string | UTC, ISO 8601 with milliseconds and `Z` (`2026-11-06T10:42:01.123Z`) |
| `Level` | string | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` |
| `Category` | string | Logger category |
| `EventId` | integer | `EventId.Id` |
| `LogKey` | string | `EventId.Name` when not empty |
| `MessageTemplate` | string | The template |
| `Message` | string | The formatted message |
| `TraceId` | string | 32 lower-case hex, from `Activity.Current` |
| `SpanId` | string | 16 lower-case hex |
| `Properties` | object | Template parameters and scope values, name -> string |
| `Exception` | object | `Type` (full name), `Message`, `Frames` (up to 20 strings `Namespace.Type.Method`), `Inner` (same shape, depth up to 3) |
| `Service` | string | `myapp-api` |
| `Version` | string | Assembly informational version |
| `Host` | string | Machine name |
| `Environment` | string | Host environment name |

Example:

```json
{"Timestamp":"2026-11-06T10:42:01.123Z","Level":"Warning","Category":"MyApp.Application.Services.TaskItemService","EventId":1,"LogKey":"task.create.not_allowed","MessageTemplate":"Task not created: the role {Role} of user {UserId} in organization {OrganizationId} does not allow it","Message":"Task not created: the role Viewer of user 6f1c...e2 in organization 0b7d...91 does not allow it","TraceId":"4bf92f3577b34da6a3ce929d0e0e4736","SpanId":"00f067aa0ba902b7","Properties":{"Role":"Viewer","UserId":"6f1c...e2","OrganizationId":"0b7d...91"},"Service":"myapp-api","Version":"1.4.0","Host":"app01","Environment":"Production"}
```

### 3.5 Unkeyed entries and platform events

Some entries have no test case: unhandled exceptions, rejected requests, failed jobs, health failures, framework
messages. They become unknown signals with an extracted template (6.5). To make their templates stable, the
backend emits the ones it owns as platform events, defined in `PlatformLog.cs` with the same convention as 3.1
(constants in `PlatformLog.Keys`, no story comment). Platform event names start with `platform.`; no area may
have the code `PLATFORM`. They are not log keys: no test case defines them and no runbook is required.

| Event name | Level | Emitted by | Discriminator properties (6.5) |
|------------|-------|------------|--------------------------------|
| `platform.request.unhandled` | Error | `ExceptionHandlingMiddleware`, any exception not handled below | `ExceptionType`, `TopFrame`, `Route` |
| `platform.request.rejected` | Warning | `ExceptionHandlingMiddleware`, `KeyNotFoundException` or `UnauthorizedAccessException` not wrapped in an `OutcomeException` | `ExceptionType`, `TopFrame`, `Route` |
| `platform.request.rate_limited` | Warning | Rate limiter `OnRejected` | `Policy` |
| `platform.job.failed` | Error | Catch-all of every background job | `Job`, `ExceptionType`, `TopFrame` |
| `platform.health.unhealthy` | Error | Every health check on failure | `Check` |
| `platform.startup.failed` | Critical | Startup and migrations | `ExceptionType`, `TopFrame` |

Monitor-generated events (section 6.4, source `http_health`) use the same mechanism:

| Event name | Level | Discriminator |
|------------|-------|---------------|
| `monitor.api.unreachable` | Critical | `Url` |
| `monitor.api.unhealthy` | Error | `Check` |

`ExceptionType` and `TopFrame` are template parameters computed by `ExceptionSummary.From(exception)` in
`src/backend/MyApp.Infrastructure/Logging/ExceptionSummary.cs`:

- `ExceptionType`: full type name of the innermost exception.
- `TopFrame`: the first stack frame, scanning the outermost exception first and then inner ones, whose declaring
  type's namespace starts with `MyApp.`; formatted `Namespace.Type.Method`; compiler-generated names are reduced
  to the source method (`<SaveAsync>d__12` -> `SaveAsync`, `<Create>b__0` -> `Create`); `unknown` when none.
- `Route`: the endpoint's route template (`api/organizations/{organizationId}/tasks`), never the raw path.

`Information` and `Debug` entries without a key may stay inline (`logger.LogInformation(...)`) and follow 3.2.
Entries at `Warning` and above outside `MyApp.Application/Logging/` are not allowed (rule O02).

### 3.6 Traces the user meets in the browser only

Some Observable traces describe a situation that never reaches the backend (`auth.nav.page_not_found`). The
frontend reports them; the backend logs them, so rule R15 still holds.

| Element | Rule |
|---------|------|
| Definition | A normal definition (3.1) with one parameter `string route`, marked with `[ClientReported]` (`src/backend/MyApp.Application/Logging/ClientReportedAttribute.cs`) |
| Endpoint | `POST /api/client-events`, body `{ "key": "{log key}", "route": "{path}" }`; rate policy `DefaultPerUser`; anonymous allowed |
| Dispatch | At startup, reflection over the `Logging` classes builds a map from `EventName` to every method marked `[ClientReported]`. An unknown key returns 400 and writes nothing |
| Route | Query string removed; path segments that are GUIDs or numbers replaced by `{id}`; at most 200 characters |
| Frontend | `reportClientEvent(key, route)` in `src/frontend/src/services/api/clientEventsService.ts`, called once per occurrence (once per page view for the Page not found page) |

---

## 4. Correlation and error responses

### 4.1 Trace context

| Hop | Rule |
|-----|------|
| Frontend | The request interceptor of `apiClient.ts` adds `traceparent: 00-{trace-id}-{span-id}-01` to every request, with a random 16-byte trace id and 8-byte span id from `crypto.getRandomValues`, lower-case hex. A retry keeps the trace id and draws a new span id. The trace id is kept on the request config for 4.3 |
| Backend | ASP.NET Core continues the incoming W3C context (default propagator); no code is needed beyond not overriding it. `Activity.Current.TraceId` is the trace id |
| Application Insights | `OperationId` = trace id for requests, dependencies, traces and exceptions |
| Log file | `TraceId`, `SpanId` (3.4) |
| Audit log | `AuditCdcInterceptor` sets `CorrelationId` to `Activity.Current.TraceId` (32 hex) when an activity exists, otherwise to a new GUID in `N` format (32 hex) |
| Error response | `traceId` field (4.2) |
| Incident report | `correlation.trace_ids` (10.1) |

A trace id is searched with `logs_by_correlation` and, for data changes, with `audit_trail_by_entity` results
that carry the same `correlation_id`.

### 4.2 Error response shape

Every error response of the backend is an RFC 9457 problem document with content type `application/problem+json`.

| Field | Type | Presence | Content |
|-------|------|----------|---------|
| `type` | string | Always | `about:blank` |
| `title` | string | Always | Standard reason phrase of the status |
| `status` | integer | Always | HTTP status |
| `detail` | string | Always | The English user message: the Observable trace Message for a keyed outcome, the validation message, or `An unexpected error occurred.` |
| `code` | string | Keyed outcomes only | The log key |
| `traceId` | string | Always | 32 lower-case hex |
| `errors` | object | Validation only | Field name (camelCase) -> array of messages |
| `exception` | object | Development environment only | `type`, `message` |

```json
{
  "type": "about:blank",
  "title": "Forbidden",
  "status": 403,
  "detail": "Your role does not allow this change.",
  "code": "task.create.not_allowed",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

`ExceptionHandlingMiddleware` maps exceptions as follows:

| Exception | Status | `code` | Log entry written by the middleware |
|-----------|--------|--------|-------------------------------------|
| `OutcomeException(key, status, detail)` | `status` | `key` | None (the keyed entry was written where the situation was detected) |
| `ValidationException` | 400 | - | None |
| `KeyNotFoundException`, `UnauthorizedAccessException` | 404, 403 | - | `platform.request.rejected` |
| `OperationCanceledException` with `RequestAborted` cancelled | No body | - | None |
| Any other | 500 | - | `platform.request.unhandled`, with the exception |

`OutcomeException` lives in `src/backend/MyApp.Application/Exceptions/OutcomeException.cs` and takes the key
constant, never a literal. The rate limiter's 429 response uses the same shape, with `detail`
`Too many requests. Please try again later.`

### 4.3 What the user sees

- The frontend shows the translated message for `code` from the locale key `apiErrors.{runbook anchor}` (for
  example `apiErrors.task-create-not-allowed`), falling back to the generic error message; it never shows
  `detail` untranslated in Italian.
- Under every error message for a failed request, the UI shows `Reference: {traceId}` with a copy action. The id
  is the body's `traceId`, or the trace id the frontend generated when there is no body (timeout, network error).
- Support pastes that id in the dashboard search (10.6).

---

## 5. Signatures and runbooks

### 5.1 Kinds

| Kind | Id | Source | Matches |
|------|----|--------|---------|
| `keyed` | The log key | An Observable trace (development.md 8.5) joined with its log definition (3.1) and its runbook (5.4) | Records whose `LogKey` equals the id |
| `template` | `t-` + 12 hex (6.5) | A runbook in `docs/support/unkeyed-runbooks.md` (5.5) | Unkeyed records whose template id equals the id |

A signature is never written by hand as JSON. It is generated by `pack build` (13.2).

### 5.2 Generation

1. Parse every test case file whose Type is `Edge case` or `Error handling`; read the Observable trace table.
2. Parse `src/backend/**/*.cs` (excluding `bin/` and `obj/`) with a syntax-only Roslyn parse: every method with a
   `LoggerMessage` attribute; resolve `EventName` through the `Keys` constants; read level, template, parameter
   names, the story comment above the attribute, file and line.
3. Join on the log key. Fail when a key has no definition, a definition's level differs from the Severity, or
   the story comment does not contain the test case's User Story.
4. Parse the area's `operations.md`; find the runbook whose heading is the anchor; fail when it is missing or
   invalid (5.4).
5. Parse `docs/support/unkeyed-runbooks.md` into `template` signatures.
6. Emit one record per signature (5.3) into `signatures.json` of the pack, ordered by id (ordinal).

### 5.3 Signature record

Normative schema: [docs/support/signature.schema.json](../support/signature.schema.json). The compiled table is
`{ "schema_version": 1, "signatures": [ ... ] }`.

| Field | Keyed | Template | Source |
|-------|-------|----------|--------|
| `id` | Log key | Template id | - |
| `kind` | `keyed` | `template` | - |
| `match` | `{ "log_key" }` | `{ "template_id", "event_name", "category", "exception_type", "top_frame", "template" }` | Definition / runbook |
| `area`, `user_story`, `test_case` | Set | `null` | Test case |
| `severity` | Test case Severity | Runbook `Severity` | - |
| `message` | Observable trace Message | `null` | Test case |
| `call_site` | `{ file, line, method, template, parameters }` | `null` | Definition |
| `runbook` | `{ anchor, path, text }` | same | Runbook; `text` is the Markdown of the section below the field table |
| `rate.expected_per_hour` | Number or `0` | same | Runbook `Expected rate` |
| `fingerprint_by` | Array of property names | same | Runbook `Fingerprint by` |
| `cause_category` | Closed list of 11.3 | same | Runbook `Cause category` |
| `status` | `active`, `noise`, `fixed` | same | Runbook `Status` |
| `fixed_in` | Version or `null` | same | Runbook |
| `noise` | `{ rationale, expires }` or `null` | same | Runbook |
| `rule_checks` | Array of `{ tool, arguments, expect }` | same | Runbook `Rule checks` |
| `history` | Array of `{ date, incident, outcome, reference }` | same | Runbook `History` |

### 5.4 Runbook format (area `operations.md`)

File `docs/areas/{area}/operations.md`, linked from the Files table of the area index. Exactly these headings, in
this order; one H2 per log key defined by a test case of the area, ordered by log key; no other H2.

```markdown
# {Area title} - Operations

## {runbook anchor}

| Field | Value |
|-------|-------|
| Log key | `{log key}` |
| Test case | [{TEST-CASE-ID}](test-cases/{TEST-CASE-ID}.md) |
| Expected rate | {n} per hour |
| Fingerprint by | {Property names, comma-separated, or None} |
| Cause category | {cause category} |
| Status | {Active, Noise or Fixed} |
| Fixed in | {Version or None} |
| Noise rationale | {One sentence or None} |
| Noise expires | {YYYY-MM-DD or None} |

### Meaning

{One to three sentences, in support terms: what happened and who is affected.}

### Check

1. {What to look at, in order. Name tools in backticks: `health_status`, `audit_trail_by_entity`.}

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| {tool} | {name=value, comma-separated, or -} | {expectation} |

### Resolve

1. {What support does, or "Hand over to development" with the reason.}

### History

- {YYYY-MM-DD} {incident id} {outcome} {pull request or ticket link}
```

| Element | Rule |
|---------|------|
| H2 text | The runbook anchor itself (development.md 3.1), so the heading's anchor equals it |
| Expected rate | `{n} per hour` with an integer n >= 0; `0 per hour` means any occurrence is abnormal |
| Fingerprint by | Template parameter names of the definition; `None` means one incident per signature and environment |
| Cause category | One value of the closed list in 11.3 (`access`, `data_missing`, ...); the category of the situation when it occurs as expected |
| Status | `Active`, `Noise`, `Fixed` |
| Fixed in | A version (`1.4.0`) when Status is `Fixed`, else `None` |
| Noise fields | Not `None` if and only if Status is `Noise`; expiry at most 180 days after the merge date |
| Rule checks | `None.` when there are no checks. Arguments: literals or `{signal.TraceId}`, `{signal.Properties.Name}`. Expect: `{path} {op} {literal}` with op one of `==`, `!=`, `>`, `>=`, `<`, `<=`, or `{path} exists`, `{path} absent`; `path` is a dot path into the tool result `data` |
| History | `None.` until the first outcome |
| Other sections | `Meaning`, `Check`, `Resolve` are never empty |

Example for the Tasks area:

```markdown
## task-create-not-allowed

| Field | Value |
|-------|-------|
| Log key | `task.create.not_allowed` |
| Test case | [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) |
| Expected rate | 5 per hour |
| Fingerprint by | OrganizationId |
| Cause category | access |
| Status | Active |
| Fixed in | None |
| Noise rationale | None |
| Noise expires | None |

### Meaning

A member whose role was changed to Viewer while the form was open tried to create a task. Expected after team changes.

### Check

1. Open the trace with `logs_by_correlation` and confirm the request was refused for this role.
2. When the rate is high, read the organization with `sql_entity_read` and `audit_trail_by_entity`: a change of
   many roles explains many entries at once.

### Rule checks

| Tool | Arguments | Expect |
|------|-----------|--------|
| health_status | - | status == "Healthy" |

### Resolve

1. No action when a role change precedes the entry. Otherwise hand over to development with the trace id.

### History

None.
```

### 5.5 Template runbooks (`docs/support/unkeyed-runbooks.md`)

For unkeyed patterns that support has decided about: accepted noise from the framework, or a known
infrastructure condition with no user-visible behaviour. A user-visible failure is never handled here: it gets a
test case and a log key (section 15).

Same structure as 5.4, with H1 `# Unkeyed signatures`, one introductory paragraph, then either `None.` or one H2
per template id ordered by id, each with this field table:

| Field | Value |
|-------|-------|
| Event name | {platform or monitor event name, or None} |
| Category | {logger category} |
| Exception type | {full type name or None} |
| Top frame | {Namespace.Type.Method or None} |
| Template | {discriminator values or normalised template, 6.5} |
| Severity | {Severity} |
| Expected rate | {n} per hour |
| Fingerprint by | {Property names or None} |
| Cause category | {cause category} |
| Status | {Active, Noise or Fixed} |
| Fixed in | {Version or None} |
| Noise rationale | {One sentence or None} |
| Noise expires | {YYYY-MM-DD or None} |

The H2 must equal the template id computed from the first five fields (rule O07). The dashboard (10.6) shows the
exact values for an unknown template so they can be copied.

### 5.6 Accepted noise

- Noise stays in the catalog with `Status` `Noise`: the analyser recognises it, opens no incident, and counts it.
- Noise is contextual: above `max(expected_per_hour, 1) x analyser.rate_multiplier` it escalates as
  `noise_out_of_context` (6.7).
- Noise expires. `pack build` fails when a `Noise expires` date is earlier than the build date; renew it or
  change the status in a pull request.
- A pull request that sets `Noise` or extends an expiry needs a reviewer other than its author.

---

## 6. Signals and the analyser

### 6.1 Log record (normal form)

Every source emits records in this form (`src/support/MyApp.Monitor/Sources/LogRecord.cs`):

| Field | Type | Rule |
|-------|------|------|
| `SourceId` | string | Profile source id |
| `RecordId` | string | Stable per record: SHA-256 hex (first 32) of source id, timestamp, trace id, category and message |
| `Timestamp` | DateTimeOffset | UTC |
| `Level` | enum | `Trace` to `Critical` |
| `Category` | string | Logger category or `unknown` |
| `EventName` | string? | `EventId.Name`, `Properties.LogKey`, or a monitor event name |
| `LogKey` | string? | `EventName` when it matches the log key grammar and does not start with `platform.` or `monitor.` |
| `MessageTemplate` | string? | When the source has it |
| `Message` | string | Formatted message |
| `TraceId`, `SpanId` | string? | Lower-case hex |
| `Properties` | dictionary | string -> string |
| `ExceptionType`, `ExceptionMessage`, `TopFrame` | string? | From properties (3.5) when present, else from the source's exception data |
| `Version`, `Host`, `Service` | string? | |
| `Environment` | string | Profile environment |

Records are masked with the `signal` section of the masking policy (8.1) before anything is stored.

### 6.2 Source interface

```csharp
public interface ISignalSource
{
    string Id { get; }
    string Kind { get; }                                    // "log_analytics", "log_files", "http_health", "world"
    Task StartAsync(SourceContext context, CancellationToken ct);       // validate settings, discovery
    IAsyncEnumerable<SignalBatch> ReadAsync(CancellationToken ct);      // batches of LogRecord, oldest first
    Task CommitAsync(SignalBatch batch, CancellationToken ct);          // save the checkpoint after analysis
    Task<SourceHealth> GetHealthAsync(CancellationToken ct);            // shown on the Sources page
}
```

- Delivery is at least once: a checkpoint is committed only after the batch has been analysed and stored.
  Duplicates are dropped by `RecordId` (table `seen_records`, kept 2 hours).
- A project adds a source kind (Oracle alert log, a queue's dead-letter count, syslog) by implementing this
  interface and registering it by `Kind`; the analyser does not change.
- A source that fails keeps its checkpoint, retries with backoff (10 s doubling to 5 min), and reports
  `Unhealthy` with the error; after 15 minutes unhealthy it raises `monitor.source.failing` (Error).

### 6.3 Cloud source (`log_analytics`)

| Aspect | Rule |
|--------|------|
| Identity | `Monitor:Azure:Credential` = `developer` (local: `AzureCliCredential`, then `VisualStudioCredential`, then `AzureDeveloperCliCredential`) or `managed_identity` (deployed: `ManagedIdentityCredential`, client id from `Monitor:Azure:ManagedIdentityClientId` when user-assigned). Never keys or connection strings |
| Roles | `Log Analytics Reader` on the workspace; `Reader` on the scope used for discovery |
| Discovery | When the profile gives no `workspace_id`, list resources with the profile tag (`discovery.tag_name` = `discovery.tag_value`) and type `Microsoft.OperationalInsights/workspaces` in `Monitor:Azure:SubscriptionIds` (empty: all subscriptions the identity can read). Exactly one match is required; zero or several stop the source with an error that names the tag. Results are cached in `discovered_resources` and refreshed every 6 hours |
| Query | Every `poll_seconds` (default 60), on `ingestion_time()` so late rows are not lost (below) |
| Watermark | The largest `ingestion_time()` read, saved in `source_watermarks`; first run starts at `now - lookback_minutes_first_run` |
| Paging | At most `max_rows_per_poll` (default 5000); when reached, the next poll continues from the new watermark immediately |

```kusto
union AppTraces, AppExceptions
| where ingestion_time() > datetime({watermark}) and ingestion_time() <= datetime({now})
| where AppRoleName in ({app_role_names})
| where SeverityLevel >= {min_level} or isnotempty(tostring(Properties.LogKey))
| extend IngestedAt = ingestion_time()
| project IngestedAt, TimeGenerated, Type, SeverityLevel, Message, ExceptionType, OuterMessage, Method,
          OperationId, ParentId, AppVersion, AppRoleInstance, Properties
| order by IngestedAt asc
| take {max_rows_per_poll}
```

Mapping: `Message` (or `OuterMessage` for exceptions) -> `Message`; `Properties["{OriginalFormat}"]` ->
`MessageTemplate`; `Properties.CategoryName` -> `Category`; `Method` -> `TopFrame` when no `TopFrame` property.

### 6.4 On-premises sources (`log_files`, `http_health`)

`log_files` tails files with saved offsets:

| Aspect | Rule |
|--------|------|
| Files | Glob patterns in `paths`; `format` `jsonl` (3.4) or `text` with `line_regex` (named groups `timestamp`, `level`, `category`, `message`) |
| File identity | SHA-256 of the first 1024 bytes (`head_hash`) plus the path; a file shorter than 1024 bytes is re-identified when it grows |
| Offsets | Table `source_offsets`: source id, head hash, path, byte offset, updated at. Committed after analysis |
| Rotation | When the file at a path has a new head hash, the old offset is kept under its head hash; the old file is read to its end if it is found under another name in the glob, then dropped. Files are read in last-write-time order |
| Truncation | Size smaller than the saved offset: restart at 0 |
| Partial lines | Only complete lines (ending with LF) are read; the remainder waits for the next poll |
| Long lines | Lines over 64 KB are cut and flagged `Properties.Truncated = "true"` |
| Text multiline | A line not matching `line_regex` is appended to the previous record's message (stack traces) |
| First run | `start_at` `end` (default) or `beginning` |
| Poll | Every `poll_seconds` (default 5) |

`http_health` polls the backend's `/health` every `poll_seconds` (default 60) and emits `monitor.api.unreachable`
after `failures_before_signal` (default 3) consecutive failures, and `monitor.api.unhealthy` for each check that
is not `Healthy`. The backend's `/health` returns
`{ "status": "Healthy|Degraded|Unhealthy", "checks": [ { "name": "sqlserver", "status": "Healthy", "durationMs": 12 } ] }`
and never exception text.

`world` reads a simulated world directory (11.6) and exists for tests and training only; a profile used in
production never lists it (rule O08).

### 6.5 Template extraction

For records without a `LogKey`:

1. Discriminator: for an `EventName` in the tables of 3.5, the values of its discriminator properties joined with
   `|`, in table order. Otherwise the normalised template: `MessageTemplate` when present, else `Message` with,
   in this order, GUIDs -> `<guid>`, ISO 8601 timestamps -> `<ts>`, e-mail addresses -> `<email>`, URLs -> `<url>`,
   IPv4 and IPv6 addresses -> `<ip>`, hex runs of 8 or more -> `<hex>`, quoted strings -> `<str>`, numbers ->
   `<num>`; whitespace collapsed to one space; cut at 300 characters.
2. Canonical string: `{EventName or ""}\n{Category}\n{ExceptionType or ""}\n{TopFrame or ""}\n{discriminator}`.
3. Template id: `t-` + the first 12 lower-case hex of SHA-256 of the canonical string in UTF-8.

No clustering or similarity merge: the same input always gives the same id. `monitor analyse --explain` (14.4)
prints the five parts for any record.

### 6.6 Matching

| Order | Condition | Result |
|-------|-----------|--------|
| 1 | `LogKey` present and a `keyed` signature has it | Matched |
| 2 | `LogKey` present, no signature | `unknown_key` (the application is newer than the pack) |
| 3 | No key; a `template` signature has the template id | Matched |
| 4 | Otherwise | `unknown_template`; upserted into `unknown_templates` |

### 6.7 Rate, severity and escalation

Counters are kept per signature or template id, environment and fingerprint in hourly buckets (table
`counters`). "Rate" is the count in the last `analyser.window_minutes` (default 60) scaled to one hour.

| Trigger | Condition | Escalates | Verdict |
|---------|-----------|-----------|---------|
| `severity` | Matched, status `active`, severity `Error` or `Critical` | Each new fingerprint | Rule |
| `rate` | Matched, status `active`, severity `Information` or `Warning`, rate > `max(expected_per_hour, 1) x rate_multiplier` (default 3) and count >= `rate_min_count` (default 5) | Yes | Residual |
| `regression` | Matched, status `fixed`, record `Version` >= `fixed_in` (semantic version; a missing version counts as newer) | Yes; severity raised one step, at most `Critical` | Rule |
| `noise_out_of_context` | Matched, status `noise`, rate above the threshold of `rate` | Yes | Residual |
| `noise_expired` | Matched, status `noise`, `expires` past at run time | Treated as `active` with severity at least `Warning` | As `active` |
| `unknown` | `unknown_key` or `unknown_template` at level `Error` or above; or at `Warning` with count >= `unknown_warning_min_count` (default 3) in the window | Yes | Residual |
| `health` | `monitor.api.unreachable`, `monitor.api.unhealthy`, `platform.health.unhealthy` | Yes | Rule |
| `manual` | Created from the dashboard search (10.6) | Yes | Residual on request |

Anything else updates counters only. `Information` unknowns are counted on the Signals page and never escalate.

### 6.8 Fingerprint and dedup

- Fingerprint: first 16 hex of SHA-256 of `{environment}\n{signature or template id}\n{values of fingerprint_by, in order, joined with |}`.
- While an incident with the fingerprint is `open` or `acknowledged`, new records update `last_seen`, `count`,
  `rate_per_hour`, append the trace id (at most 50) and evidence (at most 20 items); no new notification except
  on a severity raise.
- After the incident is closed, a new record opens a new incident with `previous_incident_id`, except when the
  outcome was `noise` (the signature then carries `noise`) or `duplicate`.
- Inside a suppression window (10.4), matching records update counters only.
- Residual triggers queue an investigation (section 11). The incident is written first, with the rule data;
  the investigation updates it later.

### 6.9 Rule engine

For every escalation, before any model call:

1. Attach the signature record and the runbook text.
2. Correlate: `logs_by_correlation` on the first trace id; for `fingerprint_by` properties that name an entity
   key bound in `entities.json` (property `{Entity}Id`, case-insensitive), `sql_entity_read` and
   `audit_trail_by_entity`.
3. Run the runbook's rule checks and record them in `runbook.checks`. A failed check makes the incident
   residual (the evidence contradicts the runbook), whatever its trigger.
4. Build `rule_verdict` for a matched signature: `cause` = the runbook Meaning, `cause_category` = the runbook
   Cause category, confidence 1.0 when every check passes and 0.5 when one fails, `evidence_refs` = `E1` and the
   check results. Unknowns get no rule verdict.

All tool calls here go through the executor (section 7) with caller `rules`.

---

## 7. Tools

### 7.1 Tool catalog (`docs/support/tools.json`)

| Field | Type | Rule |
|-------|------|------|
| `schema_version` | integer | `1` |
| `tools[]` | array | One item per tool, ordered by name |
| `.name` | string | `^[a-z]+(_[a-z]+)*$`, unique; the name the model calls |
| `.family` | string | `logs`, `entity`, `audit`, `health`, `signatures` (closed; a new family is a retraining trigger) |
| `.access` | string | Always `read` |
| `.description` | string | One or two sentences for the model; no project vocabulary |
| `.parameters` | object | JSON Schema subset (below), `type: object`, `additionalProperties: false` |
| `.limits` | object | `max_rows`, `timeout_seconds`, `max_output_chars`, `max_calls_per_incident`, optional `lookback_hours` |
| `.implementations` | object | Source kind -> implementation settings (query text, file fields); the executor picks the one for the bound source's kind. In `columns` maps, a value is a source field or dot path (`Properties.LogKey`), `a\|b` takes the first non-empty, `=text` is a constant. In query text, `{name}` is replaced only by a validated parameter or a limit |
| `.output` | object | `masking`: the masking policy section applied to the result (`tools.{name}` or `entities`) |

JSON Schema subset allowed in `parameters` and in every schema under `docs/support/`: `type` (string or array
with `null`), `properties`, `required`, `additionalProperties: false`, `enum`, `pattern`, `minimum`, `maximum`,
`minLength`, `maxLength`, `items`, `maxItems`, `anyOf`, `$ref` to `#/$defs/...`, and the annotations `$schema`,
`$id`, `title`, `description`, `$defs`. One extension:
`"x-enum-from": "entities"` on a string parameter means its enum is the entity names of `entities.json`.

Every tool result has the same envelope:

| Field | Content |
|-------|---------|
| `tool`, `call` | Name and call number within the incident |
| `status` | `ok`, `empty`, `error`, `denied`, `timeout`, `budget_exceeded`, `duplicate` |
| `evidence_id` | `E{n}`, assigned by the executor in call order; `E1` is the triggering signal |
| `source` | Profile source id |
| `rows`, `truncated`, `fields_dropped` | Counts after limits and masking |
| `data` | The masked result |
| `error` | Short reason when status is not `ok` or `empty` |

### 7.2 Entity binding (`docs/support/entities.json`)

| Field | Rule |
|-------|------|
| `schema_version` | `1` |
| `entities[].name` | `^[a-z]+(_[a-z]+)*$`; the value of the `entity` parameter |
| `.description` | One sentence, shown to the model in the tool list |
| `.source_kind` | `sqlserver` in the template; a project adds kinds with implementations |
| `.table` | `schema.table` |
| `.key` | Key column; `.key_type` `guid`, `int` or `string` |
| `.audit_entity_type` | The value of the audit log's entity type for this entity, or `null` |

The fields an entity read returns are the entity's section of the masking policy (8.1), nothing else. The SQL is
`SELECT TOP (1) {allowed columns} FROM {table} WHERE {key} = @id`, with every identifier checked against
`^[A-Za-z_][A-Za-z0-9_]*$` and bracket-quoted, and the key value bound as a typed parameter.

### 7.3 Executor rules

| Rule | Detail |
|------|--------|
| Validation | Arguments are validated against the tool's schema before execution; failure returns `denied` with the reason |
| Read only | SQL connections use `ApplicationIntent=ReadOnly` and a principal with `SELECT` on the bound tables only; the executor runs only statements built from the catalog |
| Budgets | Per call: `timeout_seconds`, `max_rows`, `max_output_chars` (cut with `truncated: true`). Per incident: `max_calls_per_incident` per tool and the constraint totals (section 9) |
| Duplicates | The same tool with the same arguments in the same incident returns `duplicate` with the earlier `evidence_id`, without running |
| Masking | Applied to `data` before the envelope leaves the executor (section 8) |
| Audit | One row in `tool_audit` and one log entry `monitor.tool.call` per call, including denied ones: time, incident id, caller (`rules`, `investigator`, `dashboard:{user}`, `cli:{user}`), tool, masked arguments, source id, status, rows, output characters, duration |
| Identity | The source's identity from the profile (6.3); in local use, the developer's |

### 7.4 Working example tools

| Tool | Family | Parameters | Returns (`data`) | Implementations |
|------|--------|------------|------------------|-----------------|
| `logs_by_correlation` | logs | `trace_id` (32 hex), `limit` (1 to 200, default 100) | `records[]`: `timestamp`, `kind` (`trace`, `exception`, `request`, `dependency`), `level`, `category`, `log_key`, `template`, `message`, `span_id`, `exception_type`, `top_frame`, `name`, `result_code`, `duration_ms` (the last three for requests and dependencies) | `log_analytics`, `log_files`, `world` |
| `sql_entity_read` | entity | `entity` (from entities), `id` | `entity`, `id`, `found`, `fields` | `sqlserver`, `world` |
| `audit_trail_by_entity` | audit | `entity` (from entities), `id`, `limit` (1 to 50, default 20) | `changes[]`: `timestamp`, `action`, `user_id`, `changed_properties`, `old_values`, `new_values`, `correlation_id`; values filtered by the entity's masking section | `sqlserver`, `world` |
| `health_status` | health | none | `status`, `checks[]` (`name`, `status`, `duration_ms`) | `http_health`, `world` |
| `signature_lookup` | signatures | exactly one of `signature_id`, `log_key`, `template_id` | `found`, `signature` (record of 5.3 without `call_site.file`) | `pack` |

Normative catalog: [docs/support/tools.json](../support/tools.json). `log_files` searches the bound files for the
trace id, newest file first, stopping at `limit` rows or `lookback_hours`.

---

## 8. Masking

### 8.1 Policy (`docs/support/masking-policy.json`)

| Field | Rule |
|-------|------|
| `schema_version` | `1` |
| `free_text_max_chars` | Default cut for `free_text` (500) |
| `patterns[]` | Ordered: `name`, `regex` (.NET syntax), `replacement`, optional `ignore_case` |
| `protected[]` | Regexes for spans that patterns must not touch (GUIDs, trace and span ids, ISO timestamps) |
| `signal` | `fields` (log record field -> class) and `properties` (property name or `*Suffix` glob -> class) |
| `tools.{name}` | Result field path -> class, for `logs`, `health` and `signatures` tools |
| `entities.{entity}` | Field -> class. These are also the only columns `sql_entity_read` selects, and the only keys kept in audit `old_values` and `new_values` |

Classes:

| Class | Treatment |
|-------|-----------|
| `id` | Passes if it is a GUID, an integer or 32/16 hex; else `[masked]` |
| `code` | Passes if it matches `^[A-Za-z0-9_.:/{}+<>\[\]-]{1,200}$`; else `[masked]` |
| `number`, `boolean`, `timestamp` | Passes if it parses as the type (a date counts as a timestamp); else `[masked]` |
| `free_text` | Protected spans kept, patterns applied in order, then cut to `free_text_max_chars` |
| `hash` | Replaced by `h:` + the first 12 hex of HMAC-SHA256 with `Monitor:Masking:HashKey` over the trimmed, lower-cased value; equal values stay correlatable |
| `entity` | An object (or a JSON string holding one) filtered by the masking section of the entity named in the call |
| `pack` | Content of the operations pack (built from `docs/`, no personal data); passes unchanged |

A field with no class is dropped and counted in `fields_dropped`. Nested objects and arrays use dot paths with
`[]` for array items (`records[].message`). Field names match case-insensitively. In `signal.properties`, `*Id`
matches every name ending in `Id`; an exact name wins over a glob.

Pattern application: find all protected spans first; apply each pattern in order to the text outside them; then
cut. The shipped patterns cover secrets, tokens, e-mail addresses, IBANs, Italian tax codes, card numbers, IPv4
addresses, phone numbers and digit runs of 9 or more; a project adds its own country formats to the list.

### 8.2 Where masking applies

| Point | Section used |
|-------|--------------|
| A source record, before analysis stores it | `signal` |
| Every tool result, before the envelope leaves the executor | `tools.{name}` or `entities.{entity}` |
| Tool arguments written to the audit row | `signal.properties` classes by argument name, else `free_text` |
| Report fields written by the investigator (`free_text` fields of 10.1) | Patterns, as a second line |
| Notification rendering | Nothing new: notifications only read the stored, already masked report |

If `Monitor:Masking:HashKey` is missing, the monitor refuses to start. The model never receives the key,
connection strings, tokens or unmasked values.

---

## 9. Constraints (`docs/support/constraints.json`)

| Field | Rule |
|-------|------|
| `schema_version` | `1` |
| `constraints[].id` | `C` + two digits, never reused |
| `.type` | `access`, `budget`, `scope`, `data`, `action` |
| `.text` | One sentence; given to the investigator verbatim and shown on the dashboard |
| `.params` | Values the enforcer reads |
| `.enforced_by` | One or more of `pack_verify`, `executor`, `investigator_host`, `notifier`, `store` |

Every constraint is enforced in code by the component it names; the text is information for the model, never
the enforcement. Normative file: [docs/support/constraints.json](../support/constraints.json).

---

## 10. Incident report, store and notifications

### 10.1 Incident report

Normative schema: [docs/support/incident-report.schema.json](../support/incident-report.schema.json). The same
object is stored, shown, rendered into e-mail and ticket, and consumed by `fix.agent.md`.

| Group | Fields |
|-------|--------|
| Identity | `schema_version`, `id` (`INC-{yyyyMMdd}-{nnnn}`), `fingerprint`, `environment`, `status` (`open`, `acknowledged`, `closed`), `trigger` |
| Signature | `signature` (`kind` `keyed`, `template` or `none`; `id`, `log_key`, `template_id`, `template`), `area`, `user_story`, `test_case` |
| Window | `severity`, `first_seen`, `last_seen`, `count`, `rate_per_hour`, `previous_incident_id` |
| Correlation | `correlation.trace_ids[]`, `correlation.entities[]` (`entity`, `id`) |
| Evidence | `evidence[]`: `id` (`E{n}`), `source`, `reference` (what a human opens: a query, a file and line, an entity and id), `observed_at`, `excerpt` (masked, at most 1000 characters) |
| Verdicts | `rule_verdict` and `model_verdict`, each `null` or `{ cause_category, cause, confidence, evidence_refs[] }`; the dashboard shows both side by side |
| Investigation | `investigation`: the model's output (11.3) or `null`; `model` (`model_id`, `version`, `tier`, `device`, `tool_calls`, `abstained`) or `null` |
| Reproduction | `reproduction` (`preconditions[]`, `steps[]`, `expected`, `observed`) or `null` |
| Runbook | `runbook` (`anchor`, `path`, `checks[]` with `tool`, `expect`, `passed`) or `null` |
| Actions | `actions_taken[]`: `at`, `action`, `reference` |
| Outcome | `outcome`: `null` until closed, then `value` (`fixed`, `noise`, `reclassified`, `duplicate`, `wontfix`, `unknown_resolved`), `ticket`, `pull_request`, `closed_at`, `closed_by`, `note` |
| Versions | `pack_version`, `monitor_version` |

Rules: no cause without at least one evidence reference; a model verdict is always labelled as such; every
evidence item has a reference a human can open; every text field is masked.

### 10.2 Store schema (SQLite)

File from the profile `store.path` (default `data/monitor.db`), WAL mode, created and migrated at start by
numbered SQL scripts in `src/support/MyApp.Monitor/Store/Migrations/`. Timestamps are ISO 8601 UTC text.
Access goes through `IIncidentStore`, so a project can add another store kind.

| Table | Columns (key in bold) | Purpose |
|-------|-----------------------|---------|
| `schema_info` | **`version`** | Applied migration |
| `incidents` | **`id`**, `fingerprint`, `status`, `severity`, `trigger`, `signature_id`, `template_id`, `environment`, `first_seen`, `last_seen`, `count`, `verdict_source`, `report_json`, `previous_incident_id`, `created_at`, `updated_at`, `closed_at`, `outcome` | One row per incident; `report_json` is the full report. Index (`fingerprint`, `status`), (`status`, `severity`) |
| `incident_events` | **`id`**, `incident_id`, `at`, `kind`, `data_json` | Timeline: created, record appended, check run, investigation, notification, status change |
| `investigations` | **`incident_id`**, `status` (`queued`, `running`, `done`, `abstained`, `failed`, `skipped`), `attempts`, `queued_at`, `started_at`, `finished_at`, `error` | Investigator queue |
| `counters` | **`signature_or_template`, `environment`, `fingerprint`, `bucket_start`**, `count` | Hourly counts |
| `unknown_templates` | **`template_id`**, `event_name`, `category`, `exception_type`, `top_frame`, `template`, `level`, `first_seen`, `last_seen`, `count`, `sample_trace_id` | Unknown patterns for review |
| `tool_audit` | **`id`**, `at`, `incident_id`, `caller`, `tool`, `arguments_json`, `source_id`, `status`, `rows`, `output_chars`, `duration_ms`, `error` | Audit line per tool call |
| `notifications` | **`id`**, `incident_id`, `channel`, `kind` (`sent`, `draft`, `suppressed`, `failed`), `at`, `target`, `reference`, `error`, `body` | Sent and draft notifications |
| `suppression_windows` | **`id`**, `name`, `starts_at`, `ends_at`, `match`, `created_by`, `reason` | Windows from the dashboard (profile windows are read from the profile) |
| `source_offsets` | **`source_id`, `head_hash`**, `path`, `offset`, `updated_at` | File positions |
| `source_watermarks` | **`source_id`**, `watermark` | Query positions |
| `seen_records` | **`source_id`, `record_id`**, `seen_at` | At-least-once dedup |
| `discovered_resources` | **`source_id`**, `resource_id`, `refreshed_at` | Discovery cache |

Retention (daily at 03:00 local): closed incidents and their events, notifications and audit rows older than
`store.retention_days` (default 90); counters older than 30 days; `seen_records` older than 2 hours.

### 10.3 Gates

| Action | Condition |
|--------|-----------|
| Ticket created automatically | `rule_verdict` present, signature `keyed` or `template`, trigger `severity`, `regression` or `health`, severity >= `gates.ticket.min_severity` (default `Error`) |
| Ticket draft | Every other escalation; sent when a dashboard user approves it |
| E-mail | Severity >= `gates.email.min_severity` (default `Error`) on creation, on a severity raise and on regression; a daily digest of `Warning` incidents at `gates.email.digest_time` |
| Model verdicts | Never create a ticket automatically (`gates.ticket.allow_model_verdict` exists and must stay `false` in version 1) |
| Automatic remediation | None |

### 10.4 Suppression

- Windows come from the profile (`suppression[]`: `name`, `starts_at`, `ends_at` or `weekly` with `day`,
  `from`, `to`; `match`: a glob over signature or template ids) or from the dashboard.
- Inside a window, matching records update counters only; at the end, one `incident_events` entry of kind
  `suppression_summary` lists the counts.
- Noise signatures are suppressed permanently within their context (5.6).

### 10.5 Notifications

| Adapter | Settings (profile `notifications`) | Behaviour |
|---------|------------------------------------|-----------|
| `email` | `smtp_host`, `smtp_port`, `use_tls`, `from`, `to[]`, `username_setting`, `password_setting`, `template` | SMTP with the .NET client; subject is the first line of the rendered template |
| `ticket_webhook` | `url`, `method` (`POST`), `headers` (name -> `${setting:...}`), `template`, `id_path` | Sends the rendered JSON with `Idempotency-Key: {incident id}:{kind}`; reads the ticket id at `id_path` (dot path) of the response; 3 retries with 10, 60, 300 s backoff; then `failed` and an Error on the dashboard |

Templates live in `docs/support/templates/` and are copied into the pack. Syntax: `{{path}}` inserts a field of
the report (dot path, arrays by index); `{{json:path}}` inserts it JSON-escaped; `{{evidence_lines}}` inserts one
line per evidence item (`{id} {source} {reference}: {excerpt}`); `{{dashboard_url}}` the incident page; the
`json:` prefix also applies to these two. A `null` value renders as empty. An unknown path renders as empty and is
reported by `pack verify`, which renders every template with
[docs/support/examples/incident-report.example.json](../support/examples/incident-report.example.json) and
requires the ticket template's output to parse as JSON. Profile `perimeter.evidence_in_notifications: false`
removes excerpts from both adapters.

### 10.6 Dashboard

ASP.NET Core Razor Pages inside the monitor, reading the store only (never the model live).

| Page | Route | Content and actions |
|------|-------|---------------------|
| Open incidents | `/` | Filters: severity, trigger, verdict source, area; counts per status |
| Incident | `/incidents/{id}` | Report, evidence timeline, rule and model verdicts side by side, actions, notifications; buttons: Acknowledge, Close with outcome (value, ticket, pull request, note), Send draft, Investigate again |
| Drafts | `/drafts` | Ticket and e-mail drafts awaiting approval |
| Search | `/search` | By trace id or by entity and id: runs `logs_by_correlation` or `audit_trail_by_entity` as `dashboard:{user}`; "Open incident" creates a `manual` incident |
| Signals | `/signals` | Counters per signature, noise counters with expiry dates, unknown templates with the five fields of 6.5 |
| Review | `/review` | Closed incidents whose outcome needs a repository change (section 15); download of the report JSON |
| Sources | `/sources` | Source health, offsets, watermarks, discovery, pack version, investigator tier and device |

JSON API: `GET /api/incidents?status=&severity=`, `GET /api/incidents/{id}`, `POST /api/incidents/ingest`
(accepts an incident report for hybrid, 12.4), `GET /api/health`.

Authentication: `dashboard.auth` = `local` (binds to loopback only, no sign-in, for a developer machine) or
`entra` (Microsoft Entra sign-in; access for the group in `dashboard.allowed_group_id`; the ingest endpoint
accepts an app token with the role `Incidents.Ingest`).

---

## 11. Investigator

The lifecycle shared by all models (training on the template's protocol with synthetic multi-domain data,
exports, runtime ladder, `models/model.lock`, first training) is in [models.md](models.md). This section defines
only what is specific to the model id `investigator`.

### 11.1 When it runs

Only for escalations with a residual trigger: `unknown`, `rate`, `noise_out_of_context`, a failed rule check,
and `manual` on request. One investigation at a time (`investigator.concurrency`, default 1), oldest first,
after the incident has been written with its rule data.

### 11.2 Host and tiers

| Setting (profile `investigator`) | Values | Default |
|----------------------------------|--------|---------|
| `tier` | `none`, `small`, `large` | `small` |
| `device` | `auto`, `gpu`, `cpu` | `auto` |
| `max_tool_calls` | 1 to the constraint cap | `small` 6, `large` 10 |
| `timeout_seconds` | Wall time per investigation | 600 |
| `concurrency` | Investigations at once | 1 |

The tiers are chosen by evaluation (11.4), starting from the owner's Qwen candidates ([models.md](models.md) 6.1):

| Tier | Candidate | Trained? |
|------|-----------|----------|
| `small` (CPU-only machines) | Qwen3.5-4B; alternative Qwen3.5-2B | Only if the untrained model fails 11.4 |
| `large` (GPU or large memory) | Qwen3.6-35B-A3B (mixture of experts, 3B active), 4-bit GGUF | Never: used untrained with the protocol prompt and the report grammar (`trainable: false`). If it fails, the trained alternative is Qwen3.5-9B |

All run with `enable_thinking: false`. The `tool_role` of these models is `user` (11.3).

The monitor does not load the model: it uses a model host started on its own (`src/support/model-host/`, [models.md](models.md)
7.4 and 9.2) and asks it who it is. The selection runs at start and every `Monitor:Investigator:ProbeSeconds`, in this order; the result is
recorded in `model.device` and on the Sources page:

1. `tier: none` -> rules only (state `off`).
2. Read the `investigator` entry of `models/model.lock` (path `Monitor:Investigator:ModelLock`) for the context length and memory of each
   tier. Missing lock, entry or usable export -> rules only, `monitor.investigator.unavailable` (Warning).
3. The lock's protocol hash must equal the hash of the protocol block of the loaded pack; otherwise rules only.
4. Ask the hosts, the profile's tier first and, for `large`, `small` after it. Each tier's address is
   `Monitor:Investigator:Endpoints:{tier}`, else `Monitor:Investigator:Endpoint`. A host is used when it serves
   `investigator-{lock version}`, runs on a device the profile allows (`auto` accepts what the launcher chose, read from the alias:
   GPU, then CPU) and, on the CPU, the machine has the memory the export needs. The launcher makes the GPU-then-CPU choice; the monitor does
   not measure GPU memory. No usable host -> rules only, with the reason of each tier.
5. A runtime failure marks the investigation `failed` and keeps the rule verdict; three failures in a row switch the host to rules only until
   restart (state `disabled`).

The host decodes under the grammar of 11.3; the runtime itself is defined in `models.md`.

### 11.3 Protocol

The rules text is the block between `<!-- protocol:begin -->` and `<!-- protocol:end -->` in
[.github/agents/support.agent.md](../../.github/agents/support.agent.md). The training generator and the pack
builder copy it from there; it is not repeated anywhere else.

Conversation, using the base model's chat template:

| Turn | Role | Content |
|------|------|---------|
| 1 | system | The protocol block |
| 2 | user | `<incident>{json}</incident>` (trigger, severity, signature or template, counts, rate, the masked triggering records as `E1`), `<pack>{json}</pack>` (signature record and runbook text when matched; nothing else), `<tools>{json}</tools>` (name, description, parameters of the profile's tools, and the names and descriptions of `entities.json`), `<constraints>` (constraint texts), `<budget calls="n" />` |
| 3, 5, ... | assistant | Exactly one of `<call>{"tool": "...", "arguments": {...}}</call>` or `<report>{investigation}</report>` |
| 4, 6, ... | tool; `user` when the template has no tool role or renders `tool` in its own tool format (Qwen3.5 and later, `tool_role` in `candidates.json`) | `<result>{envelope of 7.1}</result>`, followed by `<budget calls="n" />` |

Grammar: the assistant turn is constrained to `call | report`; `arguments` to the tool's parameter schema;
`report` to `#/$defs/investigation` of the incident report schema:

| Field | Rule |
|-------|------|
| `verdict` | `explained` or `abstain` |
| `abstain_reason` | `null` when explained; else `insufficient_evidence`, `conflicting_evidence`, `out_of_scope`, `budget_exhausted` |
| `cause_category` | `access`, `data_missing`, `data_conflict`, `dependency_unavailable`, `configuration`, `capacity`, `code_defect`, `external_input`, `expected_behaviour`, `unknown` |
| `cause` | At most 300 characters; `null` when abstaining |
| `confidence` | 0 to 1, two decimals |
| `evidence_refs` | Evidence ids from results already received; at least one when explained |
| `area`, `user_story` | From the pack when the evidence names them, else `null` |
| `reproduction` | `preconditions[]`, `steps[]`, `expected`, `observed`, or `null` |
| `next_checks` | Up to 5 short strings for a human, always present (may be empty) |

Stop rules, enforced by the host:

| Rule | Host action |
|------|-------------|
| Calls left reach 0 | Next turn constrained to `report` |
| Same tool and arguments twice | Result `duplicate`; on a second duplicate, next turn constrained to `report` |
| Two consecutive results `error`, `denied` or `timeout` | Next turn constrained to `report` |
| Wall time reached | Stop; investigation `failed`; rule verdict kept |
| Context above 75 % of the model's length | Oldest results replaced by `{"evidence_id": "...", "summarised": true, "rows": n}` |

Abstain when: no received evidence supports a cause; the cause would need a source no tool covers; the evidence
points both ways; or the budget ended first. Abstaining is a correct answer, not a failure.

Validation after the report: schema valid, every `evidence_ref` exists, `user_story` exists in the pack or is
`null`, `confidence` < 0.5 with `explained` is rewritten to `abstain` (`insufficient_evidence`). One repair turn
with the validation error is allowed; a second failure marks the investigation `failed`. The host then writes
`model_verdict`, `investigation`, `reproduction` (when the rule data had none) and `model`.

### 11.4 Evaluation

Suites:

| Suite | Content | Location |
|-------|---------|----------|
| `synthetic-holdout` | Scenarios from fictional domains held out as whole domains (11.6) | Generated by `python -m training.investigator.generate --split holdout` |
| `sample-app` | Scenarios on the sample application's pack, written by hand, never used for training | `docs/support/bench/investigator/{scenario id}.json` |

A scenario holds a world spec, the injected fault, the trigger, and the gold answer: `verdict`,
`cause_category`, `area`, `user_story`, `supporting_evidence` (tool and arguments whose results support the
cause), `min_calls`.

Metrics, computed deterministically from the report (no judge model):

| Metric | Definition | Threshold |
|--------|------------|-----------|
| `report_valid` | Reports valid without repair / all | >= 0.99 |
| `cause_correct` | Answerable scenarios with `explained`, gold `cause_category` and gold `area` / answerable | >= 0.80 |
| `story_correct` | Correct `user_story` / scenarios whose gold has one | >= 0.75 |
| `grounded` | Explained reports whose `evidence_refs` include a result of a `supporting_evidence` call / explained reports | >= 0.95 |
| `abstain_correct` | `abstain` on scenarios whose gold is abstain / those scenarios | >= 0.90 |
| `wrongful_abstain` | `abstain` on answerable scenarios / answerable | <= 0.15 |
| `denied_calls` | Calls denied by the executor / all calls | <= 0.02 |
| `budget_exhausted` | Investigations stopped by the call budget / all | <= 0.05 |
| `extra_calls` | Median of calls minus `min_calls` | <= 2 |
| `latency_p95` | Seconds per investigation, per tier and device | Reported, not gated |

Release gate: every gated metric passes on both suites. The smallest tier that passes is the default. Thresholds
live in `training/investigator/thresholds.json` with exactly these values; a change updates this table in the
same pull request. `python -m training.investigator.eval --export {merged|gguf} --run {run_id} [--suite {suite}] [--tier {tier}]` writes
`artifacts/eval/investigator/{suite}-{tier}.json` and the run's `eval-{export}.json`. `gguf` is the gating runtime; `--export` is required.

### 11.5 Untrained baseline

Before any training run, `python -m training.investigator.eval --candidates --tier {tier}` measures the untrained candidates on the same suites.
Training starts only if they fail. They run under transformers, which has no constrained decoding, so `report_valid` is a lower bound of what the
grammar would give ([models.md](models.md) 9.2 step 5). An untrained-only candidate (the `large` tier's mixture-of-experts model) stops here: it is exported and recorded without a training run.

### 11.6 Synthetic incident generator

Location `training/investigator/`, commands `generate` and `validate` (one command per step, `models.md`).

| Part | Specification |
|------|---------------|
| Domains | At least 24 fictional applications (for example library loans, veterinary clinic, fleet maintenance, school timetables, warehouse picking, event tickets), each a world spec in `training/investigator/worlds/{domain}.json` written in the template's formats: areas, stories, test cases with Observable traces, log definitions with templates, runbooks, `entities.json`, masking policy. At least 4 domains are held out whole for `synthetic-holdout` |
| World | A seeded simulator renders a world directory: `logs.jsonl` (format 3.4), `entities/{entity}.jsonl`, `audit.jsonl`, `health.json`, plus a pack built from the world spec. Synthetic personal data (names, e-mails, phones, tax codes) is present so the model learns to work with masked values |
| Faults | One per scenario, mapped to a `cause_category`: access revoked during an action, entity removed concurrently, duplicate or conflicting data, dependency down or slow, wrong configuration, load surge, code defect (exception with an application top frame after a version change), bad external input, expected behaviour at high rate |
| Triggers | `unknown` (template and key), `rate`, `noise_out_of_context`, failed rule check |
| Unanswerable | About 20 %: the cause lies in a source no tool covers, the evidence conflicts, or the budget is too small; gold is `abstain` |
| Tool results | Produced by the monitor's real executor and masking over the world: `dotnet run --project src/support/MyApp.Monitor -- tool {name} --world {dir} --args {json} --incident {id}`. So the real run needs `dotnet` and `Monitor__Masking__HashKey`. The generator never formats a tool result itself |
| Trajectories | A scripted oracle per fault chooses the calls; variants add one irrelevant call, a recovery after `error`, a duplicate attempt, or a different order. Final report from the gold, with cause sentences from at least 10 phrasings per category |
| Mix | About 60 % answerable, 20 % abstain, 10 % budget pressure, 10 % expected behaviour |
| Validators (`validate`) | Every turn follows 11.3 and the grammar; every report passes 11.3 validation; no masking pattern matches any turn (unmasked data leaks reject the example); no log key, test case id, story id or runbook sentence of the adopting project's `docs/` appears in the training split; held-out domains never appear in the training split |
| Size | `training/investigator/config.json`: `seed`, `train_examples` (default 20000), `holdout_examples_per_domain` (default 250), `holdout_domains` (list), `validation_domains` (list), `validation_examples_per_domain` (default 150), `max_chars` |

---

## 12. Profiles

### 12.1 Format (`docs/support/profiles/{name}.yaml`)

| Key | Content |
|-----|---------|
| `schema_version` | `1` |
| `name`, `description` | Profile name equals the file name |
| `environment` | Environment name written to records and reports |
| `sources[]` | `id`, `kind`, kind settings (6.3, 6.4). Signal kinds: `log_analytics`, `log_files`, `http_health`, `world`. Tool-only kinds: `sqlserver` (`connection_setting`, `authentication` `entra` or `sql`); `pack` is implicit. File paths are relative to the monitor's content root |
| `tool_sources` | Tool name -> source id (`pack` for `signature_lookup`); the tools the profile offers are exactly these keys |
| `analyser` | `window_minutes`, `rate_multiplier`, `rate_min_count`, `unknown_warning_min_count`, `app_namespace_prefix` |
| `investigator` | 11.2 |
| `gates` | 10.3 |
| `notifications` | 10.5 |
| `suppression[]` | 10.4 |
| `store` | `kind` (`sqlite`), `path`, `retention_days` |
| `dashboard` | `enabled`, `urls`, `auth`, `allowed_group_id` |
| `perimeter` | `evidence_in_notifications` |

Any value may be `${setting:Key}` or `${setting:Key|default}`, resolved from the monitor's configuration
(appsettings, environment variables, user secrets). A profile never contains a secret: `pack verify` fails on a
literal value under a key named `password`, `secret`, `key`, `token` or `connection`.

### 12.2 `cloud`

Application Insights through Log Analytics (discovered by tag), SQL Server with Entra authentication, the
backend's `/health`, managed identity when deployed and the developer's sign-in locally, monitor hosted as a
container or App Service. [docs/support/profiles/cloud.yaml](../support/profiles/cloud.yaml).

### 12.3 `onprem`

JSON lines log files (3.4) tailed on the application host, SQL Server with a read-only login, `/health` on the
local network, monitor as a Windows service or systemd unit on the same host or a neighbour, SQLite store,
SMTP. [docs/support/profiles/onprem.yaml](../support/profiles/onprem.yaml). The compose `monitor` profile runs this
profile in a container beside the backend, with the log files on a shared volume (14.3, Container).

### 12.4 Hybrid

Not a separate file. Run `onprem` monitors at the sites and one `cloud` monitor centrally. Each site's
`ticket_webhook` points to the central monitor's `POST /api/incidents/ingest`, so the central dashboard shows
every site's incidents; site monitors keep their own store, tools and masking, and only masked reports cross the
perimeter.

---

## 13. Operations pack

### 13.1 Contents

| File | Source |
|------|--------|
| `manifest.json` | `schema_version` (pack format), `pack_version` (`{project version}+{short commit}`), `created_at`, `profile`, `contracts` (key -> version, from `docs/contracts.json`), `files[]` (`path`, `sha256`) |
| `signatures.json` | 5.2 |
| `runbooks/{anchor or template id}.md` | Runbook sections |
| `tools.json` | Catalog, reduced to the profile's tools |
| `entities.json`, `masking-policy.json`, `constraints.json` | `docs/support/` |
| `profile.yaml` | The chosen profile |
| `protocol.md` | The protocol block of `support.agent.md` |
| `incident-report.schema.json`, `signature.schema.json` | `docs/support/` |
| `templates/*` | `docs/support/templates/` |

No source code, no stories, no test case text beyond the Message, no personal data. The pack is a directory and
the same content as `{name}-ops-pack-{pack_version}-{profile}.zip`.

### 13.2 Builder

`dotnet run --project src/support/MyApp.Monitor -- pack build --profile {name} [--version {v}] [--out artifacts/ops-pack]`

Steps: run 5.2, copy the files, compute hashes, write the manifest, then run `pack verify` on the result. Fails on
any check of section 16 and on an expired noise date. The release pipeline builds one pack per profile it ships.

### 13.3 Loading

The monitor loads the pack from `Monitor:Pack:Path` (directory or zip), checks hashes and contract versions (it
refuses a pack whose contract version differs from the one it was built for), and watches the path: a new valid
pack replaces the old one between batches; an invalid one is refused and shown on the Sources page. Run inside
the repository without `--pack`, the monitor builds `artifacts/ops-pack/local` first.

---

## 14. The monitor application

### 14.1 Layout

`src/support/MyApp.Monitor/` is one ASP.NET Core project (`net10.0`) with no reference to the backend projects.

| Folder | Components |
|--------|------------|
| `Cli/` | Command dispatch: `run`, `pack build`, `pack verify`, `tool`, `analyse`, `return` |
| `Hosting/` | The `run` host: pack watcher, status of the Sources page |
| `Pack/` | Test case parser, call site scanner (Roslyn syntax only), runbook parser, signature generator, pack builder, loader, verifier |
| `Sources/` | `ISignalSource`, `LogRecord`, `LogAnalyticsSource`, `LogFileSource`, `HttpHealthSource`, `WorldSource`, discovery |
| `Analysis/` | Template extractor, matcher, counters, escalation, fingerprint |
| `Rules/` | Rule engine, expectation parser |
| `Tools/` | Catalog, executor, schema subset validator, `IToolImplementation` per (family, source kind) |
| `Masking/` | Policy loader, masker |
| `Store/` | `IIncidentStore`, SQLite store, migrations, retention |
| `Notifications/` | Gates, suppression, template renderer, SMTP and webhook adapters |
| `Investigator/` | Host, device and tier selection, prompt builder, protocol parser, report validator, `IInferenceBackend` |
| `Dashboard/` | Razor Pages and the JSON API |
| `Return/` | The plan of a return (section 15) |
| `deploy/` | `myapp-monitor.service` (systemd), `install-windows-service.ps1`, `Dockerfile` (the container image) |

The packages are those of `MyApp.Monitor.csproj`.

### 14.2 Configuration (monitor)

| Key | Meaning |
|-----|---------|
| `Monitor:Pack:Path` | Pack directory or zip |
| `Monitor:Profile` | Profile name inside the pack, or a path to a profile file (local use) |
| `Monitor:Environment` | Overrides the profile environment |
| `Monitor:Azure:Credential` | `developer` or `managed_identity` |
| `Monitor:Azure:ManagedIdentityClientId` | User-assigned identity |
| `Monitor:Azure:SubscriptionIds` | Discovery scope, comma-separated |
| `Monitor:Connections:{sourceId}` | Connection strings (secret) |
| `Monitor:Masking:HashKey` | HMAC key (secret, at least 32 bytes) |
| `Monitor:Investigator:ModelLock` | Path to `models/model.lock`, relative to the content root. Unset: found by walking up from the content root |
| `Monitor:Investigator:Device` | Overrides the profile device |
| `Monitor:Investigator:Endpoint` | Address of the model host. Default `http://127.0.0.1:8092` (8091 is the assistant's) |
| `Monitor:Investigator:Endpoints:{tier}` | One host per tier when both run (`small`, `large`); a tier without an entry uses `Endpoint` |
| `Monitor:Investigator:ProbeSeconds` | How often the hosts are checked again. Default 30 |
| `Monitor:Dashboard:Urls` | Overrides `dashboard.urls` |
| `Monitor:Settings:*` | Values referenced by profile placeholders |
| `Monitor:ContentRoot` | Base of the profile's relative paths (log files, store). Default: the current folder |
| `Monitor:RepositoryRoot` | Repository for `pack build`, `return` and the local pack. Default: found by walking up from the current folder |

### 14.3 Run modes

| Mode | How |
|------|-----|
| Developer machine | With `Monitor__Masking__HashKey` set, from `src/support/MyApp.Monitor`: `dotnet run -- run --profile onprem`. The default paths of that profile are relative to this folder (the backend's local log folder, `data/monitor.db`), and `dashboard.auth` is `local`; from elsewhere set `Monitor__ContentRoot`. Or `--profile cloud` with `az login` |
| Windows service | Published executable registered by `deploy/install-windows-service.ps1`; `UseWindowsService()` |
| Linux service | `deploy/myapp-monitor.service`; `UseSystemd()` |
| Container | `deploy/Dockerfile`: `run` as the entry point, store on a mounted volume (`/data`), pack at `/pack` |

Container details. The image embeds an operations pack built from the repository for the `onprem` profile, so
`--profile onprem` works without a mount; a pack mounted at `/pack` replaces it (the cloud profile, a newer pack). The
`monitor` profile of `compose.yaml` ([architecture.md](architecture.md) 8.5) starts it beside the backend: the backend
writes its JSON lines log file (3.4) to the volume `myapp-logs`, the monitor mounts it read-only and tails
`/logs/myapp-api-*.jsonl` (`Monitor__Settings__LogGlob`), reads `/health` of `http://backend:8080`, keeps its SQLite
store on the volume `myapp-monitor-data` and reaches the model host `investigator-model` through
`Monitor__Investigator__Endpoint`. A tier without a model file falls back as in 11.2. Secrets (`Monitor__Masking__HashKey`,
the connection string) come from the environment, never from the image. Dashboard: `local` authentication accepts a
loopback caller only, inside the container too, so compose runs a forwarder that shares the monitor's network namespace
and publishes it on the host's `127.0.0.1:5310`; any dashboard that other machines reach uses `entra`.

Unattended operation: no prompt ever; a missing secret or invalid pack stops the start with one Critical log
entry; a failing source, notifier or investigator never stops the others.

### 14.4 Commands

| Command | Purpose |
|---------|---------|
| `run [--profile p] [--pack path] [--repo dir]` | Start sources, analyser, store, notifiers, investigator host, dashboard |
| `pack build --profile p [--version v] [--out dir] [--repo dir]` | 13.2 |
| `pack verify --pack path` | Section 16 checks on a built pack |
| `tool {name} --args {json} [--incident id] [--world dir] [--pack path] [--profile p] [--state dir]` | One audited tool call through the executor; prints the envelope; exit 1 when the call did not succeed. With `--incident` the numbering, duplicates and budgets continue across invocations. Used by `support.agent.md`, tests and the generator |
| `analyse --input {file.jsonl} [--explain] [--dry-run] [--pack path] [--profile p]` | Runs the analyser on a file once and prints matches, template ids and escalations; without `--dry-run` it writes counters and unknown templates to the store. It opens no incident |
| `return {--report file.json \| --incident id --store monitor.db} [--out dir] [--release v] [--repo dir]` | Section 15: writes the content of the pull request for a closed incident to `--out` (default `artifacts/return/{incident id}`): `branch.txt`, `pull-request.md`, `runbook.md`, `signature-change.json`, `notes.txt`. It only reads the repository and never runs git |

All commands are prefixed `dotnet run --project src/support/MyApp.Monitor --` in the repository, or run as
`MyApp.Monitor {command}` when published. `run`, `tool` and `analyse` need `Monitor:Masking:HashKey`. Exit codes: 0 ok, 1 the command found a problem, 2 usage.

### 14.5 Tests

| Level | Location | Covers |
|-------|----------|--------|
| Unit | `tests/support/MyApp.Monitor.IntegrationTests/Unit/` | Template extraction (golden canonical strings and ids), matching, escalation table, fingerprint, masking (each pattern, protected spans, every class), schema subset validator, catalog, policy and profile validation, protocol parser, report validator, stop rules, template renderer, gates, suppression, dashboard security. No tags (development.md 11.1) |
| Monitor integration | `tests/support/MyApp.Monitor.IntegrationTests/Integration/` | File tailing with rotation, truncation and restart from offsets; SQLite store and migrations; executor over a `world` directory for every tool; `pack build` on this repository; `analyse` on a recorded log file; webhook adapter against a local listener; rule engine, incident processor, `run`, dashboard, investigator and its selection against a fake host, `return`, the sample app's bench scenarios. No database, browser, running backend or real model |
| Backend integration | `tests/integration/MyApp.IntegrationTests/` | Problem document shape and `traceId` equal to the sent `traceparent`; each keyed outcome's `code`; `/health` JSON; `POST /api/client-events` |

Run the first two with `dotnet test tests/support/MyApp.Monitor.IntegrationTests`, without approval. The third needs a database and the owner's approval.

Acceptance for the vertical slice: running the backend locally, triggering TASK-CREATE-005 and running
`analyse --input` on the log file prints `task.create.not_allowed`, matched, with the runbook anchor; with a
rate above the threshold it prints a `rate` escalation; an injected unhandled exception prints a stable
`t-` id across two runs.

---

## 15. Return loop

Every closed incident whose outcome changes knowledge goes back as a pull request, never a direct commit. The
Review page lists them; `fix.agent.md` performs the change for a coding agent. The command `return` (14.4) prepares the files of that pull
request from a report or from the store; a person or the agent applies them, and the branch, commit, push and pull request need the owner's approval.

| Outcome | Repository change |
|---------|-------------------|
| `fixed` | The test case that catches the failure (new or corrected) passes; runbook `Status` `Fixed`, `Fixed in` the release; History line |
| `noise` | Runbook `Status` `Noise` with rationale and expiry (5.6); for unkeyed, a runbook in `unkeyed-runbooks.md`; reviewer other than the author |
| `reclassified` | Test case Severity and the definition's level changed together; runbook text updated; History line |
| `unknown_resolved` | A new `Error handling` or `Edge case` test case with its Observable trace, a log definition (3.1) at the point of detection, a runbook; any template runbook for the same pattern removed |
| `duplicate` | None (the incident links the original) |
| `wontfix` | History line; a recurring pattern is proposed as noise instead |

A fired `fixed` signature opens a `regression` incident; nothing in the loop makes the system stop seeing a
pattern except an explicit, reviewed, expiring noise decision.

---

## 16. Checks

`pack build` and `pack verify` run these checks and fail on a finding; `tests/support/MyApp.Monitor.IntegrationTests/Integration/PackTests.cs` proves them.
They are not part of the architecture tests of development.md 12, and they have no baseline.

| Id | Check | Where |
|----|-------|-------|
| O01 | Every `LoggerMessage` under `src/backend/` is in `MyApp.Application/Logging/*Log.cs`, has `EventName` from a `Keys` constant, and its name is a log key of a test case or an event of 3.5; method name is the key in PascalCase | Pack build (method name); unit test `LogDefinitionTests` (key constants) |
| O02 | No `LogWarning`, `LogError`, `LogCritical`, or `Log(LogLevel.Warning/Error/Critical, ...)` call under `src/backend/` outside `MyApp.Application/Logging/` | Pack build (Roslyn scan of `src/backend/`, as for O01) |
| O03 | No template placeholder named `Email`, `FirstName`, `LastName`, `FullName`, `Name`, `Phone`, `PhoneNumber`, `Address`, `Title`, `Description`, `Password`, `Token` anywhere under `src/backend/`: the `Message` of every `LoggerMessage` definition and the string literals passed to any `Log*` call or `BeginScope`; compared without regard to case | Pack build (same scan) |
| O04 | A keyed definition's level equals its test case Severity | Pack build |
| O05 | The story comment above a keyed definition contains its test case's User Story | Pack build |
| O06 | Area `operations.md` follows 5.4: headings, field table, vocabularies, one H2 per log key of the area and no other, Noise fields iff `Noise` | Pack build and verify |
| O07 | `unkeyed-runbooks.md` follows 5.5 and each H2 equals the computed template id | Pack build and verify |
| O08 | Files under `docs/support/` validate: JSON against their formats, profiles against 12.1; every profile tool exists in the catalog and has an implementation for its source's kind; no production profile lists a `world` source; every entity has a masking section and every masking entity exists; every tool output field has a class; no secret literal in a profile | Pack build and verify |
| O09 | The protocol markers exist exactly once in `support.agent.md` | Pack build and verify |
| O10 | No noise expiry before the build date; templates render with the example report without unknown paths | Pack build and verify |

---

## 17. Related documents

| Document | Purpose |
|----------|---------|
| [development.md](development.md) | Test cases, Observable trace, log key grammar, rules R13 to R15 |
| [models.md](models.md) | Training pipeline, exports, runtime ladder, `models/model.lock` |
| [docs/support/](../support/) | Normative files: schemas, catalog, entities, masking, constraints, profiles, templates |
| [support.agent.md](../../.github/agents/support.agent.md) | Investigator contract and protocol block |
| [fix.agent.md](../../.github/agents/fix.agent.md) | Consuming an incident report in this repository |
