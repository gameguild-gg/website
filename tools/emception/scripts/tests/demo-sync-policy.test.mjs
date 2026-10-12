import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { test } from 'node:test';

test('demo synchronization consumes only the canonical Toolchain release', async () => {
  const repoRoot = path.resolve(import.meta.dirname, '..', '..', '..', '..');
  const source = await readFile(path.join(repoRoot, 'scripts', 'sync-emception-cdn.mjs'), 'utf8');

  assert.match(source, /path\.join\(emceptionRoot, 'artifacts', 'toolchain', 'release', 'cdn'\)/);
  assert.doesNotMatch(source, /sourceBuildCdnDir|sourceManifestFile|sourcePublicCdnDir/);
  assert.doesNotMatch(source, /path\.join\(emceptionRoot, 'build'/);
  assert.doesNotMatch(source, /mode: 'build'|mode: 'public'/);
});

test('Next.js demo boots the local manifest staged by its development and build hooks', async () => {
  const repoRoot = path.resolve(import.meta.dirname, '..', '..', '..', '..');
  const demoRoot = path.join(repoRoot, 'demos', 'emception-ide-next');
  const source = await readFile(path.join(demoRoot, 'src', 'app', 'page.tsx'), 'utf8');
  const packageJson = JSON.parse(await readFile(path.join(demoRoot, 'package.json'), 'utf8'));

  for (const hook of ['predev', 'prebuild']) {
    assert.ok(packageJson.scripts[hook].includes(
      'install-app-cdn-from-package.mjs ../../demos/emception-ide-next',
    ), `${hook} must stage this demo's CDN`);
  }
  assert.match(source, /<Ide\b[^>]*\bmanifestUrl=['"]\/cdn\/manifest\.json['"]/,
    'the self-hosted demo must boot its staged manifest instead of the published package default');
});
