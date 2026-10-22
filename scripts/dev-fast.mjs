#!/usr/bin/env node
// Fast development orchestrator. This intentionally stays independent from
// scripts/dev.mjs so the regular Webpack and generated-client watcher flow
// remains unchanged.
import { spawn } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";
import { resolveDevelopmentDataProtectionEnvironment } from "./dev-data-protection.mjs";
import { registerDevProcess } from "./dev-runtime.mjs";
import { resolvePnpmProcess } from "../apps/web/scripts/pnpm-process.mjs";

await registerDevProcess("dev-fast");

const API_PORT = 8080;
const API_HEALTH_URL = `http://localhost:${API_PORT}/health`;
const DEFAULT_API_STARTUP_TIMEOUT_MS = 10 * 60 * 1000;
const CHILD_SHUTDOWN_GRACE_MS = 4_000;
const WEB_WORKSPACE = path.resolve("apps/web");
const NEXT_CLI = path.resolve(
  WEB_WORKSPACE,
  "node_modules/next/dist/bin/next",
);
const runtimeEnv = process.env;
const skipCompose = process.env.DEV_FAST_SKIP_COMPOSE === "1";
const watchApi = process.env.DEV_FAST_API_WATCH !== "0";
const skipApiBuild = !watchApi && process.env.DEV_FAST_API_NO_BUILD === "1";
const webBundler = process.env.DEV_FAST_WEB_BUNDLER ?? "turbopack";
if (webBundler !== "turbopack" && webBundler !== "webpack") {
  throw new Error(
    `DEV_FAST_WEB_BUNDLER must be "turbopack" or "webpack"; received "${webBundler}"`,
  );
}
// Resolve once before starting children; spawning remains synchronous so every
// caller can attach lifecycle listeners immediately to the returned child.
const pnpmProcess = await resolvePnpmProcess([], { env: runtimeEnv });

function loadEnv(file) {
  const env = {};
  for (const line of readFileSync(file, "utf8").split("\n")) {
    if (line.trim().startsWith("#")) continue;
    const match = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/);
    if (!match) continue;
    const value = match[2].trim();
    env[match[1]] =
      value.startsWith('"') || value.startsWith("'")
        ? value.slice(1, -1)
        : value;
  }
  return env;
}

function run(cmd, args, options = {}) {
  const invocation =
    cmd === "pnpm"
      ? { command: pnpmProcess.command, args: [...pnpmProcess.args, ...args] }
      : { command: cmd, args };
  return spawn(invocation.command, invocation.args, {
    // The orchestrator owns Ctrl+C. Detached children must not read from the
    // parent TTY or an orphaned watcher can consume input after shutdown.
    stdio: ["ignore", "inherit", "inherit"],
    detached: process.platform !== "win32",
    env: runtimeEnv,
    ...options,
    shell: false,
    windowsHide: true,
  });
}

function waitForSuccessfulExit(child, label) {
  return new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (code === 0) resolve();
      else {
        reject(
          new Error(
            `${label} failed (${signal ?? `exit ${code ?? "unknown"}`})`,
          ),
        );
      }
    });
  });
}

