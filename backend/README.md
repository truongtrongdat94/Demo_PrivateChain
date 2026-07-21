# Backend (M2)

Backend la worker, khong mo HTTP API va khong gui transaction blockchain o M2.

Flow:

```text
RabbitMQ MQTT -> validate schema -> canonical JSON -> SHA-256 -> PostgreSQL (pending)
```

Du lieu MQTT hop le phai co `schemaVersion` la `1.0`, `stationId`, `stationName`,
`observedAt`, va 6 chi tieu: `ph`, `cod`, `bod`, `tss`, `flow`, `temperature`.

Chay broker truoc de tao network `broker-net`, sau do chay backend:

```powershell
cd ..\broker
docker compose up -d

cd ..\backend
docker compose up --build
```

Backend subscribe topic:

```text
SmartEMS/WaterQuality/+/Reading
```

Backend luu bang `sensor_records`:

- `id`: dinh danh record trong DB, dung cho M3/M4.
- `station_id`, `observed_at`: gom batch theo tram va thoi gian.
- `original_json`: du lieu goc de hash lai khi verify.
- `payload_hash`: SHA-256 cua canonical JSON.
- `status`: `pending` cho den khi M3 anchor batch.

Xem DB bang pgAdmin: `http://localhost:24850` (tai khoan `admin@example.com` / `admin`).
Do script khoi tao PostgreSQL chi chay khi volume moi, neu da chay backend cu thi reset DB truoc:

```powershell
docker compose down -v
docker compose up --build
```
