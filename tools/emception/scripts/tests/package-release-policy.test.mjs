import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { test } from 'node:test';

const emceptionRoot = path.resolve(import.meta.dirname, '..', '..');
const repoRoot = path.resolve(emceptionRoot, '..', '..');

const EMCEPTION_PACKAGES = [
  'emception',
  '@gameguild/emception-toolchain',
  '@gameguild/emception-browser',
  '@gameguild/emception-xterm',
  '@gameguild/emception-react',
  '@gameguild/emception-webcomponent',
  '@gameguild/emception-ide',
];

const INTERNAL_RUNTIME_DEPENDENCIES = {
  '@gameguild/emception-browser': ['emception', '@gameguild/emception-xterm'],
  '@gameguild/emception-xterm': ['emception'],
  '@gameguild/emception-react': ['emception', '@gameguild/emception-browser'],
  '@gameguild/emception-webcomponent': ['emception', '@gameguild/emception-browser'],
  '@gameguild/emception-ide': ['emception', '@gameguild/emception-browser'],
};

const INTERNAL_RUNTIME_PEERS = {
  '@gameguild/emception-browser': ['@xterm/xterm'],
  '@gameguild/emception-react': ['@gameguild/emception-xterm'],
  '@gameguild/emception-webcomponent': ['@gameguild/emception-xterm'],
  '@gameguild/emception-ide': ['@gameguild/emception-xterm'],
};

async function json(filename) {
  return JSON.parse(await readFile(filename, 'utf8'));
}

test('published Emception packages have one version and the intended dependency direction', async () => {
  const packageDirectories = ['core', 'toolchain', 'browser', 'xterm', 'react', 'webcomponent', 'ide'];
  const packages = Object.fromEntries(await Promise.all(packageDirectories.map(async (directory) => {
    const manifest = await json(path.join(emceptionRoot, 'packages', directory, 'package.json'));
    return [manifest.name, manifest];
  })));
  const releaseVersion = packages.emception.version;

  for (const [consumer, dependencies] of Object.entries(INTERNAL_RUNTIME_DEPENDENCIES)) {
    for (const dependency of dependencies) {
      assert.equal(packages[consumer].dependencies?.[dependency], releaseVersion, `${consumer} must use ${dependency} at the release version`);
    }
  }
  for (const [consumer, dependencies] of Object.entries(INTERNAL_RUNTIME_PEERS)) {
    for (const dependency of dependencies) {
      const expectedRange = dependency === '@xterm/xterm' ? '^6.0.0' : `^${releaseVersion}`;
      assert.equal(packages[consumer].peerDependencies?.[dependency], expectedRange, `${consumer} must require a compatible ${dependency}`);
    }
  }
  const versions = new Set(EMCEPTION_PACKAGES.map((name) => packages[name].version));

  assert.equal(versions.size, 1);
  assert.equal(packages.emception.dependencies?.['@gameguild/emception-toolchain'], undefined);
  assert.equal(packages['@gameguild/emception-browser'].dependencies.emception, packages.emception.version);
  assert.equal(packages['@gameguild/emception-browser'].dependencies['@gameguild/emception-toolchain'], undefined);
  assert.equal(packages['@gameguild/emception-ide'].dependencies.emception, packages.emception.version);
  assert.equal(
    packages['@gameguild/emception-ide'].dependencies['@gameguild/emception-browser'],
    packages.emception.version,
  );
  assert.equal(packages['@gameguild/emception-ide'].dependencies['@gameguild/emception-toolchain'], undefined);
});

test('private workspace links public packages without duplicating their release version', async () => {
  const workspace = await json(path.join(emceptionRoot, 'package.json'));

  assert.equal(workspace.private, true);
  assert.equal(workspace.dependencies['@gameguild/emception-browser'], 'workspace:*');
  assert.equal(workspace.dependencies['@gameguild/emception-xterm'], 'workspace:*');
});

