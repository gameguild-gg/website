import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { resolvePnpmProcess } from './pnpm-process.mjs';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

const clientDist = join(repoRoot, 'packages', 'infrastructure', 'client', 'dist', 'index.d.ts');
const runtimeDists = ['core', 'xterm', 'browser', 'ide'].map((name) =>
  join(repoRoot, 'tools', 'emception', 'packages', name, 'dist', 'index.js'),
);

function run(args, cwd) {
  const invocation = resolvePnpmProcess(args, { env: process.env });
  const result = spawnSync(invocation.command, invocation.args, { cwd, stdio: 'inherit', shell: false, windowsHide: true });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

if (!existsSync(clientDist)) {
  run(['run', 'build'], join(repoRoot, 'packages', 'infrastructure', 'client'));
} else {
  console.log('[build:emception-dependencies] client dist present, skipping rebuild');
}

if (runtimeDists.some((dist) => !existsSync(dist))) {
  for (const name of ['core', 'xterm', 'browser', 'ide']) {
    run(['run', 'build'], join(repoRoot, 'tools', 'emception', 'packages', name));
  }
} else {
  console.log('[build:emception-dependencies] emception runtime dists present, skipping rebuild');
}