function killTree(child, signal = "SIGTERM") {
  if (!child?.pid) return;
  if (process.platform === "win32") {
    if (child.exitCode !== null) return;
    spawn("taskkill", ["/pid", String(child.pid), "/T", "/F"], {
      shell: false,
      windowsHide: true,
    });
  } else {
    try {
      process.kill(-child.pid, signal);
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }
}

function spawnReaper(pgid) {
  const source = `
    const pgid = Number(process.argv[1]);
    const delayMs = Number(process.argv[2]);
    setTimeout(() => {
      try {
        process.kill(-pgid, "SIGKILL");
      } catch (error) {
        if (error?.code !== "ESRCH") process.exitCode = 1;
      }
    }, delayMs);
  `;
  spawn(
    process.execPath,
    ["--eval", source, String(pgid), String(CHILD_SHUTDOWN_GRACE_MS)],
    {
      detached: true,
      stdio: "ignore",
    },
  ).unref();
}

function restoreTerminal() {
  if (process.stdin.isTTY && typeof process.stdin.setRawMode === "function") {
    try {
      process.stdin.setRawMode(false);
    } catch {
      // The terminal may already have been detached by the invoking shell.
    }
  }
  if (process.stdout.isTTY) process.stdout.write("\u001B[?25h");
}

process.once("exit", restoreTerminal);

function assertPortFree(port) {
  return new Promise((resolve) => {
    const probe = net.createServer();
    probe.once("error", (error) => {
      if (error.code !== "EADDRINUSE") return resolve();
      console.error(
        `[dev:fast] port ${port} is already in use. Run pnpm run dev:stop, then retry.`,
      );
      process.exit(1);
    });
    probe.once("listening", () => probe.close(resolve));
    probe.listen(port, "localhost");
  });
}

async function waitForApiHealth(
  url,
  timeoutMs = Number(
    process.env.DEV_API_STARTUP_TIMEOUT_MS ?? DEFAULT_API_STARTUP_TIMEOUT_MS,
  ),
) {
  const deadline = Date.now() + timeoutMs;
  let nextProgressAt = Date.now() + 30_000;
  let lastFailure = "no response";

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url, {
        signal: AbortSignal.timeout(2_000),
      });
      if (response.ok) {
        console.log(`[dev:fast] API ready at ${url}`);
        return;
      }
      lastFailure = `HTTP ${response.status}`;
    } catch (error) {
      lastFailure = error instanceof Error ? error.message : String(error);
    }
    if (Date.now() >= nextProgressAt) {
      console.log(
        `[dev:fast] API is still starting (${lastFailure}); continuing to wait`,
      );
      nextProgressAt = Date.now() + 30_000;
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  throw new Error(
    `API did not become healthy at ${url} within ${timeoutMs}ms (${lastFailure})`,
  );
}

function rejectWhenChildExits(child, label) {
  return new Promise((_, reject) => {
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      reject(
        new Error(
          `${label} exited before becoming ready (${signal ?? `exit ${code ?? "unknown"}`})`,
        ),
      );
    });
  });
}

if (!skipCompose) {
  const compose = run("docker", [
    "compose",
    "-f",
    "compose.yaml",
    "up",
    "-d",
    "--wait",
  ]);
  const composeCode = await new Promise((resolve) =>
    compose.on("exit", resolve),
  );
  if (composeCode !== 0) process.exit(composeCode ?? 1);
}

await assertPortFree(API_PORT);

console.log(
  `[dev:fast] starting API${watchApi ? " with hot reload" : " without hot reload"} and preparing the web application`,
);
const apiEnvironment = resolveDevelopmentDataProtectionEnvironment({
  environment: {
    ...process.env,
    ...loadEnv(".env"),
  },
});
const api = spawn(
  "dotnet",
  watchApi
    ? [
        "watch",
        "--project",
        "apps/api/Source/GameGuild.API/GameGuild.API.csproj",
        "run",
        "--urls",
        "http://localhost:8080",
      ]
    : [
        "run",
        ...(skipApiBuild ? ["--no-build"] : []),
        "--project",
        "apps/api/Source/GameGuild.API/GameGuild.API.csproj",
        "--urls",
        "http://localhost:8080",
      ],
  {
    stdio: ["ignore", "inherit", "inherit"],
    detached: process.platform !== "win32",
    env: {
      ...apiEnvironment,
      DOTNET_USE_POLLING_FILE_WATCHER: "true",
    },
  },
);

const preparationChildren = [
  run("pnpm", ["--filter", "@game-guild/client", "run", "build:fast"]),
];
const preparation = [
  waitForSuccessfulExit(
    preparationChildren[0],
    "generated client runtime build",
  ),
  Promise.race([
    waitForApiHealth(API_HEALTH_URL),
    rejectWhenChildExits(api, "API process"),
  ]),
];

if (!existsSync("apps/web/public/emception/manifest.json")) {
  console.log("[dev:fast] Emception assets are missing; synchronizing once");
  const emceptionSync = run("pnpm", [
    "--filter",
    "@game-guild/web",
    "run",
    "sync:emception",
  ]);
  preparationChildren.push(emceptionSync);
  preparation.push(
    waitForSuccessfulExit(emceptionSync, "Emception asset synchronization"),
  );
}

