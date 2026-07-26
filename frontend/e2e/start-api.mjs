import { spawn } from "node:child_process";
import { rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const frontendDirectory = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(frontendDirectory, "..");
const databasePath = join(
  tmpdir(),
  `fulfillment-console-e2e-${process.pid}.db`
);

const child = spawn(
  "dotnet",
  [
    "run",
    "--project",
    "src/Fulfillment.Api/Fulfillment.Api.csproj",
    "--configuration",
    "Release",
    "--no-launch-profile"
  ],
  {
    cwd: repositoryRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5080",
      Authentication__Issuer: "fulfillment-local",
      Authentication__Audience: "fulfillment-api",
      Authentication__SigningKey:
        "local-e2e-signing-key-not-for-production-2026",
      Database__Provider: "Sqlite",
      Database__MigrateOnStartup: "true",
      ConnectionStrings__Fulfillment: `Data Source=${databasePath}`
    },
    stdio: "inherit"
  }
);

function stop(signal) {
  if (!child.killed) {
    child.kill(signal);
  }
}

process.on("SIGINT", () => stop("SIGINT"));
process.on("SIGTERM", () => stop("SIGTERM"));

child.on("exit", (code, signal) => {
  rmSync(databasePath, { force: true });
  rmSync(`${databasePath}-shm`, { force: true });
  rmSync(`${databasePath}-wal`, { force: true });

  if (signal) {
    process.kill(process.pid, signal);
  } else {
    process.exit(code ?? 1);
  }
});
