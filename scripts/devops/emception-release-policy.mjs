import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

export const EMCEPTION_PACKAGE_DIRECTORIES = [
  'tools/emception/packages/core',
  'tools/emception/packages/toolchain',
  'tools/emception/packages/browser',
  'tools/emception/packages/xterm',
  'tools/emception/packages/react',
  'tools/emception/packages/webcomponent',
  'tools/emception/packages/ide',
];

const EMCEPTION_PACKAGE_MANIFESTS = new Set(
  EMCEPTION_PACKAGE_DIRECTORIES.map((directory) => `${directory}/package.json`),
);

function normalize(filename) {
  return filename.replaceAll('\\', '/').replace(/^\.\//, '');
}

export function assertOnlyEmceptionPackageManifests(changedPaths) {
  const unexpected = changedPaths
    .map(normalize)
    .filter((filename) => filename.endsWith('/package.json') || filename === 'package.json')
    .filter((filename) => !EMCEPTION_PACKAGE_MANIFESTS.has(filename));
  if (unexpected.length > 0) {
    throw new Error(`Versioning changed package.json outside the Emception release group: ${unexpected.join(', ')}`);
  }
}

export async function readEmceptionReleaseVersion(repoRoot) {
  const versions = new Set();
  for (const directory of EMCEPTION_PACKAGE_DIRECTORIES) {
    const manifest = JSON.parse(await readFile(path.join(repoRoot, directory, 'package.json'), 'utf8'));
    versions.add(manifest.version);
  }
  if (versions.size !== 1) throw new Error(`Emception package versions differ: ${[...versions].join(', ')}`);
  return [...versions][0];
}

/** Keep internal runtime peers at the minimum version of the fixed release group. */
export async function synchronizeEmceptionRuntimePeers(repoRoot) {
  const version = await readEmceptionReleaseVersion(repoRoot);
  const manifests = await Promise.all(EMCEPTION_PACKAGE_DIRECTORIES.map(async (directory) => {
    const filename = path.join(repoRoot, directory, 'package.json');
    return { filename, manifest: JSON.parse(await readFile(filename, 'utf8')) };
  }));
  const internalNames = new Set(manifests.map(({ manifest }) => manifest.name));
  const changed = [];
  for (const { filename, manifest } of manifests) {
    let updated = false;
    for (const [name, range] of Object.entries(manifest.peerDependencies ?? {})) {
      if (internalNames.has(name) && range !== `^${version}`) {
        manifest.peerDependencies[name] = `^${version}`;
        updated = true;
      }
    }
    if (updated) {
      await writeFile(filename, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
      changed.push(path.relative(repoRoot, filename).split(path.sep).join('/'));
    }
  }
  return changed;
}

/** Preserve the established version-returning helper for existing release callers. */
export async function synchronizeEmceptionPeerVersions(repoRoot) {
  await synchronizeEmceptionRuntimePeers(repoRoot);
  return readEmceptionReleaseVersion(repoRoot);
}