async function runtimePeerFixture(context, mixedVersions = false) {
  const { EMCEPTION_PACKAGE_DIRECTORIES } = await import('../../../../scripts/devops/emception-release-policy.mjs');
  const root = await mkdtemp(path.join(tmpdir(), 'emception-runtime-peers-'));
  context.after(() => rm(root, { recursive: true, force: true }));
  const names = ['emception', '@gameguild/emception-toolchain', '@gameguild/emception-browser',
    '@gameguild/emception-xterm', '@gameguild/emception-react', '@gameguild/emception-webcomponent', '@gameguild/emception-ide'];
  for (const [index, directory] of EMCEPTION_PACKAGE_DIRECTORIES.entries()) {
    await mkdir(path.join(root, directory), { recursive: true });
    const manifest = {
      name: names[index], version: mixedVersions && index === 6 ? '4.4.0' : '4.5.0',
      dependencies: { emception: '4.5.0' },
      peerDependencies: index >= 4 ? { '@gameguild/emception-xterm': '^4.4.0', react: '^19' } : { '@xterm/xterm': '^6.0.0' },
      peerDependenciesMeta: { '@gameguild/emception-xterm': { optional: true } },
    };
    await writeFile(path.join(root, directory, 'package.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  }
  return { root, directories: EMCEPTION_PACKAGE_DIRECTORIES };
}

test('version preparation advances internal runtime peers and preserves external peers and optional metadata', async (context) => {
  const { synchronizeEmceptionRuntimePeers } = await import('../../../../scripts/devops/emception-release-policy.mjs');
  const { root, directories } = await runtimePeerFixture(context);
  const before = await Promise.all(directories.map((directory) => json(path.join(root, directory, 'package.json'))));

  assert.deepEqual(await synchronizeEmceptionRuntimePeers(root), directories.slice(4).map((directory) => `${directory}/package.json`));
  for (const [index, directory] of directories.entries()) {
    const expected = structuredClone(before[index]);
    if (index >= 4) expected.peerDependencies['@gameguild/emception-xterm'] = '^4.5.0';
    assert.deepEqual(await json(path.join(root, directory, 'package.json')), expected);
  }
  const bytes = await Promise.all(directories.map((directory) => readFile(path.join(root, directory, 'package.json'), 'utf8')));
  assert.deepEqual(await synchronizeEmceptionRuntimePeers(root), []);
  assert.deepEqual(await Promise.all(directories.map((directory) => readFile(path.join(root, directory, 'package.json'), 'utf8'))), bytes);
});

test('inconsistent release versions reject runtime peer updates before writing any manifest', async (context) => {
  const { synchronizeEmceptionRuntimePeers } = await import('../../../../scripts/devops/emception-release-policy.mjs');
  const { root, directories } = await runtimePeerFixture(context, true);
  const before = await Promise.all(directories.map((directory) => readFile(path.join(root, directory, 'package.json'), 'utf8')));

  await assert.rejects(synchronizeEmceptionRuntimePeers(root), /Emception package versions differ/);
  assert.deepEqual(await Promise.all(directories.map((directory) => readFile(path.join(root, directory, 'package.json'), 'utf8'))), before);
});

test('package tests build Xterm before testing Browser from a clean checkout', async () => {
  const workspace = await json(path.join(emceptionRoot, 'package.json'));
  const packageTests = workspace.scripts['test:packages'];

  assert.match(packageTests, /test:packages:xterm/);
  assert.equal(
    packageTests.indexOf('test:packages:xterm') < packageTests.indexOf('test:packages:browser'),
    true,
    'Browser imports Xterm declarations from its built package output',
  );
});

test('Changesets fixes only the seven public Emception packages together', async () => {
  const config = await json(path.join(repoRoot, '.changeset', 'config.json'));
  const expected = [...EMCEPTION_PACKAGES].sort();
  const emceptionGroups = config.fixed.filter((group) => group.includes('emception'));

  assert.equal(emceptionGroups.length, 1);
  assert.deepEqual([...emceptionGroups[0]].sort(), expected);
  assert.equal(config.fixed.some((group) => group.includes('emception-workspace')), false);
  assert.equal(config.fixed.some((group) => group.some((name) => name.startsWith('@gameguild/emception-demo-'))), false);
  assert.equal(config.ignore.includes('@game-guild/emception-ui'), true);
  assert.equal(config.ignore.includes('emception-workspace'), true);
  assert.equal(config.ignore.filter((name) => name.startsWith('@gameguild/emception-demo-')).length, 4);
});

test('Emception versioning rejects package manifests outside the seven-package release group', async () => {
  const { assertOnlyEmceptionPackageManifests } = await import(
    '../../../../scripts/devops/emception-release-policy.mjs'
  );
  const allowed = [
    'tools/emception/packages/core/package.json',
    'tools/emception/packages/toolchain/package.json',
    'tools/emception/packages/browser/package.json',
    'tools/emception/packages/xterm/package.json',
    'tools/emception/packages/react/package.json',
    'tools/emception/packages/webcomponent/package.json',
    'tools/emception/packages/ide/package.json',
    'pnpm-lock.yaml',
  ];

  assert.doesNotThrow(() => assertOnlyEmceptionPackageManifests(allowed));
  assert.throws(
    () => assertOnlyEmceptionPackageManifests([...allowed, 'packages/infrastructure/ui-emception/package.json']),
    /outside the Emception release group/,
  );
  assert.throws(
    () => assertOnlyEmceptionPackageManifests([...allowed, 'tools/emception/package.json']),
    /outside the Emception release group/,
  );
});

test('release versioning refreshes internal peers while preserving external peers and other manifest fields', async (context) => {
  const { EMCEPTION_PACKAGE_DIRECTORIES, synchronizeEmceptionPeerVersions } = await import(
    '../../../../scripts/devops/emception-release-policy.mjs'
  );
  const root = await mkdtemp(path.join(tmpdir(), 'emception-peer-policy-'));
  context.after(() => rm(root, { recursive: true, force: true }));
  const originals = [];
  for (const [index, directory] of EMCEPTION_PACKAGE_DIRECTORIES.entries()) {
    const filename = path.join(root, directory, 'package.json');
    await mkdir(path.dirname(filename), { recursive: true });
    const manifest = {
      name: `@fixture/package-${index}`, version: '1.2.3', description: 'preserved',
      dependencies: { '@fixture/package-0': '1.2.3' },
      ...(index === 4 ? {
        peerDependencies: { '@fixture/package-3': '^1.0.0', react: '^19.0.0' },
        peerDependenciesMeta: { '@fixture/package-3': { optional: true } },
      } : {}),
    };
    const content = `${JSON.stringify(manifest)}\n`;
    await writeFile(filename, content);
    originals.push({ filename, manifest, content });
  }

  assert.equal(await synchronizeEmceptionPeerVersions(root), '1.2.3');
  for (const [index, original] of originals.entries()) {
    const content = await readFile(original.filename, 'utf8');
    const expected = structuredClone(original.manifest);
    if (index === 4) expected.peerDependencies['@fixture/package-3'] = '^1.2.3';
    else assert.equal(content, original.content);
    assert.deepEqual(JSON.parse(content), expected);
  }
  const first = await Promise.all(originals.map(({ filename }) => readFile(filename, 'utf8')));
  await synchronizeEmceptionPeerVersions(root);
  assert.deepEqual(await Promise.all(originals.map(({ filename }) => readFile(filename, 'utf8'))), first);

  const mismatched = { ...originals[0].manifest, version: '1.2.4' };
  await writeFile(originals[0].filename, JSON.stringify(mismatched));
  const beforeRejection = await Promise.all(originals.map(({ filename }) => readFile(filename, 'utf8')));
  await assert.rejects(synchronizeEmceptionPeerVersions(root), /package versions differ/);
  assert.deepEqual(await Promise.all(originals.map(({ filename }) => readFile(filename, 'utf8'))), beforeRejection);
});

test('publication packs Toolchain first and waits for the registry before consumers', async (context) => {
  const { EMCEPTION_PUBLISH_ORDER, publishEmception } = await import(
    '../../../../scripts/devops/publish-emception.mjs'
  );
  const root = await mkdtemp(path.join(tmpdir(), 'emception-publish-'));
  context.after(() => rm(root, { recursive: true, force: true }));
  const published = [];
  let toolchainVisible = false;
  for (const entry of EMCEPTION_PUBLISH_ORDER) {
    const packageRoot = path.join(root, entry.directory);
    await mkdir(packageRoot, { recursive: true });
    await writeFile(path.join(packageRoot, 'package.json'), `${JSON.stringify({ name: entry.name, version: '1.2.3' })}\n`);
  }
  const runCommand = (_command, args) => {
    if (args[0] === 'view') {
      return { status: args[1].startsWith('@gameguild/emception-toolchain@') && toolchainVisible ? 0 : 1, stdout: '' };
    }
    if (args[0] === 'publish') {
      const entry = EMCEPTION_PUBLISH_ORDER.find(({ directory }) => args[1] === `./${directory}`);
      published.push(entry.name);
      if (entry.name === '@gameguild/emception-toolchain') toolchainVisible = true;
    }
    return { status: 0, stdout: '[]' };
  };

  await publishEmception({ repoRoot: root, runCommand, wait: async () => {} });

  assert.equal(EMCEPTION_PUBLISH_ORDER[0].name, '@gameguild/emception-toolchain');
  assert.deepEqual(published, EMCEPTION_PUBLISH_ORDER.map(({ name }) => name));
});

test('maintained source and documentation use only canonical Toolchain paths', async () => {
  const filenames = [
    path.join(emceptionRoot, 'README.md'),
    path.join(emceptionRoot, 'docs', 'architecture.md'),
    path.join(emceptionRoot, 'docs', 'build.md'),
    path.join(emceptionRoot, 'packages', 'toolchain', 'README.md'),
    path.join(emceptionRoot, 'packages', 'browser', 'src', 'worker-entry.ts'),
  ];
  const stalePath = /(?:tools\/emception\/userland|build\/cdn|build\/stage|mutable `sysroot\/`|├── sysroot\/|tools\/emception\/tools\/emsdk)/;

  for (const filename of filenames) {
    assert.doesNotMatch(await readFile(filename, 'utf8'), stalePath, `${filename} references a removed Toolchain path`);
  }
});
