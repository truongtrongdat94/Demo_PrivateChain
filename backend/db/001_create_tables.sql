CREATE TABLE sensor_records (
  id BIGSERIAL PRIMARY KEY,
  station_id TEXT NOT NULL,
  observed_at TIMESTAMPTZ NOT NULL,
  original_json TEXT NOT NULL,
  payload_hash CHAR(64) NOT NULL CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'anchored', 'verified'))
);

CREATE INDEX sensor_records_pending_by_station
  ON sensor_records (station_id, observed_at)
  WHERE status = 'pending';
