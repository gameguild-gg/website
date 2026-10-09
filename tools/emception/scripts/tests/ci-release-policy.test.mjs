import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { test } from 'node:test';

async function readWorkflow() {
  const repoRoot = path.resolve(import.meta.dirname, '..', '..', '..', '..');
  return (await readFile(path.join(repoRoot, '.github', 'workflows', 'emception.yml'), 'utf8')).replaceAll('\r\n', '\n');
}

function namedStep(workflow, name) {
  const start = workflow.indexOf(`      - name: ${name}\n`);
  assert.notEqual(start, -1, `required step ${name} must exist`);
  const next = workflow.indexOf('\n      - name:', start + 1);
  return workflow.slice(start, next === -1 ? undefined : next);
}

test('validated Toolchain caches are saved before downstream package and browser failures', async () => {
  const workflow = await readWorkflow();
  for (const [kind, stepId] of [['downloads', 'toolchain-download-cache'], ['builds', 'toolchain-build-cache']]) {
    const restoreName = kind === 'downloads' ? 'Cache immutable Toolchain downloads' : 'Cache verified Toolchain builds';
    const saveName = kind === 'downloads' ? 'Save verified Toolchain downloads' : 'Save verified Toolchain builds';
    const restore = namedStep(workflow, restoreName);
    const save = namedStep(workflow, saveName);
    assert.match(restore, /uses: actions\/cache\/restore@55cc8345863c7cc4c66a329aec7e433d2d1c52a9/);
    assert.ok(restore.includes(`id: ${stepId}`));
    assert.match(save, /uses: actions\/cache\/save@55cc8345863c7cc4c66a329aec7e433d2d1c52a9/);
    assert.ok(save.includes(`if: success() && steps.${stepId}.outputs.cache-hit != 'true'`));
    assert.ok(save.includes(`key: \${{ steps.${stepId}.outputs.cache-primary-key }}`));
    const restorePath = restore.split('          path:')[1].split('          key:')[0].trim();
    const savePath = save.split('          path:')[1].split('          key:')[0].trim();
    assert.equal(savePath, restorePath, 'save and restore must cover exactly the same paths');
    assert.ok(workflow.indexOf('- name: Build and validate Toolchain receipts') < workflow.indexOf(`- name: ${saveName}`));
    assert.ok(workflow.indexOf(`- name: ${saveName}`) < workflow.indexOf('- name: Build all Emception packages'));
  }
});

test('only an exact validated cache avoids forced rebuild while all receipts are still verified', async () => {
  const workflow = await readWorkflow();
  const build = namedStep(workflow, 'Build and validate Toolchain receipts');
  assert.ok(build.includes('VERIFIED_BUILD_CACHE_HIT: ${{ steps.toolchain-build-cache.outputs.cache-hit }}'));
  assert.ok(build.includes('if [[ "$VERIFIED_BUILD_CACHE_HIT" == "true" ]]; then'));
  assert.doesNotMatch(build, /\$HEAVY_REBUILD/);
  assert.match(build, /then\n\s+pnpm --dir tools\/emception toolchain build all\n\s+else\n\s+pnpm --dir tools\/emception toolchain build all --force\n\s+fi/);
  assert.ok(workflow.indexOf('- name: Validate Toolchain lock and overlays') < workflow.indexOf('- name: Build and validate Toolchain receipts'));
});

test('Toolchain cache identity covers patch/build helpers, configuration and installed dependencies', async () => {
  const workflow = await readWorkflow();
  const restore = namedStep(workflow, 'Cache verified Toolchain builds');
  assert.match(restore, /key: emception-builds-linux-v2-/);
  for (const input of [
    'package.json',
    'pnpm-lock.yaml',
    'pnpm-workspace.yaml',
    '.npmrc',
    'tools/emception/package.json',
    'tools/emception/toolchain/**',
    'tools/emception/scripts/*',
    'tools/emception/scripts/toolchain/**',
    '.github/workflows/emception.yml',
  ]) {
    assert.ok(restore.includes(`'${input}'`), `cache identity must include ${input}`);
  }
});

test('failed coding cycles retain their actual web diagnostics directory', async () => {
  const workflow = await readWorkflow();
  const diagnostics = namedStep(workflow, 'Upload browser diagnostics on failure');
  assert.match(diagnostics, /if: failure\(\)/);
  assert.match(diagnostics, /apps\/web\/test-results\/coding-cycle(?:\n|$)/);
  assert.match(diagnostics, /tools\/emception\/playwright-report/);
  assert.match(diagnostics, /tools\/emception\/test-results/);
});

test('Emception CI is Linux-only, lockfile-driven, receipt-aware, and Changesets-based', async () => {
  const repoRoot = path.resolve(import.meta.dirname, '..', '..', '..', '..');
  const workflow = await readFile(path.join(repoRoot, '.github', 'workflows', 'emception.yml'), 'utf8');
  const runners = [...workflow.matchAll(/^\s*runs-on:\s*(.+)$/gm)].map((match) => match[1].trim());

  assert.equal(runners.length > 0, true);
  assert.deepEqual([...new Set(runners)], ['ubuntu-latest']);
  assert.match(workflow, /pnpm install --frozen-lockfile --ignore-scripts/);
  assert.doesNotMatch(workflow, /--no-lockfile|continue-on-error/);
  assert.doesNotMatch(workflow, /tools\/emception\/(?:userland|build|sysroot|tools\/emsdk)/);
  assert.match(workflow, /\.cache\/toolchain\/downloads/);
  assert.match(workflow, /artifacts\/toolchain\/receipts/);
  assert.match(workflow, /pnpm --dir tools\/emception toolchain build all/);
  assert.match(workflow, /pnpm --dir tools\/emception toolchain release/);
  assert.match(workflow, /pnpm --dir tools\/emception run verify:release/);
  assert.match(workflow, /changesets\/action@v2/);
  assert.match(workflow, /version-script: pnpm run version:emception/);
  assert.doesNotMatch(workflow, /auto-changeset\.mjs --apply/);
  assert.match(workflow, /tools\/emception\/packages\/toolchain\/cdn/);
  assert.match(workflow, /emception-v\$\{VERSION\}/);
  assert.equal(
    workflow.indexOf('- name: Build all Emception packages')
      < workflow.indexOf('- name: Generate clean release staging and packages'),
    true,
    'package clean/build must finish before the canonical CDN is staged',
  );
  assert.match(workflow, /pnpm --filter @game-guild\/client run build/);
  assert.equal(
    workflow.indexOf('- name: Build generated API client')
      < workflow.indexOf('- name: Run instructor and learner coding assessment cycle'),
    true,
    'the generated API client must exist before the coding-cycle runner imports it',
  );

  const ignore = await readFile(path.join(repoRoot, '.gitignore'), 'utf8');
  const rootPackage = JSON.parse(await readFile(path.join(repoRoot, 'package.json'), 'utf8'));
  assert.doesNotMatch(ignore, /^pnpm-lock\.yaml$/m);
  assert.doesNotMatch(rootPackage.scripts.clean, /pnpm-lock\.yaml/);
});