if (!existsSync("apps/web/public/langs/quickjs-asyncify.wasm.gz")) {
  console.log("[dev:fast] QuickJS runtime is missing; synchronizing once");
  const javascriptRuntimeSync = run("pnpm", [
    "--filter",
    "@game-guild/web",
    "run",
    "sync:javascript-runtime",
  ]);
  preparationChildren.push(javascriptRuntimeSync);
  preparation.push(
    waitForSuccessfulExit(
      javascriptRuntimeSync,
      "JavaScript runtime synchronization",
    ),
  );
}

let web = null;
let shuttingDown = false;
let shutdownTreeRoots = [];

function treeIsAlive(child) {
  if (!child?.pid) return false;
  if (process.platform === "win32") return child.exitCode === null;

  try {
    process.kill(-child.pid, 0);
    return true;
  } catch (error) {
    if (error?.code === "ESRCH") return false;
    if (error?.code === "EPERM") return true;
    throw error;
  }
}

async function waitForTreesToExit(children, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let remaining = children.filter(treeIsAlive);

  while (remaining.length > 0 && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 100));
    remaining = remaining.filter(treeIsAlive);
  }
  return remaining;
}

const shutdown = async (exitCode = 0, signal = "SIGTERM") => {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log("\n[dev:fast] shutting down...");
  restoreTerminal();
  shutdownTreeRoots = [
    api,
    ...preparationChildren,
    ...(web ? [web] : []),
  ].filter(treeIsAlive);
  for (const child of shutdownTreeRoots) killTree(child, signal);
  if (process.platform !== "win32") {
    for (const child of shutdownTreeRoots) {
      if (child.pid) spawnReaper(child.pid);
    }
  }

  const remaining = await waitForTreesToExit(
    shutdownTreeRoots,
    CHILD_SHUTDOWN_GRACE_MS,
  );
  for (const child of remaining) killTree(child, "SIGKILL");
  await waitForTreesToExit(remaining, 1_000);
  restoreTerminal();
  process.exit(exitCode);
};

const onSignal = (signal) => {
  if (shuttingDown) {
    for (const child of shutdownTreeRoots) killTree(child, "SIGKILL");
    restoreTerminal();
    process.exit(0);
  }
  void shutdown(0, signal);
};
process.on("SIGINT", () => onSignal("SIGINT"));
process.on("SIGTERM", () => onSignal("SIGTERM"));

try {
  await Promise.all(preparation);
} catch (error) {
  console.error(
    `[dev:fast] ${error instanceof Error ? error.message : String(error)}`,
  );
  void shutdown(1);
  await new Promise(() => {});
}

const webHeapMb = process.env.DEV_FAST_WEB_MAX_OLD_SPACE_SIZE_MB;
const webEnvironment = webHeapMb
  ? {
      ...runtimeEnv,
      NODE_OPTIONS: withMaxOldSpaceSize(runtimeEnv.NODE_OPTIONS, webHeapMb),
    }
  : runtimeEnv;
const webCommand = [
  NEXT_CLI,
  "dev",
  webBundler === "webpack" ? "--webpack" : "--turbopack",
];
// Next is the process-group leader here. Avoiding a pnpm wrapper means a
// normal SIGTERM cannot be reported as a recursive-run failure during teardown.
web = run(process.execPath, webCommand, {
  cwd: WEB_WORKSPACE,
  env: webEnvironment,
});
console.log(
  `[dev:fast] dynamic ${webBundler === "webpack" ? "Webpack" : "Turbopack"} mode enabled; SDK polling and declarations disabled${webHeapMb ? `; web heap capped at ${webHeapMb} MB` : ""}`,
);

web.on("exit", (code) => {
  void shutdown(code ?? 0);
});

function withMaxOldSpaceSize(currentOptions, rawMegabytes) {
  const megabytes = Number(rawMegabytes);
  if (!Number.isInteger(megabytes) || megabytes < 1024) {
    throw new Error(
      `DEV_FAST_WEB_MAX_OLD_SPACE_SIZE_MB must be an integer of at least 1024; received "${rawMegabytes}"`,
    );
  }

  const remaining = (currentOptions ?? "")
    .replace(/--max-old-space-size(?:=|\s+)\d+/g, "")
    .trim();
  return `${remaining} --max-old-space-size=${megabytes}`.trim();
}
