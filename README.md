# Demo neo dữ liệu cảm biến lên Besu

Demo mô phỏng dữ liệu chất lượng nước, lưu JSON trong PostgreSQL và neo Merkle root của từng batch lên Hyperledger Besu. Khi verify, backend dựng lại root từ DB rồi so với blockchain.

## Đã làm

| Mốc | Nội dung |
| --- | --- |
| **M1** | Node-RED mô phỏng 2 trạm (`station-01`, `station-02`), mỗi 30 giây gửi JSON gồm pH, COD, BOD, TSS, lưu lượng, nhiệt độ qua MQTT RabbitMQ. |
| **M2** | Backend nhận MQTT, validate JSON và lưu `original_json` vào PostgreSQL. |
| **M3** | Mỗi 60 giây gom record pending thành một batch, dựng Merkle root từ `record id + hash(JSON)`, rồi neo root lên Besu. |
| **M4** | API `GET /api/batches/verify-all` lấy event trên chain, dựng lại root từ DB để phát hiện dữ liệu bị thiếu hoặc sửa. |
| **M5** | Dashboard bảng, biểu đồ 6 chỉ tiêu, lọc/phân trang và nút **Verify all**. |
| **M6** | Script sửa có chủ đích một row trong PostgreSQL để demo kết quả `TAMPERED`. |

## Cách đọc source nhanh

```text
simulator/  Node-RED tạo dữ liệu M1
broker/     RabbitMQ + MQTT plugin

backend/    M2, M3, M4 và API
  Workers/  MqttIngestWorker nhận MQTT; BatcherWorker chạy mỗi phút
  Services/ Logic ingest, hash, Merkle tree, anchor và verify
  Repositories/ Đọc/ghi PostgreSQL
  Domain/   Hai entity EF Core: SensorRecord, AnchorBatch
  Controllers/ API dashboard và verify
  db/       Schema PostgreSQL

contract/   HashRegistry.sol và script compile/deploy
chain/      Docker Compose và cấu hình 2 node Besu
frontend/   Dashboard HTML/CSS/JavaScript
blockscout/ UI để quan sát trực tiếp block, transaction, log trên Besu
script_m6/  Script giả mạo dữ liệu DB
```

## Điểm chính cần biết

- PostgreSQL lưu JSON gốc và batch mà record thuộc về.
- Contract chỉ lưu `batchId -> merkleRoot, submitter, anchoredAt`; không lưu JSON cảm biến.
- `contract/deployments/` được tạo sau khi deploy, chứa địa chỉ contract để backend kết nối.
