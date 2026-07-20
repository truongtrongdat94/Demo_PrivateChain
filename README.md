## Kiến trúc

```text
Client / PowerShell
        |
        v
ASP.NET Core API
        |
        |-- PostgreSQL: lưu JSON gốc theo contractAddress + sequence
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
- Smart contract `HashRegistry`: tự cấp `sequence` cho từng payload hash.

Luồng ghi dữ liệu:

```text
JSON payload
-> canonical JSON
-> SHA-256(canonical JSON) = payloadHash
-> gọi anchorHash(payloadHash)
-> contract tự tạo sequence
-> lưu { contractAddress, sequence, originalJson } vào PostgreSQL
```

Luồng kiểm tra:

```text
Đọc event HashAnchored từ Besu
-> đối chiếu đủ hai tập DB và blockchain theo contractAddress + sequence
-> hash lại canonical JSON trong DB và so payloadHash
-> so payloadHash tính từ JSON DB với event tương ứng
-> phát hiện row DB thiếu, thừa hoặc trùng sequence
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

Phiên bản này đổi ABI contract và schema `records`, nên dữ liệu demo của phiên bản cũ phải chạy lại bằng hai lệnh trên.

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
- Record thứ hai có `sequence = 2`.

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

Kết quả kiểm tra có hai mức:

- `isMatch`: record blockchain này có khớp row DB tương ứng không.
- `isValid`: toàn bộ DB có khớp đầy đủ blockchain không.

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
$eventTopic = "0x2e4b02ef0e4ff0261d5ef64761d2819645ae0215b660e516d71af67634442edd"

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
- `data`: chứa `anchoredAt`.

## Thông tin kết nối

- API: `http://localhost:5000`
- Besu RPC: `http://127.0.0.1:28546`
- PostgreSQL: `127.0.0.1:24832`
- pgAdmin: `http://localhost:24850`
- pgAdmin login: `admin@example.com` / `admin`
- PostgreSQL database/user/password: `hash_demo` / `hash_demo` / `hash_demo`
