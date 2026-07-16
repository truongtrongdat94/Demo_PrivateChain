import { defineConfig } from "hardhat/config";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const directory = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  // Besu genesis only enables London, so do not generate Shanghai PUSH0 (0x5f).
  solidity: {
    version: "0.8.28",
    settings: {
      evmVersion: "london",
    },
  },
  paths: {
    root: directory,
    sources: join(directory, "contracts"),
    artifacts: join(directory, "artifacts"),
    cache: join(directory, "cache"),
  },
});
