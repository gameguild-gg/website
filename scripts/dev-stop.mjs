#!/usr/bin/env node

import { execFileSync, spawn } from "node:child_process";
import { readlinkSync, realpathSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  listRegisteredDevProcesses,
  removeDevProcessRegistration,
  repositoryRoot,
} from "./dev-runtime.mjs";

const PORTS = [3000, 8080];
const API_PROJECT = "apps/api/Source/GameGuild.API/GameGuild.API.csproj";
const dryRun = process.argv.includes("--dry-run");
const skipCompose = process.argv.includes("--skip-compose");

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function isAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

function isManagedCommand(command) {
  const normalized = command.replaceAll("\\", "/");
  return /^(?:\S*\/)?node(?:\.exe)?\s+(?:\S*\/)?scripts\/dev(?:-fast)?\.mjs(?:\s|$)/i.test(
    normalized,
  );
}

function modeFromCommand(command) {
  return command.replaceAll("\\", "/").includes("scripts/dev-fast.mjs")
    ? "dev:fast"
    : "dev";
}

export function isRepositoryApiWatcherCommand(command) {
  const normalized = command.replaceAll("\\", "/");
  const startsDotnetWatch = /^(?:\S*\/)?dotnet(?:\.exe)?\s+watch(?:\s|$)/i.test(
    normalized,
  );
  const runsDotnetWatchTool =
    /^(?:\S*\/)?dotnet(?:\.exe)?\s+\S*dotnet-watch\.dll(?:\s|$)/i.test(
      normalized,
    );

  return (
    (startsDotnetWatch || runsDotnetWatchTool) &&
    normalized.includes(API_PROJECT)
  );
}

function processCwd(pid) {
  if (process.platform === "linux") {
    try {
      return realpathSync(readlinkSync(`/proc/${pid}/cwd`));
    } catch {
      return null;
    }
  }

  if (process.platform === "darwin") {
    try {
      const output = execFileSync(
        "lsof",
        ["-a", "-p", String(pid), "-d", "cwd", "-Fn"],
        { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] },
      );
      const cwd = output
        .split("\n")
        .find((line) => line.startsWith("n"))
        ?.slice(1);
      return cwd ? realpathSync(cwd) : null;
    } catch {
      return null;
    }
  }

  return null;
}

export function parsePosixProcessList(output) {
  const processes = [];

  for (const line of output.split("\n")) {
    const match = line.match(/^\s*(\d+)\s+(\d+)\s+(\d+)\s+(.+)$/);
    if (!match) continue;
    processes.push({
      pid: Number(match[1]),
      parentPid: Number(match[2]),
      processGroupId: Number(match[3]),
      command: match[4],
    });
  }

  return processes;
}

function listPosixProcesses() {
  if (process.platform === "win32") return [];

  const args =
    process.platform === "darwin"
      ? ["-axo", "pid=,ppid=,pgid=,command="]
      : ["-eo", "pid=,ppid=,pgid=,args="];
  const output = execFileSync("ps", args, { encoding: "utf8" });
  return parsePosixProcessList(output);
}

function discoverDevProcesses() {
  const candidates = new Map();
  const registrations = listRegisteredDevProcesses();
  const processes = listPosixProcesses();
  const processByPid = new Map(processes.map((entry) => [entry.pid, entry]));

  for (const registration of registrations) {
    const entry = processByPid.get(registration.pid);
    const commandMatches =
      process.platform === "win32" ||
      (entry && isManagedCommand(entry.command));

    if (isAlive(registration.pid) && commandMatches) {
      candidates.set(registration.pid, {
        pid: registration.pid,
        mode: registration.mode,
        registration: registration.file,
      });
    } else {
      removeDevProcessRegistration(registration.file);
    }
  }

  for (const entry of processes) {
    if (!isManagedCommand(entry.command)) continue;
    if (processCwd(entry.pid) !== repositoryRoot) continue;

    candidates.set(entry.pid, {
      ...candidates.get(entry.pid),
      pid: entry.pid,
      mode: modeFromCommand(entry.command),
    });
  }

  return [...candidates.values()];
}

async function waitForExit(pids, timeoutMilliseconds) {
  const deadline = Date.now() + timeoutMilliseconds;
  let remaining = pids.filter(isAlive);

  while (remaining.length > 0 && Date.now() < deadline) {
    await delay(200);
    remaining = remaining.filter(isAlive);
  }

  return remaining;
}

function signalProcess(pid, signal) {
  if (!isAlive(pid)) return;

  if (process.platform === "win32") {
    execFileSync(
      "taskkill",
      ["/pid", String(pid), "/T", ...(signal === "SIGKILL" ? ["/F"] : [])],
      { stdio: "ignore" },
    );
    return;
  }

  process.kill(pid, signal);
}

