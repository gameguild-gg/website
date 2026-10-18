import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { copyFile, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { promisify } from 'node:util';
import { test } from 'node:test';

const executeFile = promisify(execFile);
const packages = ['apps/web', 'packages/ui', 'packages/infrastructure/client'];

async function fixture(t, counts = [1, 2, 3], budgets = [1, 2, 3]) {
  const root = await mkdtemp(join(tmpdir(), 'gameguild-lint-budget-contract-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(join(root, 'scripts'));
  const script = join(root, 'scripts/lint-budget.mjs');
  await copyFile(new URL('../../lint-budget.mjs', import.meta.url), script);
  for (const [index, name] of packages.entries()) {
    await mkdir(join(root, name), { recursive: true });
    await writeFile(join(root, name, 'eslint-suppressions.json'), JSON.stringify({
      'synthetic.ts': { 'synthetic-rule': { count: counts[index] } },
    }));
  }
  const budgetFile = join(root, 'lint-budgets.json');
  await writeFile(budgetFile, JSON.stringify(Object.fromEntries(packages.map((name, index) => [name, budgets[index]]))));
  return { root, script, budgetFile };
}

test('unchanged and reduced counts pass without rewriting the budget', async (t) => {
  const setup = await fixture(t, [0, 2, 3]);
  const before = await readFile(setup.budgetFile, 'utf8');
  const result = await executeFile(process.execPath, [setup.script], { timeout: 10_000 });
  assert.equal(result.stderr, '');
  assert.match(result.stdout, /Lint budgets OK\./);
  assert.equal(await readFile(setup.budgetFile, 'utf8'), before);
});

test('new suppressed violations fail without raising the checked-in budget', async (t) => {
  const setup = await fixture(t, [2, 2, 3]);
  const before = await readFile(setup.budgetFile, 'utf8');
  await assert.rejects(executeFile(process.execPath, [setup.script], { timeout: 10_000 }), (error) => {
    assert.equal(error.code, 1);
    assert.match(error.stderr, /Lint budget exceeded/);
    assert.match(error.stdout, /GREW/);
    return true;
  });
  assert.equal(await readFile(setup.budgetFile, 'utf8'), before);
});

test('explicit update awaits the complete budget write', async (t) => {
  const setup = await fixture(t, [0, 1, 2]);
  const result = await executeFile(process.execPath, [setup.script, '--update'], { timeout: 10_000 });
  assert.equal(result.stderr, '');
  assert.match(result.stdout, /lint-budgets.json updated:/);
  assert.deepEqual(JSON.parse(await readFile(setup.budgetFile, 'utf8')), {
    'apps/web': 0, 'packages/ui': 1, 'packages/infrastructure/client': 2,
  });
});

test('missing suppression input fails instead of treating the package as empty', async (t) => {
  const setup = await fixture(t);
  await rm(join(setup.root, packages[0], 'eslint-suppressions.json'));
  await assert.rejects(executeFile(process.execPath, [setup.script], { timeout: 10_000 }), (error) => {
    assert.equal(error.code, 1);
    assert.match(error.stderr, /missing suppression file: apps\/web\/eslint-suppressions.json/);
    return true;
  });
});
