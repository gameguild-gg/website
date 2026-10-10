#!/usr/bin/env node
// Dev orchestrator — cross-platform (Windows/macOS/Linux).
//
// 1. docker compose up -d --wait   (postgres/redis/garage/mailhog)
// 2. start api/web/client-watchers in parallel (see below)
//
// Ctrl+C tears down all children (taskkill /T on Windows, SIGTERM+SIGKILL
// process-group kill elsewhere). See spawnReaper for why a helper process
// does the final sweep.
import { spawn } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import net from 'node:net';
import { resolvePnpmProcess } from '../apps/web/scripts/pnpm-process.mjs';

const API_PORT = 8080;

// Minimal KEY=VALUE .env parser. Spawning `pnpm dev:api` instead would put
// dotenv-cli/dotnet watch inside pnpm's own process groups, where group
// kills can't reach them — they ignore SIGTERM, survive teardown, and an
// orphaned GameGuild.API then keeps port 8080 so every later run
// crash-loops on "address already in use".
function loadEnv(file) {
  const env = {};
  for (const line of readFileSync(file, 'utf8').split('\n')) {
    if (line.trim().startsWith('#')) continue;
    const match = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/);
    if (!match) continue;
    const value = match[2].trim();
    env[match[1]] = value.startsWith('"') || value.startsWith("'") ? value.slice(1, -1) : value;
  }
  return env;
}

// POSIX: detached makes each child its own process-group leader, so the
// negative-pid group kill below reaches its whole subtree.
function run(cmd, args) {
  const invocation = cmd === 'pnpm'
    ? resolvePnpmProcess(args, { env: process.env })
    : { command: cmd, args };
  return spawn(invocation.command, invocation.args, {
    shell: false,
    stdio: 'inherit',
    detached: process.platform !== 'win32',
    windowsHide: true,
  });
}

function killTree(child) {
  if (child.exitCode !== null) return;
  if (process.platform === 'win32') {
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { shell: false, windowsHide: true });
  } else {
    try {
      process.kill(-child.pid, 'SIGTERM');
    } catch {
      /* group already gone */
    }
  }
}

// pnpm force-kills this script shortly after SIGTERM — in-process kill
// timers never get to run. A detached reaper survives us and SIGKILLs the
// child's process group once the graceful window closes (dotnet watch
// ignores SIGTERM and otherwise lingers forever).
function spawnReaper(pgid) {
  spawn('sh', ['-c', `sleep 4; kill -9 -- -${pgid} 2>/dev/null; true`], {
    detached: true,
    stdio: 'ignore',
  }).unref();
}

// Fail fast with an actionable message instead of a silent bind-fail loop.
function assertPortFree(port) {
  return new Promise((resolve) => {
    const probe = net.createServer();
    probe.once('error', (err) => {
      if (err.code !== 'EADDRINUSE') return resolve();
      console.error(
        `[dev] port ${port} is already in use — probably an orphaned API from an earlier run.\n` +
          `      Fix: lsof -ti:${port} | xargs kill -9   then retry.`
      );
      process.exit(1);
    });
    probe.once('listening', () => probe.close(resolve));
    probe.listen(port, 'localhost');
  });
}

// ── 1. docker compose ────────────────────────────────────────────────────────
const compose = run('docker', ['compose', '-f', 'compose.yaml', 'up', '-d', '--wait']);
const composeCode = await new Promise((resolve) => compose.on('exit', resolve));
if (composeCode !== 0) process.exit(composeCode ?? 1);

await assertPortFree(API_PORT);

// ── 2. Everything in parallel ────────────────────────────────────────────────
// API, web, client generate:watch and build:watch all start immediately.
// generate:watch polls the spec every 10s and retries until the API is up,
// build:watch waits for regen output, web hot-reloads on client changes.
console.log('[dev] starting API, web, client generate:watch, client build:watch');
const api = spawn(
  'dotnet',
  ['watch', '--project', 'apps/api/Source/GameGuild.API/GameGuild.API.csproj', 'run', '--urls', 'http://localhost:8080'],
  {
    stdio: 'inherit',
    detached: process.platform !== 'win32',
    env: {
      ...process.env,
      ...loadEnv('.env'),
      // Polling watcher: FSEvents-backed watchers crash dotnet watch on
      // macOS 26 (fatal PAL_SEHException + infinite watcher-recreate loop).
      DOTNET_USE_POLLING_FILE_WATCHER: 'true',
    },
  }
);
// Build client up front so dist/index.js exists before starting watchers/web
const initialBuild = run('pnpm', ['--filter', '@game-guild/client', 'run', 'build']);
await new Promise((resolve) => initialBuild.on('exit', resolve));

const children = [
  run('pnpm', ['--filter', '@game-guild/client', 'run', 'generate:watch']),
  run('pnpm', ['--filter', '@game-guild/client', 'run', 'build:watch']),
  run('pnpm', ['--filter', '@game-guild/web', 'run', 'dev']),
];
const [clientGen, clientBuild, web] = children;

let shuttingDown = false;
const shutdown = () => {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log('\n[dev] shutting down...');
  const treeRoots = [api, ...children];
  for (const child of treeRoots) killTree(child);
  if (process.platform !== 'win32') {
    for (const child of treeRoots) {
      if (child.pid) spawnReaper(child.pid);
    }
  }
  setTimeout(() => process.exit(0), 6000);
};
// Second Ctrl+C force-quits.
const onSignal = () => (shuttingDown ? process.exit(0) : shutdown());
process.on('SIGINT', onSignal);
process.on('SIGTERM', onSignal);

// Watcher processes failing must not take down dev — log and continue.
for (const [name, child] of [
  ['client generate:watch', clientGen],
  ['client build:watch', clientBuild],
]) {
  child.on('exit', (code) => {
    if (code !== 0 && code !== null) console.error(`[dev] ${name} exited (code ${code}) — continuing without it`);
  });
}

// When web exits (Ctrl+C on its foreground output), tear everything down.
web.on('exit', () => {
  shutdown();
});
