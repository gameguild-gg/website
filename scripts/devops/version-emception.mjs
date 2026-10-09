import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import {
  assertOnlyEmceptionPackageManifests,
  readEmceptionReleaseVersion,
  synchronizeEmceptionRuntimePeers,
} from './emception-release-policy.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const pnpm = process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm';

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: repoRoot, encoding: 'utf8', stdio: options.capture ? 'pipe' : 'inherit' });
  if (result.status !== 0) throw new Error(`${command} ${args.join(' ')} failed with exit ${result.status}`);
  return result.stdout ?? '';
}

export async function versionEmception() {
  run(pnpm, ['exec', 'changeset', 'version']);
  await synchronizeEmceptionRuntimePeers(repoRoot);
  const version = await readEmceptionReleaseVersion(repoRoot);
  run(pnpm, ['install', '--lockfile-only', '--ignore-scripts']);
  const changedPaths = run('git', ['diff', '--name-only', 'HEAD'], { capture: true })
    .split(/\r?\n/)
    .filter(Boolean);
  assertOnlyEmceptionPackageManifests(changedPaths);
  console.log(`[version-emception] Prepared the seven Emception packages at ${version}.`);
}

if (path.resolve(process.argv[1] ?? '') === fileURLToPath(import.meta.url)) {
  versionEmception().catch((error) => {
    console.error(`[version-emception] ${error instanceof Error ? error.message : String(error)}`);
    process.exitCode = 1;
  });
}
