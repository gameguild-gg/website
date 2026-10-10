import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { CODE_TOOLCHAIN_V1, requireFrozenCodeToolchain } from './toolchain-binding.mjs';

const manifest = { schemaVersion: 2, ...CODE_TOOLCHAIN_V1, buildReceiptHash: 'a'.repeat(64), buildFingerprint: 'b'.repeat(64) };

test('adapter identity matches the reviewed toolchain lock and ABI', async () => {
  const [lock, config, pkg] = await Promise.all(
    ['toolchain/toolchain.lock.json', 'toolchain/toolchain.config.json', 'packages/toolchain/package.json'].map(async (path) =>
      JSON.parse(await readFile(new URL(`../${path}`, import.meta.url), 'utf8')),
    ),
  );
  // Same ordering and trailing newline as serializeToolchainLock.
  function sortDeep(value) {
    if (Array.isArray(value)) return value.map(sortDeep);
    if (value && typeof value === 'object')
      return Object.fromEntries(
        Object.entries(value)
          .sort(([left], [right]) => left.localeCompare(right))
          .map(([key, entry]) => [key, sortDeep(entry)]),
      );
    return value;
  }
  assert.equal(
    createHash('sha256')
      .update(JSON.stringify(sortDeep(lock), null, 2) + '\n')
      .digest('hex'),
    CODE_TOOLCHAIN_V1.toolchainLockHash,
  );
  assert.equal(config.runtimeAbi, CODE_TOOLCHAIN_V1.runtimeAbi);
  assert.equal(pkg.version, CODE_TOOLCHAIN_V1.artifactVersion);
});

test('version 1 binding matches the compiled server adapter constants', async () => {
  const source = await readFile(
    new URL('../../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Code/CodeAssessmentTypeAdapter.cs', import.meta.url),
    'utf8',
  );
  for (const value of Object.values(CODE_TOOLCHAIN_V1)) assert.ok(source.includes(`"${value}"`));
  assert.doesNotThrow(() => requireFrozenCodeToolchain(CODE_TOOLCHAIN_V1, manifest));
});

for (const [name, patch] of Object.entries({
  legacy: { schemaVersion: undefined },
  changedCompiler: { artifactVersion: '999' },
  changedAbi: { runtimeAbi: 'next' },
  changedLock: { toolchainLockHash: 'c'.repeat(64) },
  missingReceipt: { buildReceiptHash: undefined },
  corruptFingerprint: { buildFingerprint: 'bad' },
})) {
  test(`rejects ${name} artifacts without executing learner code`, () => {
    assert.throws(() => requireFrozenCodeToolchain(CODE_TOOLCHAIN_V1, { ...manifest, ...patch }));
  });
}

test('rejects an unknown or extended request binding', () => {
  assert.throws(() => requireFrozenCodeToolchain({ ...CODE_TOOLCHAIN_V1, artifactVersion: '999' }, manifest));
  assert.throws(() => requireFrozenCodeToolchain({ ...CODE_TOOLCHAIN_V1, newest: true }, manifest));
});
