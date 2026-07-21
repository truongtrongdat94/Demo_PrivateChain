# Contract

Project nay chua Solidity source va Hardhat config.

Compile + copy artifact sang backend + deploy contract:

```powershell
.\scripts\deploy.ps1
```

Chi compile va copy artifact, khong deploy:

```powershell
.\scripts\deploy.ps1 -SkipDotnetDeploy
```

Artifact backend dung nam tai:

```text
../backend/contract-artifacts/HashRegistry.json
```
