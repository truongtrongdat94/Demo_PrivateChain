## Kiến trúc

```text
Client / PowerShell
        |
        v
ASP.NET Core API
        |
        |-- PostgreSQL: lưu JSON gốc, JSON chuẩn hóa, payloadHash
        |
        |-- Nethereum: gọi smart contract trên Besu
                         |
                         v
              Hyperledger Besu QBFT private chain
```

Các thành phần chính:

- `POST /api/blockchain/anchor`: nhận JSON, tính hash, ghi blockchain, lưu database.
- `GET /api/blockchain/records`: đọc dữ liệu đang lưu trong PostgreSQL.
- `GET /api/blockchain/history`: đọc event từ blockchain và đối chiếu với database.
- Smart contract `HashRegistry`: bắt buộc `sequence = lastSequence + 1` và `previousHash = lastPayloadHash`.

Luồng ghi dữ liệu:

```text
JSON payload
-> canonical JSON
-> SHA-256 payloadHash
-> lấy lastSequence + lastPayloadHash từ contract
-> gọi anchorHash(sequence, payloadHash, previousHash)
-> lưu record vào PostgreSQL
```

Luồng kiểm tra:

```text
Đọc event HashAnchored từ Besu
-> tìm record PostgreSQL theo sequence
-> hash lại JSON trong DB
-> so payloadHash, previousHash, sequence
-> kiểm tra record có nối chuỗi đúng không
```

## Cách chạy demo

```powershell
docker compose up --build
```

Lệnh trên làm các việc sau:

- build smart contract bằng Hardhat;
- build ASP.NET Core API;
- khởi động PostgreSQL, pgAdmin và 2 node Besu;
- tự deploy smart contract nếu database chưa có contract address;
- chạy API tại `http://localhost:5000`.

Nếu muốn xóa sạch dữ liệu cũ rồi chạy lại từ đầu:

```powershell
docker compose down -v
docker compose up --build
```

## Cách test demo

Gửi record thứ nhất:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/blockchain/anchor" `
  -ContentType "application/json" `
  -Body '{"name":"demo-1","amount":100,"note":"first record"}' |
  ConvertTo-Json -Depth 10
```

Gửi record thứ hai:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/blockchain/anchor" `
  -ContentType "application/json" `
  -Body '{"name":"demo-2","amount":200,"note":"second record"}' |
  ConvertTo-Json -Depth 10
```

Kết quả mong đợi:

- Record đầu tiên có `sequence = 1`.
- Record đầu tiên có `previousHash = 000000...000`.
- Record thứ hai có `sequence = 2`.
- Record thứ hai có `previousHash` bằng `payloadHash` của record thứ nhất.

Đọc dữ liệu trong database:

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/blockchain/records" |
  ConvertTo-Json -Depth 10
```

Đối chiếu database với blockchain:

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/blockchain/history" |
  ConvertTo-Json -Depth 10
```

Các cờ kiểm tra quan trọng:

- `isSequenceMatch`: sequence trong DB và blockchain có khớp không.
- `isPayloadHashMatch`: payloadHash trong DB và blockchain có khớp không.
- `isPreviousHashMatch`: previousHash trong DB và blockchain có khớp không.
- `isChainLinkValid`: record có nối đúng record trước không.
- `isDatabaseContentHashValid`: JSON trong DB hash lại có còn khớp không.
- `isMatch`: tất cả điều kiện trên đều đúng.

## Đọc raw blockchain

Lấy block cụ thể, ví dụ block `5`:

```powershell
$body = @{
    jsonrpc = "2.0"
    method  = "eth_getBlockByNumber"
    params  = @("0x5", $true)
    id      = 1
} | ConvertTo-Json -Depth 10

Invoke-RestMethod `
    -Uri "http://127.0.0.1:28546" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

Lấy event log của contract:

```powershell
$contractAddress = "<contract address sau khi deploy>"
$eventTopic = "0x3ef012c643ed8475171f2e03d2845940066898109e64f3da1622355e0c4f3fd9"

$body = @{
    jsonrpc = "2.0"
    method  = "eth_getLogs"
    params  = @(
        @{
            address   = $contractAddress
            fromBlock = "0x0"
            toBlock   = "latest"
            topics    = @($eventTopic)
        }
    )
    id = 1
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod `
    -Uri "http://127.0.0.1:28546" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body

$response.result
```

Trong event log:

- `topics[0]`: mã nhận diện event `HashAnchored`.
- `topics[1]`: `sequence`.
- `topics[2]`: `payloadHash`.
- `topics[3]`: `submitter`.
- `data`: chứa `previousHash` và `anchoredAt`.

## Thông tin kết nối

- API: `http://localhost:5000`
- Besu RPC: `http://127.0.0.1:28546`
- PostgreSQL: `127.0.0.1:25432`
- pgAdmin: `http://localhost:25050`
- pgAdmin login: `admin@example.com` / `admin`
- PostgreSQL database/user/password: `hash_demo` / `hash_demo` / `hash_demo`
