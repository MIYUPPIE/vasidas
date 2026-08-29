-- Design doc section 37: initial SQLite schema.

CREATE TABLE workflows (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    version INTEGER NOT NULL,
    platform TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    -- Not in section 37's table list, but Workflow.StopOnFailure (section 16) is a real per-workflow
    -- setting that has to round-trip through storage somewhere.
    stop_on_failure INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE workflow_actions (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL REFERENCES workflows (id) ON DELETE CASCADE,
    sequence INTEGER NOT NULL,
    action_type TEXT NOT NULL,
    configuration_json TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX idx_workflow_actions_workflow_id ON workflow_actions (workflow_id, sequence);

CREATE TABLE executions (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL REFERENCES workflows (id) ON DELETE CASCADE,
    started_at TEXT NOT NULL,
    completed_at TEXT,
    status TEXT NOT NULL,
    error TEXT
);

CREATE INDEX idx_executions_workflow_id ON executions (workflow_id, started_at);

CREATE TABLE execution_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    execution_id TEXT NOT NULL REFERENCES executions (id) ON DELETE CASCADE,
    timestamp TEXT NOT NULL,
    level TEXT NOT NULL,
    message TEXT NOT NULL,
    action_id TEXT,
    screenshot_path TEXT
);

CREATE INDEX idx_execution_logs_execution_id ON execution_logs (execution_id, timestamp);

CREATE TABLE templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    path TEXT NOT NULL,
    type TEXT NOT NULL,
    application TEXT,
    threshold REAL,
    created_at TEXT NOT NULL
);