function processTargetIsAlive(target) {
  try {
    process.kill(target.kind === "group" ? -target.id : target.id, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

function signalProcessTarget(target, signal) {
  if (!processTargetIsAlive(target)) return;
  process.kill(target.kind === "group" ? -target.id : target.id, signal);
}

async function waitForProcessTargets(targets, timeoutMilliseconds) {
  const deadline = Date.now() + timeoutMilliseconds;
  let remaining = targets.filter(processTargetIsAlive);

  while (remaining.length > 0 && Date.now() < deadline) {
    await delay(200);
    remaining = remaining.filter(processTargetIsAlive);
  }

  return remaining;
}

async function stopOrchestrators() {
  const processes = discoverDevProcesses();
  if (processes.length === 0) {
    console.log("[dev:stop] no managed development orchestrator is running");
    return;
  }

  for (const entry of processes) {
    console.log(
      `[dev:stop] ${dryRun ? "would stop" : "stopping"} ${entry.mode} orchestrator (pid ${entry.pid})`,
    );
  }
  if (dryRun) return;

  for (const entry of processes) {
    try {
      signalProcess(entry.pid, "SIGTERM");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  const remaining = await waitForExit(
    processes.map((entry) => entry.pid),
    8_000,
  );
  for (const pid of remaining) {
    console.log(`[dev:stop] forcing orchestrator shutdown (pid ${pid})`);
    try {
      signalProcess(pid, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  for (const entry of processes) {
    if (entry.registration) {
      removeDevProcessRegistration(entry.registration);
    }
  }
}

function discoverRepositoryApiWatchers() {
  const processes = listPosixProcesses();
  const processByPid = new Map(processes.map((entry) => [entry.pid, entry]));
  const cwdByPid = new Map();
  const targets = new Map();

  const belongsToRepository = (entry) => {
    if (!isRepositoryApiWatcherCommand(entry.command)) return false;
    if (!cwdByPid.has(entry.pid))
      cwdByPid.set(entry.pid, processCwd(entry.pid));
    return cwdByPid.get(entry.pid) === repositoryRoot;
  };

  for (const entry of processes) {
    if (!belongsToRepository(entry)) continue;

    const groupLeader = processByPid.get(entry.processGroupId);
    const ownsDedicatedGroup =
      entry.processGroupId > 1 &&
      groupLeader &&
      groupLeader.pid === groupLeader.processGroupId &&
      belongsToRepository(groupLeader);
    const target = ownsDedicatedGroup
      ? { kind: "group", id: entry.processGroupId }
      : { kind: "process", id: entry.pid };

    targets.set(`${target.kind}:${target.id}`, target);
  }

  return [...targets.values()];
}

async function stopRepositoryApiWatchers() {
  const targets = discoverRepositoryApiWatchers();
  if (targets.length === 0) return;

  for (const target of targets) {
    console.log(
      `[dev:stop] ${dryRun ? "would stop" : "stopping"} repository API watcher (${target.kind} ${target.id})`,
    );
  }
  if (dryRun) return;

  for (const target of targets) {
    try {
      signalProcessTarget(target, "SIGTERM");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  const remaining = await waitForProcessTargets(targets, 4_000);
  for (const target of remaining) {
    console.log(
      `[dev:stop] forcing repository API watcher shutdown (${target.kind} ${target.id})`,
    );
    try {
      signalProcessTarget(target, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }
}

function listenersForPort(port) {
  if (process.platform === "win32") {
    const output = execFileSync("netstat", ["-ano", "-p", "tcp"], {
      encoding: "utf8",
    });
    const pids = new Set();
    for (const line of output.split("\n")) {
      const columns = line.trim().split(/\s+/);
      if (
        columns.length >= 5 &&
        columns[1]?.endsWith(`:${port}`) &&
        columns[3] === "LISTENING"
      ) {
        pids.add(Number(columns[4]));
      }
    }
    return [...pids].filter(Number.isInteger);
  }

  try {
    const output = execFileSync("lsof", [`-tiTCP:${port}`, "-sTCP:LISTEN"], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    return [
      ...new Set(
        output
          .split("\n")
          .map(Number)
          .filter((pid) => Number.isInteger(pid) && pid > 0),
      ),
    ];
  } catch (error) {
    if (error?.status === 1) return [];
    throw error;
  }
}

async function stopPortListeners() {
  const listeners = new Map();
  for (const port of PORTS) {
    for (const pid of listenersForPort(port)) {
      if (pid === process.pid) continue;
      const ports = listeners.get(pid) ?? [];
      ports.push(port);
      listeners.set(pid, ports);
    }
  }

  if (listeners.size === 0) {
    console.log("[dev:stop] ports 3000 and 8080 are free");
    return;
  }

  for (const [pid, ports] of listeners) {
    console.log(
      `[dev:stop] ${dryRun ? "would stop" : "stopping"} fallback listener on ${ports.join(", ")} (pid ${pid})`,
    );
  }
  if (dryRun) return;

  for (const pid of listeners.keys()) {
    try {
      signalProcess(pid, "SIGTERM");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }

  const remaining = await waitForExit([...listeners.keys()], 2_000);
  for (const pid of remaining) {
    console.log(`[dev:stop] forcing listener shutdown (pid ${pid})`);
    try {
      signalProcess(pid, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") throw error;
    }
  }
}

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: repositoryRoot,
      shell: process.platform === "win32",
      stdio: "inherit",
    });
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (code === 0) resolve();
      else {
        reject(
          new Error(
            `${command} failed (${signal ?? `exit ${code ?? "unknown"}`})`,
          ),
        );
      }
    });
  });
}

async function main() {
  await stopOrchestrators();
  await stopRepositoryApiWatchers();
  await stopPortListeners();

  if (skipCompose) return;
  if (dryRun) {
    console.log("[dev:stop] would run docker compose down --remove-orphans");
    return;
  }

  console.log("[dev:stop] stopping Docker infrastructure");
  await run("docker", [
    "compose",
    "-f",
    path.join(repositoryRoot, "compose.yaml"),
    "down",
    "--remove-orphans",
  ]);
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main().catch((error) => {
    console.error(
      `[dev:stop] ${error instanceof Error ? error.message : String(error)}`,
    );
    process.exitCode = 1;
  });
}
