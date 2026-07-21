# Chain

Project nay chi chua Hyperledger Besu private network.

Thanh phan:

```text
besu-a
besu-b
config/besu/genesis.json
config/besu/static-nodes.json
config/besu/server-a-key
config/besu/server-b-key
```

Chay:

```powershell
docker compose up -d
docker compose ps
```

RPC endpoint dung cho backend:

```text
http://127.0.0.1:28546
```

Reset ledger:

```powershell
docker compose down -v
docker compose up -d
```
