#!/usr/bin/env node
/**
 * E2E stack supervisor — guarantees the full stack before tests/recordings.
 *
 * 1. docker compose up -d --wait   (databases must be up + healthy; idempotent)
 * 2. if API /health and web already serving -> nothing to start
 * 3. else spawn `pnpm run dev:fast` and wait for both to become healthy
 * 4. caller decides teardown via the returned stop(); SIGTERM the dev:fast
 *    tree — scripts/dev-fast.mjs kills its own API/web children.
 *
 * The regular `pnpm dev` stack intentionally includes generated-client polling
 * and build watchers. E2E runs do not edit source while executing, so retaining
 * those watchers wastes memory and can starve the browser or the developer IDE.
 *
 * Used by scripts/e2e-global-setup.mjs (playwright) and scripts/e2e-record.mjs.
 */
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const WEB_URL = (
  process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://localhost:3000"
).replace(/\/$/, "");
const API_HEALTH_URL =
  process.env.E2E_API_HEALTH_URL ?? "http://localhost:8080/health";
const BOOT_TIMEOUT_MS = 600_000;
const HEALTH_TIMEOUT_MS = 5_000;
const E2E_WEB_HEAP_MB = process.env.E2E_WEB_MAX_OLD_SPACE_SIZE_MB ?? "4096";
const API_PROJECT = "apps/api/Source/GameGuild.API/GameGuild.API.csproj";

async function healthy(url, okStatus) {
  try {
    const res = await fetch(url, {
      method: "GET",
      redirect: "manual",
      signal: AbortSignal.timeout(HEALTH_TIMEOUT_MS),
    });
    return okStatus(res.status);
  } catch {
    return false;
  }
}

const webUp = () => healthy(WEB_URL, (s) => s < 500);
const apiUp = () => healthy(API_HEALTH_URL, (s) => s >= 200 && s < 300);
export const stackUp = async () => (await webUp()) && (await apiUp());

async function waitUntil(fn, what, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (!(await fn())) {
    if (Date.now() > deadline) {
      throw new Error(
        `[e2e-stack] timed out waiting for ${what} (${timeoutMs / 1000}s)`,
      );
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
}

export async function startStackSupervisor() {
  const compose = spawnSync(
    "docker",
    ["compose", "-f", "compose.yaml", "up", "-d", "--wait"],
    {
      cwd: root,
      stdio: "inherit",
    },
  );
  if (compose.status !== 0) {
    throw new Error(
      `[e2e-stack] databases (docker compose) failed (exit ${compose.status})`,
    );
  }

  let dev = null;
  if (!(await stackUp())) {
    console.log("[e2e-stack] building API once with one MSBuild worker...");
    const apiBuild = spawnSync(
      "dotnet",
      ["build", API_PROJECT, "--no-restore", "-m:1", "-nr:false"],
      {
        cwd: root,
        stdio: "inherit",
        env: {
          ...process.env,
          DOTNET_CLI_TELEMETRY_OPTOUT: "1",
          MSBUILDDISABLENODEREUSE: "1",
        },
      },
    );
    if (apiBuild.status !== 0) {
      throw new Error(
        `[e2e-stack] API build failed (exit ${apiBuild.status ?? "unknown"})`,
      );
    }

    console.log("[e2e-stack] starting lean dev stack (pnpm dev:fast)...");
    dev = spawn("pnpm", ["run", "dev:fast"], {
      cwd: root,
      stdio: "inherit",
      detached: process.platform !== "win32",
      env: {
        ...process.env,
        DEV_FAST_SKIP_COMPOSE: "1",
        DEV_FAST_API_WATCH: "0",
        DEV_FAST_API_NO_BUILD: "1",
        DEV_FAST_WEB_BUNDLER: "turbopack",
        DEV_FAST_WEB_MAX_OLD_SPACE_SIZE_MB: E2E_WEB_HEAP_MB,
        GRADING_VALIDATION_E2E: "1",
        NEXT_TELEMETRY_DISABLED: "1",
      },
    });
    await waitUntil(
      stackUp,
      `web ${WEB_URL} + api ${API_HEALTH_URL}`,
      BOOT_TIMEOUT_MS,
    );
  }

  console.log("[e2e-stack] ready: databases + api + web healthy");
  return async () => {
    if (!dev) return;

    const stopped =
      dev.exitCode === null ? waitForProcessExit(dev, 30_000) : null;
    terminateProcessTree(dev, "SIGTERM");
    await stopped;
  };
}

function terminateProcessTree(child, signal) {
  try {
    if (process.platform === "win32") {
      child.kill(signal);
      return;
    }

    // The supervisor starts pnpm as a process-group leader. Signalling the
    // group reaches dev-fast, whose shutdown handler owns its API/web trees.
    process.kill(-child.pid, signal);
  } catch (error) {
    if (error?.code !== "ESRCH") throw error;
  }
}

function waitForProcessExit(child, timeoutMs) {
  return new Promise((resolve) => {
    const timeout = setTimeout(resolve, timeoutMs);
    timeout.unref();
    child.once("exit", () => {
      clearTimeout(timeout);
      resolve();
    });
  });
}

// CLI mode: run as a long-lived process until SIGTERM/SIGINT.
if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  const stop = await startStackSupervisor();
  const shutdown = async () => {
    await stop();
    process.exit(0);
  };
  process.on("SIGTERM", () => void shutdown());
  process.on("SIGINT", () => void shutdown());
  setInterval(() => {}, 1 << 30);
}
