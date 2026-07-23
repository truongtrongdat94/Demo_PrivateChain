CREATE TABLE anchor_batches (
  id BIGSERIAL PRIMARY KEY,
  record_count INTEGER NOT NULL CHECK (record_count > 0),
  transaction_hash CHAR(66) CHECK (transaction_hash ~ '^0x[0-9a-f]{64}$'),
  status TEXT NOT NULL DEFAULT 'prepared' CHECK (status IN ('prepared', 'anchored')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
  anchored_at TIMESTAMPTZ,
  CHECK (
    (status = 'prepared' AND transaction_hash IS NULL AND anchored_at IS NULL)
    OR
    (status = 'anchored' AND transaction_hash IS NOT NULL AND anchored_at IS NOT NULL)
  )
);

CREATE TABLE sensor_records (
  id BIGSERIAL PRIMARY KEY,
  original_json TEXT NOT NULL,
  batch_id BIGINT REFERENCES anchor_batches(id),
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'anchored')),
  CHECK (
    status = 'pending'
    OR (status = 'anchored' AND batch_id IS NOT NULL)
  )
);

CREATE INDEX sensor_records_pending_for_batch
  ON sensor_records (id)
  WHERE status = 'pending' AND batch_id IS NULL;

CREATE INDEX sensor_records_by_batch
  ON sensor_records (batch_id, id)
  WHERE batch_id IS NOT NULL;
