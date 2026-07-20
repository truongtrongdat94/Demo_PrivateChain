CREATE TABLE records (
  contract_address CHAR(42) NOT NULL CHECK (contract_address ~ '^0x[0-9a-f]{40}$'),
  sequence BIGINT NOT NULL CHECK (sequence > 0),
  original_json TEXT NOT NULL,
  PRIMARY KEY (contract_address, sequence)
);

CREATE TABLE runtime_settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
