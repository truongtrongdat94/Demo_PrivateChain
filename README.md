# Besu Hash Demo

Repository da tach thanh cac project rieng:

```text
chain/      Hyperledger Besu private network
contract/   Solidity source, Hardhat compile, copy artifact
backend/    ASP.NET Core API, contract client, PostgreSQL
broker/     RabbitMQ + MQTT plugin
simulator/  M1 Node-RED sensor simulator
```

## Chay Thu Tu

Chay Besu network:

```powershell
cd chain
docker compose up -d
```

Chay backend:

```powershell
cd ..\contract
.\scripts\deploy.ps1 -SkipDotnetDeploy

cd ..\backend
docker compose up --build
```

Chay RabbitMQ broker:

```powershell
cd ..\broker
docker compose up -d
```

Chay Node-RED simulator:

```powershell
cd ..\simulator
docker compose up -d
```

## Ket Noi Chinh

```text
Besu RPC:             http://127.0.0.1:28546
Backend API:          http://localhost:5000
PostgreSQL:           127.0.0.1:24832
pgAdmin:              http://localhost:24850
RabbitMQ MQTT:        127.0.0.1:1883
RabbitMQ Management:  http://localhost:15672
Node-RED UI:          http://localhost:1880
```

RabbitMQ login:

```text
ems / ems
```

pgAdmin login:

```text
admin@example.com / admin
```

## Reset Rieng Tung Project

```powershell
cd chain
docker compose down -v

cd ..\backend
docker compose down -v

cd ..\broker
docker compose down -v

cd ..\simulator
docker compose down -v
```

`backend` khi chay bang Docker se goi Besu qua `http://host.docker.internal:28546`. Neu chay backend bang `dotnet run` tren host thi dung RPC `http://127.0.0.1:28546`.
