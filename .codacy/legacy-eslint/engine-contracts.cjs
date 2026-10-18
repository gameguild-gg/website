'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const path = require('node:path');
const { test } = require('node:test');
const { decodeParameter, projectInput, createProjection } = require('./project-configuration.cjs');

// These contracts run only inside the pinned hosted engine. No repository
// dependency installation, cloud configuration change or issue upload is made.
const engineRequire = createRequire('/node_modules/package.json');
const { Linter } = engineRequire('eslint');
const repaired = process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED === 'true';
assert(['true', 'false'].includes(process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED));
const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));

function lint(ruleId, packageName, ruleName, source, typescript = false) {
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire(packageName).rules[ruleName]);
  if (typescript) linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  return linter.verify(source, {
    env: { browser: true, node: true, commonjs: true, es2022: true },
    ...(typescript ? { parser: 'engine-typescript' } : {}),
    parserOptions: { ecmaVersion: 2022, sourceType: typescript ? 'script' : 'module' },
    rules: { [ruleId]: 'error' },
  }, { filename: typescript ? 'contract.ts' : 'contract.mjs' });
}

function assertReportedOrOriginalCrash(run, message, originalCrash) {
  if (!repaired) return assert.throws(run, originalCrash);
  const results = run();
  assert.equal(results.length, 1, JSON.stringify(results));
  assert.equal(results[0].fatal, undefined);
  assert.match(results[0].message, message);
}

test('namespace rule preserves a diagnostic with TypeScript globals that have no definition', () => {
  assertReportedOrOriginalCrash(
    () => lint('import/no-namespace', 'eslint-plugin-import', 'no-namespace',
      'import * as service from "contract-service";\nexport const result = service.run();', true),
    /Unexpected namespace import/,
    /Cannot read properties of undefined \(reading 'node'\)/,
  );
});

test('namespace rule accepts named imports', () => {
  assert.deepEqual(lint('import/no-namespace', 'eslint-plugin-import', 'no-namespace',
    'import { run } from "contract-service";\nexport const result = run();', true), []);
});

test('mock rule preserves its original diagnostic instead of crashing on its message ID', () => {
  assertReportedOrOriginalCrash(
    () => lint('@shopify/jest/no-all-mocks-methods', '@shopify/eslint-plugin',
      'jest/no-all-mocks-methods', 'jest.clearAllMocks();'),
    /Do not use clearAllMocks/,
    /context\.report\(\) called with a messageId, but no messages were present in the rule metadata\./,
  );
});

test('mock rule accepts clearing an individual mock', () => {
  assert.deepEqual(lint('@shopify/jest/no-all-mocks-methods', '@shopify/eslint-plugin',
    'jest/no-all-mocks-methods', 'const mock = jest.fn();\nmock.mockClear();'), []);
});

test('async rule reports a finally block without a catch instead of crashing', () => {
  assertReportedOrOriginalCrash(
    () => lint('security-node/detect-unhandled-async-errors', 'eslint-plugin-security-node',
      'detect-unhandled-async-errors',
      'async function work() { try { await doWork(); } finally { cleanup(); } }'),
    /Unhandled errors detected in asynchronous function/,
    /Cannot read properties of null \(reading 'type'\)/,
  );
});

test('async rule accepts an actual catch handler', () => {
  assert.deepEqual(lint('security-node/detect-unhandled-async-errors', 'eslint-plugin-security-node',
    'detect-unhandled-async-errors',
    'async function work() { try { await doWork(); } catch (error) { report(error); } }'), []);
});

test('complete effective snapshot retains all enabled IDs and exact raw parameters', () => {
  assert.equal(snapshot.patterns.length, 2631);
  assert.equal(new Set(snapshot.patterns.map((row) => row.patternId)).size, 2631);
  assert(snapshot.patterns.every((row) => row.enabled === true));
  const encoded = JSON.stringify(snapshot.patterns.map(({ patternId, enabled, parameters }) =>
    ({ patternId, enabled, parameters })));
  assert.equal(createHash('sha256').update(encoded).digest('hex'), snapshot.patternIdentitySha256);
  assert.equal(snapshot.patterns.filter((row) => row.parameters.length > 0).length, 247);
});

