import { existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { resolvePnpmProcess } from '../../../apps/web/scripts/pnpm-process.mjs';

const emceptionRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = join(emceptionRoot, '..', '..');
const lockDir = join(repoRoot, '.cache', 'emception-libs.lock');

const targets = [
  { name: 'root lib', cwd: emceptionRoot, dist: join(emceptionRoot, 'dist', 'index.js'), args: ['run', 'build:lib'] },
  ...['core', 'xterm', 'browser', 'ide'].map((name) => ({
    name: `${name} package`,
    cwd: join(emceptionRoot, 'packages', name),
    dist: join(emceptionRoot, 'packages', name, 'dist', 'index.js'),
    args: ['run', 'build'],
  })),
];

function missing() {
  return targets.filter((target) => !existsSync(target.dist));
}

function acquireLock() {
  try {
    mkdirSync(lockDir, { recursive: false });
    return true;
  } catch (error) {
    if (error.code === 'EEXIST') return false;
    throw error;
  }
}

async function main() {
  if (missing().length > 0) {
    mkdirSync(dirname(lockDir), { recursive: true });
    const deadline = Date.now() + 10 * 60 * 1000;
    let waitingLogged = false;
    while (!acquireLock()) {
      if (!waitingLogged) {
        console.log(`[ensure-emception-libs] waiting for builder lock: ${lockDir}`);
        waitingLogged = true;
      }
      if (Date.now() > deadline) throw new Error(`Timed out waiting for library builder lock: ${lockDir}`);
      if (missing().length === 0) return;
      await new Promise((resolve) => setTimeout(resolve, 500));
    }
    try {
      for (const target of missing()) {
        console.log(`[ensure-emception-libs] building ${target.name}`);
        const invocation = resolvePnpmProcess(target.args, { env: process.env });
        const result = spawnSync(invocation.command, invocation.args, { cwd: target.cwd, stdio: 'inherit', shell: false, windowsHide: true });
        if (result.status !== 0) {
          const error = new Error(`Failed to build ${target.name}`, { cause: result.error });
          error.exitCode = result.status ?? 1;
          throw error;
        }
      }
    } finally {
      // This process reached the build block only after acquiring the directory.
      rmSync(lockDir, { recursive: true, force: true });
    }
  } else {
    console.log('[ensure-emception-libs] all dists present, skipping rebuild');
  }
}

main().catch((error) => {
  console.error('[ensure-emception-libs] failed:', error);
  process.exitCode = error.exitCode ?? 1;
});
