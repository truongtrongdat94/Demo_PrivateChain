import { readFile, mkdir, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ContractFactory, JsonRpcProvider, Wallet } from "ethers";

const contractRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const rpcUrl = process.env.BESU_RPC_URL ?? "http://127.0.0.1:28546";
const chainId = Number(process.env.BESU_CHAIN_ID ?? "1337");
const privateKey = process.env.DEPLOYER_PRIVATE_KEY
  ?? "0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63";

const artifactPath = join(
  contractRoot,
  "artifacts",
  "contracts",
  "HashRegistry.sol",
  "HashRegistry.json");
const artifact = JSON.parse(await readFile(artifactPath, "utf8"));

if (!artifact.bytecode || artifact.bytecode === "0x") {
  throw new Error(`HashRegistry bytecode is missing from ${artifactPath}`);
}

const provider = new JsonRpcProvider(rpcUrl, chainId);
const wallet = new Wallet(privateKey, provider);
const factory = new ContractFactory(artifact.abi, artifact.bytecode, wallet);
const contract = await factory.deploy();
const deploymentTransaction = contract.deploymentTransaction();

if (!deploymentTransaction) {
  throw new Error("Ethers did not return the deployment transaction.");
}

const receipt = await deploymentTransaction.wait();
if (!receipt || receipt.status !== 1) {
  throw new Error("Besu rejected the contract deployment transaction.");
}

const deployment = {
  contractAddress: (await contract.getAddress()).toLowerCase(),
  transactionHash: deploymentTransaction.hash.toLowerCase(),
  blockNumber: receipt.blockNumber,
  chainId,
};
const deploymentDirectory = join(contractRoot, "deployments");

await mkdir(deploymentDirectory, { recursive: true });
await writeFile(
  join(deploymentDirectory, "HashRegistry.json"),
  `${JSON.stringify(deployment, null, 2)}\n`);
await writeFile(
  join(deploymentDirectory, "backend.env"),
  [
    `Blockchain__ContractAddress=${deployment.contractAddress}`,
    `Blockchain__ChainId=${chainId}`,
    "",
  ].join("\n"));

console.log(`Contract address: ${deployment.contractAddress}`);
console.log(`Transaction hash: ${deployment.transactionHash}`);
console.log(`Block number: ${deployment.blockNumber}`);
console.log(`Backend environment: ${join(deploymentDirectory, "backend.env")}`);
