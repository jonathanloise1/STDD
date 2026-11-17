-- Store schema version 1 (operations.md 10.2). Timestamps are ISO 8601 UTC text.

CREATE TABLE incidents (
    id                   TEXT PRIMARY KEY,
    fingerprint          TEXT NOT NULL,
    status               TEXT NOT NULL,
    severity             TEXT NOT NULL,
    trigger              TEXT NOT NULL,
    signature_id         TEXT NULL,
    template_id          TEXT NULL,
    environment          TEXT NOT NULL,
    first_seen           TEXT NOT NULL,
    last_seen            TEXT NOT NULL,
    count                INTEGER NOT NULL,
    verdict_source       TEXT NOT NULL,
    report_json          TEXT NOT NULL,
    previous_incident_id TEXT NULL,
    created_at           TEXT NOT NULL,
    updated_at           TEXT NOT NULL,
    closed_at            TEXT NULL,
    outcome              TEXT NULL
);
CREATE INDEX ix_incidents_fingerprint_status ON incidents (fingerprint, status);
CREATE INDEX ix_incidents_status_severity ON incidents (status, severity);

CREATE TABLE incident_events (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    incident_id   TEXT NOT NULL,
    at            TEXT NOT NULL,
    kind          TEXT NOT NULL,
    data_json     TEXT NOT NULL
);
CREATE INDEX ix_incident_events_incident ON incident_events (incident_id, id);

CREATE TABLE investigations (
    incident_id   TEXT PRIMARY KEY,
    status        TEXT NOT NULL,
    attempts      INTEGER NOT NULL DEFAULT 0,
    queued_at     TEXT NULL,
    started_at    TEXT NULL,
    finished_at   TEXT NULL,
    error         TEXT NULL
);

CREATE TABLE counters (
    signature_or_template TEXT NOT NULL,
    environment           TEXT NOT NULL,
    fingerprint           TEXT NOT NULL,
    bucket_start          TEXT NOT NULL,
    count                 INTEGER NOT NULL,
    PRIMARY KEY (signature_or_template, environment, fingerprint, bucket_start)
);
CREATE INDEX ix_counters_bucket ON counters (bucket_start);

CREATE TABLE unknown_templates (
    template_id     TEXT PRIMARY KEY,
    event_name      TEXT NULL,
    category        TEXT NOT NULL,
    exception_type  TEXT NULL,
    top_frame       TEXT NULL,
    template        TEXT NOT NULL,
    level           TEXT NOT NULL,
    first_seen      TEXT NOT NULL,
    last_seen       TEXT NOT NULL,
    count           INTEGER NOT NULL,
    sample_trace_id TEXT NULL
);

CREATE TABLE tool_audit (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    at             TEXT NOT NULL,
    incident_id    TEXT NULL,
    caller         TEXT NOT NULL,
    tool           TEXT NOT NULL,
    arguments_json TEXT NOT NULL,
    source_id      TEXT NULL,
    status         TEXT NOT NULL,
    rows           INTEGER NOT NULL,
    output_chars   INTEGER NOT NULL,
    duration_ms    INTEGER NOT NULL,
    error          TEXT NULL
);
CREATE INDEX ix_tool_audit_at ON tool_audit (at);

-- event is the incident event the notification answers (created, severity_raised, digest); the idempotency key is incident id + event.
CREATE TABLE notifications (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    incident_id  TEXT NOT NULL,
    channel      TEXT NOT NULL,
    kind         TEXT NOT NULL,
    at           TEXT NOT NULL,
    target       TEXT NULL,
    reference    TEXT NULL,
    error        TEXT NULL,
    body         TEXT NULL,
    event        TEXT NOT NULL DEFAULT 'created'
);
CREATE INDEX ix_notifications_incident ON notifications (incident_id, channel, event);
CREATE INDEX ix_notifications_kind ON notifications (kind, at);

CREATE TABLE suppression_windows (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT NOT NULL,
    starts_at   TEXT NOT NULL,
    ends_at     TEXT NOT NULL,
    match       TEXT NOT NULL,
    created_by  TEXT NOT NULL,
    reason      TEXT NULL
);

CREATE TABLE source_offsets (
    source_id   TEXT NOT NULL,
    head_hash   TEXT NOT NULL,
    path        TEXT NOT NULL,
    offset      INTEGER NOT NULL,
    updated_at  TEXT NOT NULL,
    PRIMARY KEY (source_id, head_hash)
);

CREATE TABLE source_watermarks (
    source_id  TEXT PRIMARY KEY,
    watermark  TEXT NOT NULL
);

CREATE TABLE seen_records (
    source_id  TEXT NOT NULL,
    record_id  TEXT NOT NULL,
    seen_at    TEXT NOT NULL,
    PRIMARY KEY (source_id, record_id)
);
CREATE INDEX ix_seen_records_seen_at ON seen_records (seen_at);

CREATE TABLE discovered_resources (
    source_id     TEXT PRIMARY KEY,
    resource_id   TEXT NOT NULL,
    refreshed_at  TEXT NOT NULL
);
