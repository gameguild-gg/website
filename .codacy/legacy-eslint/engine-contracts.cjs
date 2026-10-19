'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { mkdtempSync, readFileSync, rmSync, writeFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const { tmpdir } = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const {
  decodeParameter, projectInput, createProjection, createRepositoryProfile, selectTrackedFiles, readNodeTargets,
} = require('./project-configuration.cjs');
const { publicDiagnostics, engineRule, engineFailure, partitionSources,
  analysisStatus, qualifiedAnalysisStatus } = require('./analyze-repository.cjs');

// These contracts run only inside the pinned hosted engine. No repository
// dependency installation, cloud configuration change or issue upload is made.
const engineRequire = createRequire('/node_modules/package.json');
const { Linter } = engineRequire('eslint');
const repaired = process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED === 'true';
assert(['true', 'false'].includes(process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED));
const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));

function lint(ruleId, packageName, ruleName, source, typescript = false, jsx = false) {
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire(packageName).rules[ruleName]);
  if (typescript) linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  return linter.verify(source, {
    env: { browser: true, node: true, commonjs: true, es2022: true },
    ...(typescript ? { parser: 'engine-typescript' } : {}),
    parserOptions: { ecmaVersion: 2022, sourceType: typescript ? 'script' : 'module',
      ...(jsx ? { ecmaFeatures: { jsx: true } } : {}) },
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

function repositoryProfile(result) {
  assert(process.env.GAMEGUILD_CODACY_TRACKED_FILES, 'The hosted Git manifest is required');
  const selected = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
  return createRepositoryProfile(result, readNodeTargets(selected.packageManifestFiles));
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

test('the repository profile admits libraries and tests while preserving every rule and option', async () => {
  const result = await projection();
  const original = JSON.stringify(result.options);
  const options = repositoryProfile(result);
  assert.equal(JSON.stringify(result.options), original, 'Do not mutate the historical engine projection');
  assert.deepEqual(options.baseConfig.rules, result.options.baseConfig.rules);
  assert.deepEqual(options.baseConfig.overrides.slice(0, result.options.baseConfig.overrides.length),
    result.options.baseConfig.overrides);
  assert(options.baseConfig.overrides.slice(result.options.baseConfig.overrides.length)
    .every((override) => override.rules === undefined), 'Node target overrides must not change rules');
  assert.deepEqual(options.baseConfig.ignorePatterns, ['!**/*']);
  assert.equal(options.ignore, result.options.ignore, 'Retain the engine ignore mechanism');
  const { ESLint } = engineRequire('eslint');
  const inherited = new ESLint(result.options);
  const corrected = new ESLint(options);
  for (const filename of ['apps/web/src/lib/learning/grading-runtime-actions.ts',
    'scripts/ci/tests/eslint-root-config.test.mjs']) {
    assert.equal(await inherited.isPathIgnored(`/src/${filename}`), true,
      `The inherited exclusion must be demonstrated for ${filename}`);
    assert.equal(await corrected.isPathIgnored(`/src/${filename}`), false,
      `A repository source file must be admitted: ${filename}`);
  }
});

test('tracked-source selection covers all supported extensions without dropping directories', () => {
  const paths = ['src/main.js', 'scripts/main.mjs', 'scripts/main.cjs', 'ui/main.jsx',
    'src/main.ts', 'src/main.mts', 'src/main.cts', 'ui/main.tsx', 'vendor/main.jsm',
    'ui/main.vue', 'tests/example.test.mjs', 'examples/lib/runtime.js',
    '.codacy/legacy-eslint/engine-contracts.cjs', 'src/Space Name.js',
    'src/line\nbreak.js', 'README.md', 'Program.cs'];
  const selection = selectTrackedFiles(Buffer.from(`${paths.join('\0')}\0`));
  assert.deepEqual(selection.sourceFiles, paths.slice(0, -2));
  assert.deepEqual(selection.directoryOrFileExclusions, []);
  assert.equal(selection.trackedFileCount, 17);
  assert.equal(selection.sourceFileCount, 15);
  assert.throws(() => selectTrackedFiles(Buffer.from('../outside.js\0')));
  assert.throws(() => selectTrackedFiles(Buffer.from('/outside.js\0')));
  assert.throws(() => selectTrackedFiles(Buffer.from('C:/outside.js\0')));
  assert.throws(() => selectTrackedFiles(Buffer.from('src/main.js')));
  assert.throws(() => selectTrackedFiles(Buffer.from('src/main.js\0src/main.js\0')));
  assert.throws(() => selectTrackedFiles(Buffer.from([255, 0])));
});

test('the actual engine admits every supported source file in the complete Git manifest', async () => {
  assert(process.env.GAMEGUILD_CODACY_TRACKED_FILES, 'The hosted Git manifest is required');
  const selected = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
  const { ESLint } = engineRequire('eslint');
  const eslint = new ESLint(repositoryProfile(await projection()));
  for (const filename of selected.sourceFiles) {
    assert.equal(await eslint.isPathIgnored(`/src/${filename}`), false,
      `Tracked source must not disappear from analysis: ${filename}`);
  }
});

test('Node compatibility follows root and package engines while preserving unsupported-feature detection', async () => {
  const { ESLint } = engineRequire('eslint');
  const eslint = new ESLint(repositoryProfile(await projection()));
  const root = await eslint.calculateConfigForFile('/src/tools/emception/grading/browser-launch-options.test.mjs');
  const client = await eslint.calculateConfigForFile('/src/packages/infrastructure/client/src/index.ts');
  assert.equal(root.settings.node.version, '>=20.0.0');
  assert.equal(client.settings.node.version, '>=18.0.0');
  const ruleId = 'n/no-unsupported-features/node-builtins';
  const check = (source, settings) => {
    const linter = new Linter();
    linter.defineRule(ruleId, engineRequire('eslint-plugin-n').rules['no-unsupported-features/node-builtins']);
    return linter.verify(source, {
      env: { node: true, es2022: true },
      parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
      settings,
      rules: { [ruleId]: root.rules[ruleId] },
    }, { filename: '/src/tools/emception/grading/browser-launch-options.test.mjs' });
  };
  const nodeTest = 'import { test } from "node:test";\ntest("works", () => {});';
  const oldSettings = { ...root.settings, node: { ...root.settings.node, version: '>=16.0.0' } };
  assert(check(nodeTest, oldSettings).some((issue) => issue.ruleId === ruleId && /20\.0\.0/u.test(issue.message)));
  assert.deepEqual(check(nodeTest, root.settings), []);
  assert(check(nodeTest, client.settings).some((issue) => issue.ruleId === ruleId),
    'The independently supported Node18 client must retain its stricter compatibility check');
  assert(check('import { styleText } from "node:util";\nstyleText("bold", "text");', root.settings)
    .some((issue) => issue.ruleId === ruleId && /20\.12\.0/u.test(issue.message)),
  'A newer builtin must still fail for the declared Node20.0 minimum');
});

test('configuration comments cannot hide the requested code diagnostics', async () => {
  const options = repositoryProfile(await projection());
  assert.equal(options.baseConfig.noInlineConfig, true);
  const linter = new Linter();
  const messages = linter.verify('/* eslint-disable no-undef */\nmissingGlobal();', {
    noInlineConfig: options.baseConfig.noInlineConfig,
    parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
    rules: { 'no-undef': 'error' },
  });
  assert(messages.some((message) => message.ruleId === 'no-undef'));
});

test('public analysis evidence omits security locations and all message text', () => {
  const categories = new Map([['security-node/example', 'Security'], ['no-undef', 'ErrorProne']]);
  const sensitive = publicDiagnostics([{
    ruleId: 'security-node/example', severity: 2, line: 12, column: 4,
    message: 'private-fixture-value',
  }], 'src/private-fixture.ts', categories);
  assert.deepEqual(sensitive.diagnostics, []);
  assert.deepEqual(sensitive.securityCounts, { 'security-node/example': 1 });
  assert(!JSON.stringify(sensitive).includes('private-fixture'));
  const ordinary = publicDiagnostics([{
    ruleId: 'no-undef', severity: 2, line: 1, column: 1,
    message: 'private-fixture-value', source: 'private-fixture-value',
  }], 'src/public.ts', categories);
  assert.equal(ordinary.diagnostics[0].ruleId, 'no-undef');
  assert(!JSON.stringify(ordinary).includes('private-fixture-value'));
  assert.equal(engineRule({ ruleId: 'no-undef' }, categories), 'no-undef');
  assert.equal(engineRule({ message: 'private-fixture-value\nRule: "no-undef"' }, categories), 'no-undef');
  assert.equal(engineRule({ ruleId: 'private-fixture-value' }, categories), null);
});

test('complete analysis requires every file and preserves all failure states', () => {
  const clean = { selected: 2, attempted: 2, processed: 2, allFilesProcessed: true,
    engineErrors: 0, configurationErrors: 0, fatalParsing: 0, diagnosticCount: 0 };
  assert.deepEqual(analysisStatus(clean, 0), { completeAnalysisCertified: true, analysisGatePassed: true });
  for (const field of ['engineErrors', 'configurationErrors', 'fatalParsing']) {
    assert.equal(analysisStatus({ ...clean, [field]: 1 }, 0).analysisGatePassed, false);
  }
  assert.equal(analysisStatus({ ...clean, processed: 1 }, 0).completeAnalysisCertified, false);
  assert.equal(analysisStatus({ ...clean, selected: 0 }, 0).completeAnalysisCertified, false);
  assert.equal(analysisStatus(clean, 1).completeAnalysisCertified, false);
  assert.deepEqual(analysisStatus({ ...clean, diagnosticCount: 1 }, 0),
    { completeAnalysisCertified: true, analysisGatePassed: false });
});

test('class-property preference accepts a declared constructor and retains real assignment detection', () => {
  const rule = '@shopify/prefer-class-properties';
  const run = () => lint(rule, '@shopify/eslint-plugin', 'prefer-class-properties',
    'declare class Contract { constructor(); }', true);
  if (repaired) assert.deepEqual(run(), []);
  else assert.throws(run, /Cannot read properties of (?:null|undefined) \(reading 'body'\)/);
  const results = lint(rule, '@shopify/eslint-plugin', 'prefer-class-properties',
    'class Contract { constructor() { this.value = 1; } }');
  assert.equal(results.length, 1);
  assert.match(results[0].message, /Unexpected assignment of literal instance member/);
});

test('raw-text detection handles a template without expressions and retains the Text boundary', () => {
  const run = (source) => lint('react-native/no-raw-text', 'eslint-plugin-react-native',
    'no-raw-text', source, false, true);
  assertReportedOrOriginalCrash(() => run('const view = <View>{`plain`}</View>;'),
    /cannot be used outside of a <Text> tag/,
    /Cannot read properties of undefined \(reading 'name'\)/);
  assert.deepEqual(run('const view = <Text>{`plain`}</Text>;'), []);
  const results = run('const view = <View>{`${label}`}</View>;');
  assert.equal(results.length, 1);
  assert.match(results[0].message, /TemplateLiteral: label/);
});

test('type-import style retains its diagnostic without an invalid namespace autofix', () => {
  const run = (source) => lint('flowtype/type-import-style', 'eslint-plugin-flowtype',
    'type-import-style', source, true);
  const source = 'import type * as Contract from "contract";';
  if (repaired) {
    const results = run(source);
    assert.equal(results.length, 1);
    assert.match(results[0].message, /Unexpected "import type"/);
    assert.equal(results[0].fix, undefined);
  } else assert.throws(() => run(source), /Cannot read properties of undefined \(reading 'name'\)/);
  assert.deepEqual(run('import { type Value } from "contract";'), []);
  const named = run('import type { Value } from "contract";');
  assert.equal(named.length, 1);
  assert.match(named[0].fix.text, /import \{type Value\}/);
});

test('engine failure metadata retains known causes without raw exception messages or source frames', () => {
  const categories = new Map([['flowtype/type-import-style', 'CodeStyle']]);
  const result = engineFailure({ name: 'TypeError', ruleId: 'flowtype/type-import-style',
    message: "Cannot read properties of undefined (reading 'name')\nprivate-fixture-value",
    stack: 'private-fixture-value\n at fix (/node_modules/eslint-plugin-flowtype/dist/rules/typeImportStyle.js:12:3)\n at /src/private-fixture-value.ts:3:2',
  }, categories);
  assert.equal(result.reason, 'missing-node-name');
  assert.equal(result.ruleId, 'flowtype/type-import-style');
  assert.deepEqual(result.dependencyFrames, [
    { file: 'eslint-plugin-flowtype/dist/rules/typeImportStyle.js', line: 12, column: 3 },
  ]);
  assert(!JSON.stringify(result).includes('private-fixture-value'));
  assert.equal(engineFailure({ message: 'private-fixture-value' }, categories).reason, 'unclassified-engine-error');
});

test('all partitions retain every source exactly once and cannot certify a full scan individually', () => {
  const files = Array.from({ length: 19 }, (value, index) => `source-${index}.ts`);
  const partitions = Array.from({ length: 4 }, (value, index) => partitionSources(files, index, 4));
  assert.equal(partitions.flat().length, files.length);
  assert.equal(new Set(partitions.flat()).size, files.length);
  assert.deepEqual([...partitions.flat()].sort(), [...files].sort());
  for (const args of [[-1, 4], [4, 4], [0, 0], [0, 20], [0.5, 4]]) {
    assert.throws(() => partitionSources(files, ...args));
  }
  const clean = { selected: 2, attempted: 2, processed: 2, allFilesProcessed: true,
    engineErrors: 0, configurationErrors: 0, fatalParsing: 0, diagnosticCount: 0,
    partition: { count: 4 } };
  assert.equal(qualifiedAnalysisStatus(clean, 0).partitionAnalysisComplete, true);
  assert.equal(qualifiedAnalysisStatus(clean, 0).completeAnalysisCertified, false);
  assert.equal(qualifiedAnalysisStatus({ ...clean, partition: { count: 1 } }, 0).completeAnalysisCertified, true);
  assert.equal(qualifiedAnalysisStatus({ ...clean, partition: { count: 1 }, diagnosticSample: true }, 0)
    .completeAnalysisCertified, false);
});

test('Lodash import lookup ignores inherited object names and retains real chain detection', () => {
  const run = (source) => lint('lodash-fp/no-chain', 'eslint-plugin-lodash-fp', 'no-chain', source);
  if (repaired) assert.deepEqual(run('toString();'), []);
  else assert.throws(() => run('toString();'), /info\.name\.replace is not a function/);
  assert.deepEqual(run('function custom() {}\ncustom();'), []);
  const messages = run('import _ from "lodash";\n_.chain([]);');
  assert.equal(messages.length, 1);
  assert.match(messages[0].message, /Unallowed use of chain operations/);
});

test('semantic-tag preference handles non-string roles and retains genuine accessibility diagnostics', () => {
  const run = (source) => lint('jsx-a11y/prefer-tag-over-role', 'eslint-plugin-jsx-a11y',
    'prefer-tag-over-role', source, false, true);
  if (repaired) assert.deepEqual(run('const view = <div role={1} />;'), []);
  else assert.throws(() => run('const view = <div role={1} />;'), /propValue\.lastIndexOf is not a function/);
  assert.deepEqual(run('const view = <button role="button" />;'), []);
  const messages = run('const view = <div role="button" />;');
  assert.equal(messages.length, 1);
  assert.equal(messages[0].message,
    'Use <input type="button">, <input type="image">, <input type="reset">, <input type="submit">, or <button> '
      + 'instead of the "button" role to ensure accessibility across all devices.');
  const invalid = lint('jsx-a11y/aria-role', 'eslint-plugin-jsx-a11y', 'aria-role',
    'const view = <div role="invented-role" />;', false, true);
  assert.equal(invalid.length, 1, 'The separate invalid-role diagnostic remains enabled and effective');
});

test('JSX prop sorting retains diagnostics without overlapping fixes and preserves safe fixes', async () => {
  const ruleId = 'perfectionist/sort-jsx-props';
  const profile = repositoryProfile(await projection());
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire('eslint-plugin-perfectionist').rules['sort-jsx-props']);
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  const check = (source, filename = 'contract.tsx') => linter.verify(source, {
    parser: 'engine-typescript', parserOptions: { ecmaVersion: 2022, sourceType: 'module', ecmaFeatures: { jsx: true } },
    settings: profile.baseConfig.settings, rules: { [ruleId]: profile.baseConfig.rules[ruleId] },
  }, { filename });
  const cases = JSON.parse(readFileSync(path.join(__dirname, 'crash-cases.json'), 'utf8'));
  const recorded = cases.find((row) => row.ruleId === ruleId);
  const bytes = readFileSync(path.join('/src', recorded.file));
  assert.equal(createHash('sha256').update(bytes).digest('hex'), recorded.sha256);
  if (!repaired) assert.throws(() => check(bytes.toString('utf8'), recorded.file), /Fix objects must not be overlapped in a report/);
  else {
    const messages = check(bytes.toString('utf8'), recorded.file);
    assert(messages.some((message) => message.ruleId === ruleId && message.fix === undefined),
      'An unsafe overlapping autofix must not remove its ordering diagnostic');
    assert(messages.every((message) => message.fatal !== true));
  }
  assert.deepEqual(check('const view = <View a={1} z={0} />;'), []);
  const safe = check('const view = <View z={0} a={1} />;');
  assert.equal(safe.length, 1);
  assert(safe[0].fix, 'A normal non-overlapping ordering fix remains available');
});

test('engine module origins are pinned while real cycles and TypeScript alias namespaces remain detectable', async (context) => {
  const projected = await projection();
  const profile = repositoryProfile(projected);
  const modules = projected.engineModules;
  assert.equal(profile.resolvePluginsRelativeTo, '/');
  assert.equal(profile.baseConfig.parser, modules['@typescript-eslint/parser'].entry);
  assert.equal(modules['@typescript-eslint/parser'].version, '7.18.0');
  assert.equal(modules['eslint-import-resolver-typescript'].version, '3.6.1');
  assert.equal(modules['eslint-import-resolver-node'].version, '0.3.9');
  assert.deepEqual(Object.keys(profile.baseConfig.settings['import/resolver']).sort(),
    [modules['eslint-import-resolver-node'].entry, modules['eslint-import-resolver-typescript'].entry].sort());
  const { ESLint } = engineRequire('eslint');
  const engine = new ESLint(profile);
  const vue = await engine.calculateConfigForFile('/src/contracts/example.vue');
  assert.equal(vue.parser, modules['vue-eslint-parser'].entry);
  assert.equal(vue.parserOptions.parser, modules['@typescript-eslint/parser'].entry);
  const directory = mkdtempSync(path.join(tmpdir(), 'codacy-import-contract-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  const a = 'import { valueB } from "./b.mjs"; export const valueA = valueB;';
  const b = 'import { valueA } from "./a.mjs"; export const valueB = valueA;';
  writeFileSync(path.join(directory, 'a.mjs'), a);
  writeFileSync(path.join(directory, 'b.mjs'), b);
  writeFileSync(path.join(directory, 'exports.ts'), 'export const present = 1;');
  writeFileSync(path.join(directory, 'tsconfig.json'), JSON.stringify({
    compilerOptions: { baseUrl: '.', paths: { '@fixture/*': ['./*'] } },
  }));
  const settings = { ...profile.baseConfig.settings,
    'import/resolver': Object.fromEntries(Object.entries(profile.baseConfig.settings['import/resolver'])
      .map(([entry, options]) => [entry, entry === modules['eslint-import-resolver-typescript'].entry
        ? { ...options, project: path.join(directory, 'tsconfig.json') } : options])),
  };
  const linter = new Linter();
  for (const rule of ['namespace', 'no-cycle']) {
    linter.defineRule(`import/${rule}`, engineRequire('eslint-plugin-import').rules[rule]);
  }
  const parserPath = modules['@typescript-eslint/parser'].entry;
  linter.defineParser(parserPath, engineRequire(parserPath));
  const check = (source, filename, ruleId) => linter.verify(source, {
    parser: parserPath, parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
    settings, rules: { [ruleId]: 'error' },
  }, { filename: path.join(directory, filename) });
  assert(check(a, 'a.mjs', 'import/no-cycle').some((message) => message.ruleId === 'import/no-cycle'));
  const missing = check('import * as api from "@fixture/exports"; api.missing();', 'entry.ts', 'import/namespace');
  assert(missing.some((message) => message.ruleId === 'import/namespace' && /not found/u.test(message.message)));
  assert.deepEqual(check('import * as api from "@fixture/exports"; api.present;', 'entry.ts', 'import/namespace'), []);
});

test('Flow annotation context accepts plain JavaScript and retains Flow errors and malformed-header checks', async () => {
  const profile = repositoryProfile(await projection());
  assert.equal(profile.baseConfig.settings.flowtype.onlyFilesWithFlowAnnotation, true);
  const linter = new Linter();
  const rules = ['require-parameter-type', 'require-valid-file-annotation'];
  for (const rule of rules) {
    linter.defineRule(`flowtype/${rule}`, engineRequire('eslint-plugin-flowtype').rules[rule]);
  }
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  const check = (source, annotatedOnly, filename = 'language-context.ts') => linter.verify(source, {
    parser: 'engine-typescript', parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
    settings: { ...profile.baseConfig.settings,
      flowtype: { ...profile.baseConfig.settings.flowtype, onlyFilesWithFlowAnnotation: annotatedOnly } },
    rules: Object.fromEntries(rules.map((rule) => [`flowtype/${rule}`, profile.baseConfig.rules[`flowtype/${rule}`]])),
  }, { filename });
  const plain = 'function useValue(value) { return value; }';
  assert(check(plain, false).some((message) => message.ruleId === 'flowtype/require-parameter-type'),
    'The prior unscoped setting must reproduce the inappropriate plain-JavaScript type requirement');
  assert.deepEqual(check(plain, true), []);
  const actualFilename = '/src/tools/emception/grading/browser-launch-options.mjs';
  const actualSource = readFileSync(actualFilename, 'utf8');
  assert(check(actualSource, false, actualFilename)
    .some((message) => message.ruleId === 'flowtype/require-parameter-type'));
  assert.deepEqual(check(actualSource, true, actualFilename), []);
  assert(check(`// @flow\n${plain}`, true)
    .some((message) => message.ruleId === 'flowtype/require-parameter-type'));
  assert.deepEqual(check('// @flow\nfunction useValue(value: string) { return value; }', true), []);
  assert.deepEqual(check(`// @noflow\n${plain}`, true), []);
  assert(check('// @flo\nfunction useValue(value: string) { return value; }', true)
    .some((message) => message.ruleId === 'flowtype/require-valid-file-annotation'),
  'A malformed Flow declaration must remain a diagnostic');
});

test('JSDoc option failures remain visible and block certification while source documentation errors stay distinct', async () => {
  const profile = repositoryProfile(await projection());
  const ruleIds = ['jsdoc/match-name', 'jsdoc/text-escaping', 'jsdoc/require-param'];
  const categories = new Map(ruleIds.map((ruleId) => [ruleId, 'Documentation']));
  const linter = new Linter();
  for (const ruleId of ruleIds) {
    linter.defineRule(ruleId, engineRequire('eslint-plugin-jsdoc').rules[ruleId.slice('jsdoc/'.length)]);
  }
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  const source = '/** Example. */\nfunction documented(value) { return value; }';
  const check = (rules) => linter.verify(source, {
    parser: 'engine-typescript', parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
    settings: profile.baseConfig.settings,
    rules: Object.fromEntries(rules.map((ruleId) => [ruleId, profile.baseConfig.rules[ruleId]])),
  }, { filename: 'documentation-context.ts' });
  const options = check(ruleIds.slice(0, 2));
  assert(options.some((message) => message.ruleId === 'jsdoc/match-name'));
  assert(options.some((message) => message.ruleId === 'jsdoc/text-escaping'));
  const invalid = publicDiagnostics(options, 'documentation-context.ts', categories);
  assert.equal(invalid.configurationErrors, options.length);
  assert.equal(invalid.diagnostics.length, options.length, 'Retain every invalid-option diagnostic');
  const missing = check(['jsdoc/require-param']);
  assert(missing.some((message) => /Missing JSDoc @param/u.test(message.message)));
  assert.equal(publicDiagnostics(missing, 'documentation-context.ts', categories).configurationErrors, 0);
  assert.equal(analysisStatus({ selected: 1, attempted: 1, processed: 1, allFilesProcessed: true,
    engineErrors: 0, configurationErrors: invalid.configurationErrors, fatalParsing: 0,
    diagnosticCount: options.length }, 0).completeAnalysisCertified, false);
});

test('class-property detection handles dynamic spreads and array holes without losing literal diagnostics', () => {
  const run = (source) => lint('@shopify/prefer-class-properties', '@shopify/eslint-plugin',
    'prefer-class-properties', source);
  const spread = 'class Contract { constructor(options) { this.options = { ...options }; } }';
  if (repaired) assert.deepEqual(run(spread), []);
  else assert.throws(() => run(spread), /Cannot read properties of undefined \(reading 'type'\)/);
  assertReportedOrOriginalCrash(
    () => run('class Contract { constructor() { this.items = [, 1]; } }'),
    /Unexpected assignment of literal instance member/,
    /Cannot read properties of null \(reading 'type'\)/,
  );
  assert.deepEqual(run('class Contract { constructor(options) { this.options = options; } }'), []);
  const literal = run('class Contract { constructor() { this.options = { value: null }; } }');
  assert.equal(literal.length, 1);
  assert.match(literal[0].message, /Unexpected assignment of literal instance member/);
});

test('typed ES-X detection handles module roots and retains real Set union diagnostics', (context) => {
  const directory = mkdtempSync(path.join(tmpdir(), 'codacy-es-x-contract-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  const sources = {
    'custom-source.ts': 'export function union(value: string) { return value; }',
    'custom-only.ts': 'import * as custom from "./custom-source"; custom.union("value");',
    'set-only.ts': 'const values = new Set<number>(); values.union(new Set<number>()); export {};',
    'mixed.ts': 'import * as custom from "./custom-source"; custom.union("value");'
      + ' const values = new Set<number>(); values.union(new Set<number>());',
  };
  for (const [filename, source] of Object.entries(sources)) {
    writeFileSync(path.join(directory, filename), source);
  }
  writeFileSync(path.join(directory, 'tsconfig.json'), JSON.stringify({
    compilerOptions: { target: 'ESNext', module: 'ESNext', lib: ['ESNext'],
      strict: true, types: [], skipLibCheck: true },
    include: ['*.ts'],
  }));
  const ruleId = 'es-x/no-set-prototype-union';
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire('eslint-plugin-es-x').rules['no-set-prototype-union']);
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  const check = (filename) => linter.verify(sources[filename], {
    parser: 'engine-typescript',
    parserOptions: { ecmaVersion: 2022, sourceType: 'module',
      project: path.join(directory, 'tsconfig.json'), tsconfigRootDir: directory },
    rules: { [ruleId]: ['error', { aggressive: false }] },
  }, { filename: path.join(directory, filename) });
  if (repaired) assert.deepEqual(check('custom-only.ts'), []);
  else assert.throws(() => check('custom-only.ts'), /Cannot read properties of undefined \(reading 'kind'\)/);
  const realSet = check('set-only.ts');
  assert.equal(realSet.length, 1);
  assert.equal(realSet[0].ruleId, ruleId);
  assert.equal(realSet[0].messageId, 'forbidden');
  assert.match(realSet[0].message, /Set\.prototype\.union/u);
  if (repaired) {
    const mixed = check('mixed.ts');
    assert.equal(mixed.length, 1);
    assert.equal(mixed[0].ruleId, ruleId);
    assert.equal(mixed[0].messageId, 'forbidden');
  } else assert.throws(() => check('mixed.ts'), /Cannot read properties of undefined \(reading 'kind'\)/);
});

test('expanded partition counts preserve complete source traversal and the global certification boundary', () => {
  const files = Array.from({ length: 3279 }, (value, index) => `source-${index}.ts`);
  for (const count of [4, 8, 16]) {
    const partitions = Array.from({ length: count }, (value, index) => partitionSources(files, index, count));
    assert.equal(partitions.flat().length, files.length);
    assert.equal(new Set(partitions.flat()).size, files.length);
    assert.deepEqual([...partitions.flat()].sort(), [...files].sort());
    assert(Math.max(...partitions.map((part) => part.length))
      - Math.min(...partitions.map((part) => part.length)) <= 1);
    for (const part of partitions) {
      const report = { selected: part.length, attempted: part.length, processed: part.length,
        allFilesProcessed: true, engineErrors: 0, configurationErrors: 0,
        fatalParsing: 0, diagnosticCount: 0, partition: { count } };
      assert.equal(qualifiedAnalysisStatus(report, 0).partitionAnalysisComplete, true);
      assert.equal(qualifiedAnalysisStatus(report, 0).completeAnalysisCertified, false);
    }
  }
});