test('all effective IDs exist in the pinned engine catalog with parameter definitions', () => {
  const catalog = JSON.parse(readFileSync('/docs/patterns.json', 'utf8'));
  const ids = new Map(catalog.patterns.map((row) => [`ESLint8_${row.patternId}`, row]));
  for (const row of snapshot.patterns) {
    const actual = ids.get(row.patternId);
    assert(actual, `Missing engine pattern: ${row.patternId}`);
    const names = new Set(actual.parameters.map((parameter) => parameter.name));
    for (const parameter of row.parameters) {
      assert(names.has(parameter.name), `${row.patternId}: unknown parameter ${parameter.name}`);
    }
  }
});

test('parameter projection preserves strings, false, zero, arrays and objects without coercion', () => {
  assert.equal(decodeParameter('false', true), false);
  assert.equal(decodeParameter('0', 4), 0);
  assert.equal(decodeParameter('', 'default'), '');
  assert.equal(decodeParameter('false', 'default'), 'false');
  assert.deepEqual(decodeParameter('["one","two"]', []), ['one', 'two']);
  assert.deepEqual(decodeParameter('{"enabled":false}', {}), { enabled: false });
  assert.throws(() => decodeParameter('"false"', true));
  assert.throws(() => decodeParameter('[]', {}));
  assert.throws(() => decodeParameter('1', undefined));

  const catalog = JSON.parse(readFileSync('/docs/patterns.json', 'utf8'));
  const input = projectInput(snapshot, catalog);
  assert.equal(input.tools[0].patterns.length, 2631);
  assert.equal(input.tools[0].patterns.reduce((count, row) => count + row.parameters.length, 0), 512);
  const complexity = input.tools[0].patterns.find((row) => row.patternId === 'complexity');
  assert.deepEqual(complexity.parameters, [{ name: 'unnamedParam', value: 4 }]);
  const dictionary = input.tools[0].patterns.find((row) => row.patternId === 'spellcheck_spell-checker');
  const values = Object.fromEntries(dictionary.parameters.map(({ name, value }) => [name, value]));
  assert.equal(values.comments, true);
  assert.equal(values.ignoreRequire, false);
  assert.equal(values.minLength, 1);
  assert.equal(values.lang, 'en_US');
  assert.deepEqual(values.skipWords, ['dict', 'aff', 'hunspellchecker', 'hunspell', 'utils',
    'Pnpm', 'Toolchain', 'gunzip', 'gzip', 'gzipped', 'mjs', 'mkdtemp', 'os', 'pnpm',
    'readdir', 'realpath', 'sep', 'tmpdir', 'tsc', 'unlink']);
});

let projectionPromise;
function projection() {
  projectionPromise ??= createProjection(snapshot,
    JSON.parse(readFileSync('/docs/patterns.json', 'utf8')), engineRequire);
  return projectionPromise;
}

test('the actual engine projects all 2631 requested rules with their exact effective options', async () => {
  const result = await projection();
  assert.equal(result.receipt.enabledPatterns, 2631);
  assert.equal(result.receipt.projectedRules, 2631);
  assert.equal(result.receipt.nonemptyParameterSets, 247);
  assert.equal(result.receipt.rawParameters, 512);
  assert.equal(result.receipt.allRuleIdsAndOptionsMatched, true);
  assert.equal(result.receipt.activeScannerChanged, false);
});

test('the complete projected profile loads for JavaScript and TypeScript in the actual engine', async () => {
  const result = await projection();
  const { ESLint } = engineRequire('eslint');
  const eslint = new ESLint(result.options);
  for (const filename of ['scripts/lint-budget.mjs', 'apps/web/src/lib/learning/grading-runtime-actions.ts']) {
    const config = await eslint.calculateConfigForFile(`/src/${filename}`);
    assert(config && Object.keys(config.rules).length > 0, `No effective configuration for ${filename}`);
    assert([2, 'error'].includes(config.rules.complexity[0]), 'Complexity must retain error severity');
    assert.deepEqual(config.rules.complexity.slice(1), [4]);
  }
});
