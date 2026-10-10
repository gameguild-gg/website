#!/usr/bin/env node
// Fast development orchestrator. This intentionally stays independent from
// scripts/dev.mjs so the regular Webpack and generated-client watcher flow
// remains unchanged.
import { spawn } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import net from 'node:net';
import { registerDevProcess } from './dev-runtime.mjs';
import { resolvePnpmProcess } from '../apps/web/scripts/pnpm-process.mjs';

registerDevProcess('dev-fast');

const API_PORT = 8080;
const runtimeEnv = process.env;

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

function run(cmd, args, options = {}) {
  const invocation = cmd === 'pnpm' ? resolvePnpmProcess(args, { env: runtimeEnv }) : { command: cmd, args };
  return spawn(invocation.command, invocation.args, {
    stdio: 'inherit',
    detached: process.platform !== 'win32',
    env: runtimeEnv,
    ...options,
    shell: false,
    windowsHide: true,
  });
}

function waitForSuccessfulExit(child, label) {
  return new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code, signal) => {
      if (code === 0) resolve();
      else {
        reject(new Error(`${label} failed (${signal ?? `exit ${code ?? 'unknown'}`})`));
      }
    });
  });
}

function killTree(child) {
  if (child.exitCode !== null) return;
  if (process.platform === 'win32') {
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], {
      shell: false,
      windowsHide: true,
    });
  } else {
    try {
      process.kill(-child.pid, 'SIGTERM');
    } catch {
      // The process group has already exited.
    }
  }
}

function spawnReaper(pgid) {
  spawn('sh', ['-c', `sleep 4; kill -9 -- -${pgid} 2>/dev/null; true`], {
    detached: true,
    stdio: 'ignore',
  }).unref();
}

function assertPortFree(port) {
  return new Promise((resolve) => {
    const probe = net.createServer();
    probe.once('error', (error) => {
      if (error.code !== 'EADDRINUSE') return resolve();
      console.error(`[dev:fast] port ${port} is already in use. Run pnpm run dev:stop, then retry.`);
      process.exit(1);
    });
    probe.once('listening', () => probe.close(resolve));
    probe.listen(port, 'localhost');
  });
}

const compose = run('docker', ['compose', '-f', 'compose.yaml', 'up', '-d', '--wait']);
const composeCode = await new Promise((resolve) => compose.on('exit', resolve));
if (composeCode !== 0) process.exit(composeCode ?? 1);

await assertPortFree(API_PORT);

console.log('[dev:fast] starting API and preparing the web application');
const api = spawn('dotnet', ['watch', '--project', 'apps/api/Source/GameGuild.API/GameGuild.API.csproj', 'run', '--urls', 'http://localhost:8080'], {
  stdio: 'inherit',
  detached: process.platform !== 'win32',
  env: {
    ...process.env,
    ...loadEnv('.env'),
    DOTNET_USE_POLLING_FILE_WATCHER: 'true',
  },
});

const preparationChildren = [run('pnpm', ['--filter', '@game-guild/client', 'run', 'build:fast'])];
const preparation = [waitForSuccessfulExit(preparationChildren[0], 'generated client runtime build')];

if (!existsSync('apps/web/public/emception/manifest.json')) {
  console.log('[dev:fast] Emception assets are missing; synchronizing once');
  const emceptionSync = run('pnpm', ['--filter', '@game-guild/web', 'run', 'sync:emception']);
  preparationChildren.push(emceptionSync);
  preparation.push(waitForSuccessfulExit(emceptionSync, 'Emception asset synchronization'));
}

let web = null;
let shuttingDown = false;
const shutdown = (exitCode = 0) => {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log('\n[dev:fast] shutting down...');
  const treeRoots = [api, ...preparationChildren, ...(web ? [web] : [])];
  for (const child of treeRoots) killTree(child);
  if (process.platform !== 'win32') {
    for (const child of treeRoots) {
      if (child.pid) spawnReaper(child.pid);
    }
  }
  setTimeout(() => process.exit(exitCode), 6000);
};

const onSignal = () => (shuttingDown ? process.exit(0) : shutdown());
process.on('SIGINT', onSignal);
process.on('SIGTERM', onSignal);

try {
  await Promise.all(preparation);
} catch (error) {
  console.error(`[dev:fast] ${error instanceof Error ? error.message : String(error)}`);
  shutdown(1);
  await new Promise(() => {});
}

web = run('pnpm', ['--filter', '@game-guild/web', 'run', 'dev:fast']);
console.log('[dev:fast] dynamic Turbopack mode enabled; SDK polling and declarations disabled');

web.on('exit', (code) => {
  shutdown(code ?? 0);
});
