import { existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const emceptionRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = join(emceptionRoot, '..', '..');
const lockDir = join(repoRoot, '.cache', 'emception-libs.lock');

const targets = [
  { name: 'root lib', cwd: emceptionRoot, dist: join(emceptionRoot, 'dist', 'index.js'), cmd: 'pnpm run build:lib' },
  ...['core', 'xterm', 'browser', 'ide'].map((name) => ({
    name: `${name} package`,
    cwd: join(emceptionRoot, 'packages', name),
    dist: join(emceptionRoot, 'packages', name, 'dist', 'index.js'),
    cmd: 'pnpm run build',
  })),
];

function missing() {
  return targets.filter((target) => !existsSync(target.dist));
}

function acquireLock() {
  try {
    mkdirSync(lockDir, { recursive: false });
    return true;
  } catch {
    return false;
  }
}

if (missing().length > 0) {
  const deadline = Date.now() + 10 * 60 * 1000;
  while (!acquireLock()) {
    if (Date.now() > deadline) break;
    if (missing().length === 0) process.exit(0);
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  try {
    for (const target of missing()) {
      console.log(`[ensure-emception-libs] building ${target.name}`);
      const result = spawnSync(target.cmd, { cwd: target.cwd, stdio: 'inherit', shell: true });
      if (result.status !== 0) process.exit(result.status ?? 1);
    }
  } finally {
    const { rmSync } = await import('node:fs');
    rmSync(lockDir, { recursive: true, force: true });
  }
} else {
  console.log('[ensure-emception-libs] all dists present, skipping rebuild');
}
