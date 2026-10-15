import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
// Guard logic lives in the script itself (node runs it directly, so it must be
// plain ESM); vitest can import .mjs via a relative path regardless of its
// `src/**` include pattern (include only governs test discovery).
import {
  ensureCanonicalRelease,
  extractPackageArchive,
  manifestsMatch,
  SOURCE_CANONICAL,
  syncEmceptionCdn,
} from '../../../scripts/sync-emception-cdn.mjs';

const tempDirs: string[] = [];

async function makeTree() {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'emception-sync-'));
  tempDirs.push(root);
  return root;
}

function canonicalManifest() {
  return JSON.stringify({ schemaVersion: 2, artifactVersion: '4.3.0' });
}

afterEach(async () => {
  await Promise.all(tempDirs.splice(0).map((dir) => fs.rm(dir, { recursive: true, force: true })));
});

describe('manifestsMatch', () => {
  it('returns true when source and target manifests are byte-identical', async () => {
    const root = await makeTree();
    const src = path.join(root, 'src', 'manifest.json');
    const tgt = path.join(root, 'tgt', 'manifest.json');
    await fs.mkdir(path.dirname(src), { recursive: true });
    await fs.mkdir(path.dirname(tgt), { recursive: true });
    const content = canonicalManifest();
    await fs.writeFile(src, content);
    await fs.writeFile(tgt, content);
    await expect(manifestsMatch(src, tgt)).resolves.toBe(true);
  });

  it('returns false when manifests differ', async () => {
    const root = await makeTree();
    const src = path.join(root, 'src', 'manifest.json');
    const tgt = path.join(root, 'tgt', 'manifest.json');
    await fs.mkdir(path.dirname(src), { recursive: true });
    await fs.mkdir(path.dirname(tgt), { recursive: true });
    await fs.writeFile(src, canonicalManifest());
    await fs.writeFile(tgt, JSON.stringify({ schemaVersion: 2, artifactVersion: '4.2.0' }));
    await expect(manifestsMatch(src, tgt)).resolves.toBe(false);
  });

  it('returns false when target manifest is missing', async () => {
    const root = await makeTree();
    const src = path.join(root, 'src', 'manifest.json');
    const tgt = path.join(root, 'tgt', 'manifest.json');
    await fs.mkdir(path.dirname(src), { recursive: true });
    await fs.writeFile(src, canonicalManifest());
    await expect(manifestsMatch(src, tgt)).resolves.toBe(false);
  });
});

describe('extractPackageArchive', () => {
  it('passes Windows paths directly to the portable tar library', async () => {
    const calls: unknown[] = [];
    const archivePath = 'C:\\Temp\\gameguild-emception\\emception-4.3.0.tgz';
    const destination = 'C:\\Temp\\gameguild-emception';

    await extractPackageArchive(archivePath, destination, async (options) => { calls.push(options); });

    expect(calls).toEqual([{ cwd: destination, file: archivePath, strict: true }]);
  });
});

describe('syncEmceptionCdn', () => {
  it('uses the canonical Toolchain release directory by default', () => {
    expect(SOURCE_CANONICAL).toContain(path.join('tools', 'emception', 'artifacts', 'toolchain', 'release', 'cdn'));
  });

  it('first run copies the canonical Toolchain release', async () => {
    const root = await makeTree();
    const src = path.join(root, 'cdn');
    const tgt = path.join(root, 'public', 'emception');
    await fs.mkdir(src);
    await fs.writeFile(path.join(src, 'manifest.json'), canonicalManifest());
    await fs.writeFile(path.join(src, 'bundle.tar.br'), 'bytes');

    const logs: string[] = [];
    const result = await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: (m) => logs.push(m) });

    expect(result).toEqual({ action: 'synced', source: src });
    expect(await fs.readFile(path.join(tgt, 'manifest.json'), 'utf8')).toBe(canonicalManifest());
    expect(await fs.readFile(path.join(tgt, 'bundle.tar.br'), 'utf8')).toBe('bytes');
    expect(logs.join('\n')).toContain('copied');
  });

  it('second run with matching manifest is a no-op', async () => {
    const root = await makeTree();
    const src = path.join(root, 'cdn');
    const tgt = path.join(root, 'public', 'emception');
    await fs.mkdir(src);
    await fs.writeFile(path.join(src, 'manifest.json'), canonicalManifest());
    await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} });

    const logs: string[] = [];
    const result = await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: (m) => logs.push(m) });

    expect(result).toEqual({ action: 'skip', source: src });
    expect(logs.join('\n')).toContain('up to date');
  });

  it('re-copies when a target asset is missing despite a matching manifest', async () => {
    const root = await makeTree();
    const src = path.join(root, 'cdn');
    const tgt = path.join(root, 'public', 'emception');
    await fs.mkdir(src);
    await fs.writeFile(path.join(src, 'manifest.json'), canonicalManifest());
    await fs.writeFile(path.join(src, 'bundle.tar.br'), 'bytes');
    await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} });
    await fs.rm(path.join(tgt, 'bundle.tar.br'));

    const result = await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} });

    expect(result).toEqual({ action: 'synced', source: src });
    await expect(fs.readFile(path.join(tgt, 'bundle.tar.br'), 'utf8')).resolves.toBe('bytes');
  });

  it('diverged target manifest triggers a re-copy', async () => {
    const root = await makeTree();
    const src = path.join(root, 'cdn');
    const tgt = path.join(root, 'public', 'emception');
    await fs.mkdir(src);
    await fs.writeFile(path.join(src, 'manifest.json'), canonicalManifest());
    await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} });

    const updatedManifest = JSON.stringify({ schemaVersion: 2, artifactVersion: '4.3.1' });
    await fs.writeFile(path.join(src, 'manifest.json'), updatedManifest);
    const result = await syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} });

    expect(result).toEqual({ action: 'synced', source: src });
    expect(await fs.readFile(path.join(tgt, 'manifest.json'), 'utf8')).toBe(updatedManifest);
  });
  it('refuses a missing canonical Toolchain release', async () => {
    const root = await makeTree();
    const srcMissing = path.join(root, 'no-such-src');
    const tgt = path.join(root, 'public', 'emception');

    await expect(
      syncEmceptionCdn({
        srcDir: srcMissing,
        tgtDir: tgt,
        log: () => {},
      }),
    ).rejects.toThrow(/canonical Toolchain release is unavailable/);
  });

  it('refuses a legacy manifest before it can replace the target', async () => {
    const root = await makeTree();
    const src = path.join(root, 'cdn');
    const tgt = path.join(root, 'public', 'emception');
    await fs.mkdir(src);
    await fs.mkdir(tgt, { recursive: true });
    await fs.writeFile(path.join(src, 'manifest.json'), '{"version":1}');
    await fs.writeFile(path.join(tgt, 'manifest.json'), canonicalManifest());

    await expect(syncEmceptionCdn({ srcDir: src, tgtDir: tgt, log: () => {} }))
      .rejects.toThrow(/schemaVersion 2/);
    await expect(fs.readFile(path.join(tgt, 'manifest.json'), 'utf8')).resolves.toBe(canonicalManifest());
  });
});

