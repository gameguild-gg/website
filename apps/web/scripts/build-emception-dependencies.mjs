import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

const clientDist = join(repoRoot, 'packages', 'infrastructure', 'client', 'dist', 'index.d.ts');
const runtimeDists = ['core', 'xterm', 'browser', 'ide'].map((name) =>
  join(repoRoot, 'tools', 'emception', 'packages', name, 'dist', 'index.js'),
);

function run(cmd, cwd) {
  const result = spawnSync(cmd, { cwd, stdio: 'inherit', shell: true });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

if (!existsSync(clientDist)) {
  run('pnpm run build', join(repoRoot, 'packages', 'infrastructure', 'client'));
} else {
  console.log('[build:emception-dependencies] client dist present, skipping rebuild');
}

if (runtimeDists.some((dist) => !existsSync(dist))) {
  for (const name of ['core', 'xterm', 'browser', 'ide']) {
    run('pnpm run build', join(repoRoot, 'tools', 'emception', 'packages', name));
  }
} else {
  console.log('[build:emception-dependencies] emception runtime dists present, skipping rebuild');
}
