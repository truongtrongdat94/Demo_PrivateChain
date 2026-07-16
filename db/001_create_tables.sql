CREATE TABLE records (
  id UUID NOT NULL UNIQUE,
  sequence BIGINT PRIMARY KEY CHECK (sequence > 0),
  original_json TEXT NOT NULL,
  canonical_json TEXT NOT NULL,
  payload_hash CHAR(64) NOT NULL UNIQUE CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
  previous_hash CHAR(64) NOT NULL CHECK (previous_hash ~ '^[0-9a-f]{64}$'),
  created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE runtime_settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