describe('ensureCanonicalRelease', () => {
  it('hydrates a missing canonical release from the pinned package version', async () => {
    const root = await makeTree();
    const src = path.join(root, 'canonical', 'cdn');
    const packageJson = path.join(root, 'toolchain-package.json');
    await fs.writeFile(packageJson, JSON.stringify({ version: '4.3.0' }));

    const result = await ensureCanonicalRelease({
      srcDir: src,
      versionPackagePath: packageJson,
      log: () => {},
      hydrate: async ({ srcDir, version }) => {
        await fs.mkdir(srcDir, { recursive: true });
        await fs.writeFile(
          path.join(srcDir, 'manifest.json'),
          JSON.stringify({ schemaVersion: 2, artifactVersion: version }),
        );
      },
    });

    expect(result).toEqual({ action: 'hydrated', source: src, version: '4.3.0', schemaVersion: 2 });
  });

  it('accepts a hydrate fallback to the latest published version when the pin is unpublished', async () => {
    const root = await makeTree();
    const src = path.join(root, 'canonical', 'cdn');
    const packageJson = path.join(root, 'toolchain-package.json');
    await fs.writeFile(packageJson, JSON.stringify({ version: '4.4.1' }));

    const logs: string[] = [];
    const result = await ensureCanonicalRelease({
      srcDir: src,
      versionPackagePath: packageJson,
      log: (m) => logs.push(m),
      hydrate: async ({ srcDir: dir, version }) => {
        // Mirrors hydratePublishedRelease: the pinned tarball is unpublished, so
        // npm pack falls back to the latest published version.
        const packed = version === '4.4.1' ? '4.4.0' : version;
        await fs.mkdir(dir, { recursive: true });
        await fs.writeFile(
          path.join(dir, 'manifest.json'),
          JSON.stringify({ schemaVersion: 2, artifactVersion: packed }),
        );
        return { version: packed };
      },
    });

    expect(result).toEqual({ action: 'hydrated', source: src, version: '4.4.0', schemaVersion: 2 });
  });

  it('rejects a hydrated release whose manifest does not match the pinned version', async () => {
    const root = await makeTree();
    const src = path.join(root, 'canonical', 'cdn');
    const packageJson = path.join(root, 'toolchain-package.json');
    await fs.writeFile(packageJson, JSON.stringify({ version: '4.3.0' }));

    await expect(ensureCanonicalRelease({
      srcDir: src,
      versionPackagePath: packageJson,
      log: () => {},
      hydrate: async ({ srcDir }) => {
        await fs.mkdir(srcDir, { recursive: true });
        await fs.writeFile(
          path.join(srcDir, 'manifest.json'),
          JSON.stringify({ schemaVersion: 2, artifactVersion: '4.2.0' }),
        );
      },
    })).rejects.toThrow(/does not match 4\.3\.0/);
  });

  it('accepts the pinned package legacy manifest as an explicit compatibility fallback', async () => {
    const root = await makeTree();
    const src = path.join(root, 'canonical', 'cdn');
    const packageJson = path.join(root, 'toolchain-package.json');
    await fs.writeFile(packageJson, JSON.stringify({ version: '4.3.0' }));

    const result = await ensureCanonicalRelease({
      srcDir: src,
      versionPackagePath: packageJson,
      log: () => {},
      hydrate: async ({ srcDir }) => {
        await fs.mkdir(srcDir, { recursive: true });
        await fs.writeFile(path.join(srcDir, 'manifest.json'), JSON.stringify({ version: 1 }));
      },
    });

    expect(result).toEqual({ action: 'hydrated', source: src, version: '4.3.0', schemaVersion: 1 });
  });
});
