import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { ESLint } from 'eslint';
import config from '../../../eslint.config.mjs';

const repositoryRoot = fileURLToPath(new URL('../../../', import.meta.url));
const eslint = new ESLint({
  cwd: repositoryRoot,
  overrideConfigFile: path.join(repositoryRoot, 'eslint.config.mjs'),
});

for (const extension of ['js', 'mjs', 'cjs', 'jsx', 'ts', 'mts', 'cts', 'tsx']) {
  test(`root config analyzes .${extension} files without excluding them`, async () => {
    const file = `scripts/ci/root-config-contract.${extension}`;
    assert.equal(await eslint.isPathIgnored(file), false);
    const resolved = await eslint.calculateConfigForFile(file);
    assert.ok(resolved);
    const typescript = ['ts', 'mts', 'cts', 'tsx'].includes(extension);
    assert.equal(resolved.rules[typescript ? '@typescript-eslint/no-unused-vars' : 'no-unreachable'][0], 2);
  });
}

test('root config adds no file exclusions', () => {
  assert.equal(config.some((entry) => Object.hasOwn(entry, 'ignores')), false);
});

test('root config reports undefined JavaScript identifiers', async () => {
  const [result] = await eslint.lintText('missingRootConfigValue();', {
    filePath: 'scripts/ci/root-config-contract.mjs',
  });
  assert.deepEqual(result.messages.map((message) => message.ruleId), ['no-undef']);
});

test('root config parses TypeScript and reports unused bindings with its typed rule', async () => {
  const [result] = await eslint.lintText('const unusedRootConfigValue: number = 1;', {
    filePath: 'scripts/ci/root-config-contract.ts',
  });
  assert.deepEqual(result.messages.map((message) => message.ruleId), ['@typescript-eslint/no-unused-vars']);
});

test('root config recognizes CommonJS module globals', async () => {
  const [result] = await eslint.lintText("const { join } = require('node:path'); module.exports = join('a', 'b');", {
    filePath: 'scripts/ci/root-config-contract.cjs',
  });
  assert.deepEqual(result.messages, []);
});

test('root config counts JSX component references as actual uses', async () => {
  const [result] = await eslint.lintText('const Component = () => null; export const output = <Component />;', {
    filePath: 'scripts/ci/root-config-contract.jsx',
  });
  assert.deepEqual(result.messages, []);
});

test('root config retains and verifies all three pnpm resolver priority limits', async () => {
  const file = 'apps/web/scripts/pnpm-process.mjs';
  const resolved = await eslint.calculateConfigForFile(file);
  for (const [rule, limit] of [['complexity', 4], ['max-statements', 10], ['max-lines-per-function', 50]]) {
    assert.equal(resolved.rules[rule][0], 2);
    assert.equal(resolved.rules[rule][1], limit);
  }
  const [result] = await eslint.lintText(await readFile(path.join(repositoryRoot, file), 'utf8'), { filePath: file });
  assert.equal(result.fatalErrorCount, 0);
  assert.deepEqual(result.messages.filter((message) => ['complexity', 'max-statements', 'max-lines-per-function'].includes(message.ruleId)), []);
});

test('root config recognizes the real shared Jest runtime helpers', async () => {
  for (const file of ['packages/tooling/jest/src/setup.js', 'packages/tooling/jest/src/test-utils.js']) {
    const [result] = await eslint.lintText(await readFile(path.join(repositoryRoot, file), 'utf8'), { filePath: file });
    assert.equal(result.fatalErrorCount, 0);
    assert.deepEqual(result.messages.filter((message) => message.ruleId === 'no-undef'), []);
  }
});

test('root config still reports unknown references inside Jest helpers', async () => {
  const file = 'packages/tooling/jest/src/root-config-control.js';
  const resolved = await eslint.calculateConfigForFile(file);
  assert.equal(resolved.rules['no-undef'][0], 2);
  const [result] = await eslint.lintText('jest.fn(); beforeAll(() => {}); missingJestHelper();', { filePath: file });
  assert.equal(result.fatalErrorCount, 0);
  assert.deepEqual(result.messages.map((message) => message.ruleId), ['no-undef']);
  assert.match(result.messages[0].message, /missingJestHelper/u);
});

test('root config does not expose Jest runtime globals to ordinary scripts', async () => {
  const [result] = await eslint.lintText('jest.fn();', { filePath: 'scripts/ci/ordinary-helper.mjs' });
  assert.equal(result.fatalErrorCount, 0);
  assert.deepEqual(result.messages.map((message) => message.ruleId), ['no-undef']);
});
