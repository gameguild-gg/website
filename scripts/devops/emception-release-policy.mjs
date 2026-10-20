import { mkdir, readFile, readdir, rename, writeFile } from 'node:fs/promises';
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

// The four demos are unpublished consumers that pin exact internal ranges; the
// version bump rewrites those ranges so the workspace does not pin the stale
// published version. Their manifests changing is expected consumer maintenance,
// not a release-scope violation.
const EMCEPTION_DEMO_MANIFESTS = new Set([
  'demos/emception-ide-next/package.json',
  'demos/emception-ide-react/package.json',
  'demos/emception-run-react/package.json',
  'demos/emception-run-webcomponent/package.json',
]);

function normalize(filename) {
  return filename.replaceAll('\\', '/').replace(/^\.\//, '');
}

export function assertOnlyEmceptionPackageManifests(changedPaths) {
  const unexpected = changedPaths
    .map(normalize)
    .filter((filename) => filename.endsWith('/package.json') || filename === 'package.json')
    .filter((filename) => !EMCEPTION_PACKAGE_MANIFESTS.has(filename))
    .filter((filename) => !EMCEPTION_DEMO_MANIFESTS.has(filename));
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

const CHANGESET_DIRECTORY = '.changeset';

/** Package names of the seven-package Emception release group, read from their manifests. */
async function emceptionPackageNames(repoRoot) {
  const names = new Set();
  for (const directory of EMCEPTION_PACKAGE_DIRECTORIES) {
    const manifest = JSON.parse(await readFile(path.join(repoRoot, directory, 'package.json'), 'utf8'));
    names.add(manifest.name);
  }
  return names;
}

/** Frontmatter package names of a changeset file; empty when the file has no frontmatter. */
function changesetTargets(content) {
  const frontmatter = /^---\r?\n([\s\S]*?)\r?\n---/.exec(content);
  if (!frontmatter) return [];
  return [...frontmatter[1].matchAll(/^\s*['"]?([^'":\n]+?)['"]?\s*:/gm)].map((match) => match[1].trim());
}

/**
 * Move changesets that target any package outside the Emception release group into
 * .changeset/archive/ so `changeset version` cannot bump foreign manifests. The
 * content stays in-tree, visible to whoever authors the eventual changelog entry.
 */
export async function archiveForeignChangesets(repoRoot) {
  const changesetDir = path.join(repoRoot, CHANGESET_DIRECTORY);
  const archiveDir = path.join(changesetDir, 'archive');
  const emceptionNames = await emceptionPackageNames(repoRoot);
  const archived = [];
  for (const entry of await readdir(changesetDir, { withFileTypes: true })) {
    if (!entry.isFile() || !entry.name.endsWith('.md') || entry.name === 'README.md') continue;
    const source = path.join(changesetDir, entry.name);
    const targets = changesetTargets(await readFile(source, 'utf8'));
    if (targets.length > 0 && targets.every((name) => emceptionNames.has(name))) continue;
    await mkdir(archiveDir, { recursive: true });
    await rename(source, path.join(archiveDir, entry.name));
    archived.push(`${CHANGESET_DIRECTORY}/${entry.name}`);
  }
  return archived;
}
