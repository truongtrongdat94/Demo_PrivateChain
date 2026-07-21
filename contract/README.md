# Contract

Project nay chua Solidity source, Hardhat compiler va script deploy len Besu.

Compile va deploy contract:

```powershell
.\scripts\deploy.ps1
```

Chi compile:

```powershell
npm.cmd run compile
```

Deploy khong goi code .NET va khong copy ABI/bytecode sang backend. Ket qua deploy
duoc ghi trong:

```text
deployments/HashRegistry.json   dia chi, transaction hash, block number
deployments/backend.env         contract address + chain ID cho backend Compose
```

Mac dinh script dung RPC `http://127.0.0.1:28546`, chain ID `1337` va tai khoan
demo da duoc cap tien trong genesis. Co the doi RPC va chain ID:

```powershell
.\scripts\deploy.ps1 -RpcUrl http://127.0.0.1:28546 -ChainId 1337
```
