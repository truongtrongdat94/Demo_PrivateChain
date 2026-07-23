
| Mốc | Nội dung |
| --- | --- |
| **M1** | Node-RED mô phỏng 2 trạm (`station-01`, `station-02`), mỗi 30 giây gửi JSON gồm pH, COD, BOD, TSS, lưu lượng, nhiệt độ qua MQTT RabbitMQ. |
| **M2** | Backend nhận MQTT, validate JSON và lưu `original_json` vào PostgreSQL. |
| **M3** | Mỗi 60 giây gom record pending thành một batch, dựng Merkle root từ `record id + hash(JSON)`, rồi neo root lên Besu. |
| **M4** | API `GET /api/batches/verify-all` lấy event trên chain, dựng lại root từ DB để phát hiện dữ liệu bị thiếu hoặc sửa. |
| **M5** | Dashboard bảng, biểu đồ 6 chỉ tiêu, lọc/phân trang và nút **Verify all**. |
| **M6** | Script sửa có chủ đích một row trong PostgreSQL để demo kết quả `TAMPERED`. |

##

```text
simulator/  Node-RED tạo dữ liệu M1
broker/     RabbitMQ + MQTT plugin

backend/    M2, M3, M4 và API
  Workers/      MqttIngestWorker nhận MQTT; BatcherWorker chạy mỗi phút
  Services/     Logic ingest, hash, Merkle tree, anchor và verify
  Repositories/ Đọc/ghi PostgreSQL
  Domain/       Hai entity EF Core: SensorRecord, AnchorBatch
  Controllers/  API dashboard và verify
  db/           Schema PostgreSQL

contract/   HashRegistry.sol và script compile/deploy
chain/      Docker Compose và cấu hình 2 node Besu
frontend/   Dashboard HTML/CSS/JavaScript
script_m6/  Script giả mạo dữ liệu DB
```

## Demo

```powershell
# 1. Chạy blockchain và deploy contract
docker compose -f chain\docker-compose.yml up -d
.\contract\scripts\deploy.ps1

# 2. Chạy luồng dữ liệu
docker compose -f broker\docker-compose.yml up -d
docker compose -f backend\docker-compose.yml up --build -d
docker compose -f simulator\docker-compose.yml up -d
docker compose -f frontend\docker-compose.yml up --build -d
```

1. Mở dashboard: `http://127.0.0.1:25100`.
2. Chờ tối đa 1 phút để batcher neo record `pending` lên Besu; record chuyển thành `anchored`.
3. Nhấn **Verify all**: các batch hợp lệ hiển thị `VALID`.
4. Chọn một record `anchored`, ví dụ id `25`, rồi sửa pH:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\script_m6\tamper-record.ps1 -RecordId 25 -Field ph
```

5. Nhấn **Verify all** lần nữa: các record của batch đó hiển thị `TAMPERED`.
