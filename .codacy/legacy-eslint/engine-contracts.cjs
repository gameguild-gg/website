'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const { tmpdir } = require('node:os');
const path = require('node:path');
const { after, test } = require('node:test');
const {
  decodeParameter, projectInput, createProjection, createRepositoryProfile, selectTrackedFiles, readNodeTargets,
  inferParserSingleRun,
  trackedTypeScriptProject, writeTrackedTypeScriptProject,
  createSourceOverrideProgram,
} = require('./project-configuration.cjs');
const { publicDiagnostics, engineRule, engineFailure, partitionSources,
  sourceCosts, sourceSchedulingContext, schedulingContextSha256,
  analysisStatus, qualifiedAnalysisStatus } = require('./analyze-repository.cjs');
const { dependencyFrames, captureWriteStack, observeWrite, warningMetadata } = require('./tool-output-provenance.cjs');
const { repairs } = require('./apply-repairs.cjs');

// These contracts run only inside the pinned hosted engine. No repository
// dependency installation, cloud configuration change or issue upload is made.
const engineRequire = createRequire('/node_modules/package.json');
const { Linter } = engineRequire('eslint');
const repaired = process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED === 'true';
assert(['true', 'false'].includes(process.env.GAMEGUILD_ENGINE_EXPECT_REPAIRED));
const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));

function lint(ruleId, packageName, ruleName, source, typescript = false, jsx = false, ruleOptions = []) {
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire(packageName).rules[ruleName]);
  if (typescript) linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  return linter.verify(source, {
    env: { browser: true, node: true, commonjs: true, es2022: true },
    ...(typescript ? { parser: 'engine-typescript' } : {}),
    parserOptions: { ecmaVersion: 2022, sourceType: typescript ? 'script' : 'module',
      ...(jsx ? { ecmaFeatures: { jsx: true } } : {}) },
    rules: { [ruleId]: ['error', ...ruleOptions] },
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
let repositoryTypeProjectDirectory;
let repositoryTypeProject;
after(() => {
  if (repositoryTypeProjectDirectory) rmSync(repositoryTypeProjectDirectory, { recursive: true, force: true });
});
function projection() {
  projectionPromise ??= createProjection(snapshot,
    JSON.parse(readFileSync('/docs/patterns.json', 'utf8')), engineRequire);
  return projectionPromise;
}

function repositoryProfile(result) {
  assert(process.env.GAMEGUILD_CODACY_TRACKED_FILES, 'The hosted Git manifest is required');
  const selected = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
  if (!repositoryTypeProject) {
    repositoryTypeProjectDirectory = mkdtempSync(path.join(tmpdir(), 'codacy-tracked-project-'));
    repositoryTypeProject = writeTrackedTypeScriptProject(selected, repositoryTypeProjectDirectory);
  }
  return createRepositoryProfile(result, readNodeTargets(selected.packageManifestFiles), repositoryTypeProject.filename);
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

test('immutable diagnostic fixtures retain observed bytes and keep current sources selected', () => {
  const cases = JSON.parse(readFileSync(path.join(__dirname, 'crash-cases.json'), 'utf8'));
  const manifest = readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES);
  const selection = selectTrackedFiles(manifest);
  const trackedFiles = new Set(manifest.toString('utf8').split('\0').filter(Boolean));
  const fixtureFiles = new Set();
  for (const recorded of cases) {
    assert(selection.sourceFiles.includes(recorded.file),
      'A historical reproducer must not exclude its current production source');
    assert.match(recorded.fixtureFile, /^\.codacy\/legacy-eslint\/fixtures\/[^/]+\.txt$/u,
      'Each historical reproducer must use a separate immutable fixture');
    const inputFile = recorded.fixtureFile;
    assert(!fixtureFiles.has(inputFile), 'Historical fixture paths must be unique');
    fixtureFiles.add(inputFile);
    assert(trackedFiles.has(inputFile), 'The historical bytes must also be tracked');
    assert(!selection.sourceFiles.includes(inputFile),
      'Historical fixture copies cannot inflate current source coverage');
    assert.notEqual(inputFile, recorded.file);
    assert.match(recorded.observedAtHead, /^[a-f0-9]{40}$/u);
    const bytes = readFileSync(path.join('/src', inputFile));
    assert.equal(createHash('sha256').update(bytes).digest('hex'), recorded.sha256);
  }
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
  const bytes = readFileSync(path.join('/src', recorded.fixtureFile ?? recorded.file));
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

test('interface sorting preserves the diagnostic when a same-line comment produces overlapping edits', () => {
  const run = () => lint('typescript-sort-keys/interface', 'eslint-plugin-typescript-sort-keys', 'interface',
    'interface Contract {\n  z: string;\n  /* keep this documentation */ a: string;\n}', true);
  if (!repaired) assert.throws(run, /Fix objects must not be overlapped in a report/);
  else {
    const messages = run();
    assert.equal(messages.length, 1);
    assert.equal(messages[0].ruleId, 'typescript-sort-keys/interface');
    assert.equal(messages[0].fatal, undefined);
    assert.equal(messages[0].fix, undefined, 'An invalid edit is refused while the ordering issue is retained');
    assert.match(messages[0].message, /a.*z/);
  }
});

test('interface sorting retains sorted inputs and ordinary non-overlapping fixes', () => {
  const check = source => lint('typescript-sort-keys/interface', 'eslint-plugin-typescript-sort-keys', 'interface', source, true);
  assert.deepEqual(check('interface Contract {\n  a: string;\n  /* keep this documentation */ z: string;\n}'), []);
  const source = 'interface Contract {\n  z: string;\n  a: number;\n}';
  const messages = check(source);
  assert.equal(messages.length, 1);
  const fix = messages[0].fix;
  assert(fix, 'A valid ordering edit must remain available');
  const output = source.slice(0, fix.range[0]) + fix.text + source.slice(fix.range[1]);
  assert.deepEqual(check(output), []);
  assert.match(output, /a: number/);
  assert.match(output, /z: string/);
});

test('string-enum sorting retains diagnostics without overlapping comment edits and preserves safe fixes', () => {
  const check = source => lint('typescript-sort-keys/string-enum', 'eslint-plugin-typescript-sort-keys', 'string-enum', source, true);
  const run = () => check('enum Contract {\n  Z = "z",\n  /* keep this documentation */ A = "a",\n}');
  if (!repaired) assert.throws(run, /Fix objects must not be overlapped in a report/);
  else {
    const messages = run();
    assert.equal(messages.length, 1);
    assert.equal(messages[0].ruleId, 'typescript-sort-keys/string-enum');
    assert.equal(messages[0].fatal, undefined);
    assert.equal(messages[0].fix, undefined);
  }
  assert.deepEqual(check('enum Contract {\n  A = "a",\n  Z = "z",\n}'), []);
  const source = 'enum Contract {\n  Z = "z",\n  A = "a",\n}';
  const messages = check(source);
  assert.equal(messages.length, 1);
  const fix = messages[0].fix;
  assert(fix, 'A valid enum ordering edit must remain available');
  const output = source.slice(0, fix.range[0]) + fix.text + source.slice(fix.range[1]);
  assert.deepEqual(check(output), []);
  assert.match(output, /A = "a"/);
  assert.match(output, /Z = "z"/);
});

test('interface sorting preserves findings for the exact source observed in the hosted overlap failure', async () => {
  const ruleId = 'typescript-sort-keys/interface';
  const bytes = readFileSync(path.join(__dirname, 'fixtures/14-source-code-editor-view-sort-keys.tsx.txt'));
  assert.equal(createHash('sha256').update(bytes).digest('hex'), '7fca0228511879faf0cf1be6b996f2b74ce437072aec4044c9aa3b4781dc8922');
  const profile = repositoryProfile(await projection());
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire('eslint-plugin-typescript-sort-keys').rules.interface);
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  let messages;
  try {
    messages = linter.verify(bytes.toString('utf8'), {
      parser: 'engine-typescript', parserOptions: { ecmaVersion: 2022, sourceType: 'module', ecmaFeatures: { jsx: true } },
      settings: profile.baseConfig.settings, rules: { [ruleId]: profile.baseConfig.rules[ruleId] },
    }, { filename: 'observed-source-code-editor-view.tsx' });
  } catch (error) {
    assert.equal(repaired, false, 'The repaired engine must traverse the observed source without crashing');
    assert.match(error.message, /Fix objects must not be overlapped in a report/);
    return;
  }
  assert(messages.some(message => message.ruleId === ruleId), 'Ordering findings on the observed source must remain visible');
  assert(messages.every(message => message.fatal !== true));
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

test('byte-weighted partitions separate large sources and retain every source in manifest order', () => {
  const files = ['small-a.ts', 'large.ts', 'small-b.ts', 'medium.ts', 'empty.ts'];
  const sizes = new Map([['small-a.ts', 10], ['large.ts', 1000], ['small-b.ts', 10],
    ['medium.ts', 600], ['empty.ts', 0]]);
  const originalFiles = [...files];
  const originalSizes = [...sizes];
  const parts = [0, 1].map((index) => partitionSources(files, index, 2, sizes));
  assert.deepEqual(parts, [['large.ts'], ['small-a.ts', 'small-b.ts', 'medium.ts', 'empty.ts']]);
  assert.deepEqual([...parts.flat()].sort(), [...files].sort());
  assert.equal(new Set(parts.flat()).size, files.length);
  assert.deepEqual(files, originalFiles);
  assert.deepEqual([...sizes], originalSizes);
  assert.deepEqual(partitionSources(files, 0, 1, sizes), files);
});

test('equal-sized sources retain balanced deterministic traversal with every partition count', () => {
  const files = Array.from({ length: 3279 }, (value, index) => `source-${index}.ts`);
  for (const bytes of [0, 100]) {
    const sizes = new Map(files.map((file) => [file, bytes]));
    for (const count of [4, 8, 16]) {
      const parts = Array.from({ length: count }, (value, index) => partitionSources(files, index, count, sizes));
      assert.equal(parts.flat().length, files.length);
      assert.equal(new Set(parts.flat()).size, files.length);
      for (let index = 0; index < count; index += 1) {
        assert.deepEqual(parts[index], partitionSources(files, index, count));
      }
    }
  }
});

test('byte-weighted allocation rejects missing and invalid source sizes', () => {
  const files = ['first.ts', 'second.ts'];
  for (const sizes of [null, {}, new Map(), new Map([['first.ts', 10]])]) {
    assert.throws(() => partitionSources(files, 0, 2, sizes));
  }
  for (const value of [-1, 0.5, NaN, Infinity, '100']) {
    assert.throws(() => partitionSources(files, 0, 2, new Map([['first.ts', 10], ['second.ts', value]])));
  }
  assert.throws(() => partitionSources(files, 0, 2, new Map([['first.ts', 10], ['other.ts', 10]])));
});

function timingProfile(sources) {
  const engineContext = { 'engine-context': 'a'.repeat(64) };
  return { schemaVersion: 1, engineContext,
    engineContextSha256: schedulingContextSha256(engineContext), sources };
}

test('measured scheduling isolates small expensive sources without changing source coverage or order', () => {
  const files = ['small-a.ts', 'large.ts', 'small-b.ts', 'slow.ts', 'empty.ts'];
  const sizes = new Map([['small-a.ts', 10], ['large.ts', 1000], ['small-b.ts', 10],
    ['slow.ts', 1], ['empty.ts', 0]]);
  const weights = new Map([['small-a.ts', 1], ['large.ts', 200], ['small-b.ts', 1],
    ['slow.ts', 10000], ['empty.ts', 1]]);
  const before = JSON.stringify([files, [...sizes], [...weights]]);
  const parts = [0, 1].map((index) => partitionSources(files, index, 2, sizes, weights));
  assert.deepEqual(parts, [['slow.ts'], ['small-a.ts', 'large.ts', 'small-b.ts', 'empty.ts']]);
  assert.deepEqual([...parts.flat()].sort(), [...files].sort());
  assert.equal(new Set(parts.flat()).size, files.length);
  assert.deepEqual(partitionSources(files, 0, 1, sizes, weights), files);
  assert.equal(JSON.stringify([files, [...sizes], [...weights]]), before);
});

test('timing hints require exact source bytes and keep changed new empty and retired files explicit', () => {
  const profile = timingProfile([
    { file: 'stable.ts', bytes: 10, sha256: 'a'.repeat(64), milliseconds: 1000, samples: 2 },
    { file: 'changed.ts', bytes: 20, sha256: 'b'.repeat(64), milliseconds: 600, samples: 1 },
    { file: 'retired.ts', bytes: 99, sha256: 'c'.repeat(64), milliseconds: 99999, samples: 1 },
  ]);
  const current = [
    { file: 'stable.ts', bytes: 10, sha256: 'a'.repeat(64) },
    { file: 'changed.ts', bytes: 20, sha256: 'd'.repeat(64) },
    { file: 'new.ts', bytes: 5, sha256: 'e'.repeat(64) },
    { file: 'empty.ts', bytes: 0, sha256: 'f'.repeat(64) },
  ];
  const before = JSON.stringify([profile, current]);
  const costs = sourceCosts(profile, current, profile.engineContextSha256);
  assert.deepEqual([...costs.weights], [['stable.ts', 1000], ['changed.ts', 2000], ['new.ts', 500], ['empty.ts', 1]]);
  assert.deepEqual(costs.entries.map((row) => row.basis), ['measured-milliseconds',
    'estimated-milliseconds', 'estimated-milliseconds', 'estimated-milliseconds']);
  assert.equal(costs.receipt.profiledSourceFiles, 1);
  assert.equal(costs.receipt.estimatedSourceFiles, 3);
  assert.equal(costs.weights.has('retired.ts'), false);
  assert.equal(JSON.stringify([profile, current]), before);
  const wrongSize = sourceCosts(profile, [{ ...current[0], bytes: 11 }], profile.engineContextSha256);
  assert.equal(wrongSize.weights, undefined);
  assert.equal(wrongSize.receipt.profiledSourceFiles, 0);
});

test('invalid timing profiles and missing unsafe or nonpositive partition weights fail visibly', () => {
  const row = { file: 'source.ts', bytes: 10, sha256: 'a'.repeat(64), milliseconds: 100, samples: 1 };
  const profile = timingProfile([row]);
  const check = (candidate) => sourceCosts(candidate, [row], profile.engineContextSha256);
  for (const milliseconds of [0, -1, 0.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1, '100']) {
    assert.throws(() => check(timingProfile([{ ...row, milliseconds }])));
  }
  for (const invalid of [{ bytes: -1 }, { bytes: 0.5 }, { samples: 0 }, { sha256: 'invalid' },
    { file: '../source.ts' }, { file: '/source.ts' }, { file: 'C:\\source.ts' }]) {
    assert.throws(() => check(timingProfile([{ ...row, ...invalid }])));
  }
  assert.throws(() => check(timingProfile([row, row])));
  assert.throws(() => check({ ...profile, schemaVersion: 2 }));
  assert.throws(() => check({ ...profile, engineContextSha256: 'b'.repeat(64) }));
  assert.throws(() => sourceCosts(profile, [row, row], profile.engineContextSha256));
  const files = ['source.ts'];
  const sizes = new Map([['source.ts', 10]]);
  for (const weights of [null, {}, new Map(), new Map([['other.ts', 1]]),
    ...[0, -1, NaN, Infinity, 0.5, Number.MAX_SAFE_INTEGER + 1].map((cost) => new Map([['source.ts', cost]]))]) {
    assert.throws(() => partitionSources(files, 0, 1, sizes, weights));
  }
  assert.throws(() => partitionSources(files, 0, 1, undefined, new Map([['source.ts', 1]])));
});

test('engine context drift falls back to byte scheduling without claiming measured coverage', () => {
  const row = { file: 'source.ts', bytes: 10, sha256: 'a'.repeat(64), milliseconds: 100, samples: 1 };
  const profile = timingProfile([row]);
  for (const [current, context] of [[[row], 'b'.repeat(64)],
    [[{ ...row, sha256: 'c'.repeat(64) }], profile.engineContextSha256]]) {
    const costs = sourceCosts(profile, current, context);
    assert.equal(costs.weights, undefined);
    assert.equal(costs.receipt.strategy, 'largest-source-bytes-first-v1');
    assert.equal(costs.receipt.profiledSourceFiles, 0);
    assert.equal(costs.receipt.estimatedSourceFiles, 0);
    assert.equal(costs.receipt.byteFallbackSourceFiles, 1);
    assert.equal(costs.entries[0].basis, 'source-bytes');
    assert.equal(costs.entries[0].cost, 10);
  }
});

test('the checked-in hosted timing profile schedules every current tracked source exactly once', () => {
  const profile = JSON.parse(readFileSync(path.join(__dirname, 'source-cost-profile.json'), 'utf8'));
  const selection = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
  const sources = selection.sourceFiles.map((file) => {
    const raw = readFileSync(path.join('/src', file));
    return { file, bytes: raw.length, sha256: createHash('sha256').update(raw).digest('hex') };
  });
  const costs = sourceCosts(profile, sources, schedulingContextSha256(sourceSchedulingContext()));
  assert.equal(costs.entries.length, selection.sourceFiles.length);
  assert.equal(costs.receipt.profiledSourceFiles + costs.receipt.estimatedSourceFiles
    + costs.receipt.byteFallbackSourceFiles, selection.sourceFiles.length);
  const sizes = new Map(sources.map((row) => [row.file, row.bytes]));
  const parts = Array.from({ length: 8 }, (value, index) =>
    partitionSources(selection.sourceFiles, index, 8, sizes, costs.weights));
  assert.deepEqual([...parts.flat()].sort(), [...selection.sourceFiles].sort());
  assert.equal(new Set(parts.flat()).size, selection.sourceFiles.length);
  const positions = new Map(selection.sourceFiles.map((file, index) => [file, index]));
  for (const part of parts) {
    assert.deepEqual(part, [...part].sort((left, right) => positions.get(left) - positions.get(right)));
  }
});

test('typed JavaScript rules receive a real project and retain AST-node property diagnostics', async (context) => {
  const configBytes = readFileSync('/tsconfig.json');
  assert.equal(createHash('sha256').update(configBytes).digest('hex'),
    '33411198f0da1d2ea5b54f7323178fc13ff9d9cd476039eee87635c70682fcc4');
  assert.equal(JSON.parse(configBytes).compilerOptions.allowJs, true);
  const projected = await projection();
  const { ESLint } = engineRequire('eslint');
  const filename = '/src/tools/emception/scripts/tests/toolchain-provider-security.test.mjs';
  const ruleId = 'eslint-plugin/no-property-in-node';
  const previous = await new ESLint({ ...projected.options, ignore: false }).calculateConfigForFile(filename);
  const corrected = await new ESLint(repositoryProfile(projected)).calculateConfigForFile(filename);
  assert.equal(previous.parserOptions.project, false);
  assert.equal(corrected.parserOptions.project, repositoryTypeProject.filename);
  assert.notEqual(corrected.parserOptions.project, '/tsconfig.json',
    'The inherited wildcard project omitted five tracked files in the dotfolder');
  assert.deepEqual(corrected.rules[ruleId], previous.rules[ruleId]);
  const linter = new Linter();
  linter.defineRule(ruleId, engineRequire('eslint-plugin-eslint-plugin').rules['no-property-in-node']);
  linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
  const check = (source, filePath, parserOptions) => linter.verify(source, {
    parser: 'engine-typescript', parserOptions,
    rules: { [ruleId]: corrected.rules[ruleId] },
  }, { filename: filePath });
  const actual = readFileSync(filename, 'utf8');
  assert.throws(() => check(actual, filename, previous.parserOptions), /requires parserServices to be generated/u);
  assert.deepEqual(check(actual, filename, corrected.parserOptions), []);
  const parsed = engineRequire('@typescript-eslint/parser').parseForESLint(actual,
    { ...corrected.parserOptions, filePath: filename, loc: true, range: true, raw: true,
      tokens: true, comment: true, eslintVisitorKeys: true, eslintScopeManager: true });
  assert(parsed.services.program, 'The JavaScript project must provide a real TypeScript program');
  const selection = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
  const javascript = selection.sourceFiles.filter((file) => /\.(?:js|jsx|mjs|cjs|jsm)$/u.test(file));
  assert.deepEqual(javascript.filter((file) => !parsed.services.program.getSourceFile(`/src/${file}`)), [],
    'Every admitted JavaScript input must also belong to the type-checking program');

  const directory = mkdtempSync(path.join(tmpdir(), 'codacy-typed-js-contract-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  const ast = '/** @param {import("/node_modules/@types/estree").Node} node */\n'
    + 'export function hasValue(node) { return "value" in node; }';
  const ordinary = 'export function hasValue() { return "value" in { type: "custom", value: 1 }; }';
  writeFileSync(path.join(directory, 'ast.mjs'), ast);
  writeFileSync(path.join(directory, 'ordinary.mjs'), ordinary);
  writeFileSync(path.join(directory, 'tsconfig.json'), JSON.stringify({
    compilerOptions: { allowJs: true, checkJs: false, target: 'ESNext',
      module: 'ESNext', moduleResolution: 'Node', types: [], skipLibCheck: true },
    include: ['*.mjs'],
  }));
  const options = { ecmaVersion: 2022, sourceType: 'module',
    project: path.join(directory, 'tsconfig.json'), tsconfigRootDir: directory };
  const messages = check(ast, path.join(directory, 'ast.mjs'), options);
  assert.equal(messages.length, 1);
  assert.equal(messages[0].ruleId, ruleId);
  assert.equal(messages[0].messageId, 'in');
  assert.deepEqual(check(ordinary, path.join(directory, 'ordinary.mjs'), options), []);
});

test('tool-output provenance preserves writes and failures without retaining messages or source frames', () => {
  const events = [];
  const receiver = {};
  const callback = () => {};
  const message = 'private-fixture-value';
  const stack = 'Error: private-fixture-value\n'
    + '    at warn (/node_modules/example/lib/notice.cjs:12:3)\n'
    + '    at invalid (/node_modules/../private-fixture-value.js:2:3)\n'
    + '    at source (/src/private-fixture-value.mjs:4:5)';
  let calls = 0;
  const observed = observeWrite(function (chunk, encoding, completion) {
    assert.equal(this, receiver);
    assert.equal(chunk, message);
    assert.equal(encoding, 'utf8');
    assert.equal(completion, callback);
    calls += 1;
    return false;
  }, 'stderr', (event) => events.push(event), () => stack);
  assert.equal(observed.call(receiver, message, 'utf8', callback), false);
  assert.equal(calls, 1);
  assert.deepEqual(events, [{ event: 'write', stream: 'stderr', bytes: Buffer.byteLength(message),
    sha256: createHash('sha256').update(message).digest('hex'),
    dependencyFrames: [{ file: 'example/lib/notice.cjs', line: 12, column: 3 }] }]);
  const original = new Error('private-fixture-value');
  const failed = observeWrite(() => { throw original; }, 'stdout', (event) => events.push(event));
  assert.throws(() => failed(Buffer.from(message)), (error) => error === original);
  assert.throws(() => failed(null), (error) => error === original,
    'An invalid chunk must reach the original write without coercion or a replacement error');
  assert.equal(events.length, 1);
  const warning = warningMetadata({ name: 'DeprecationWarning', code: 'DEP0040',
    message, stack });
  assert.deepEqual(warning, { event: 'warning', kind: 'DeprecationWarning', code: 'DEP0040',
    dependencyFrames: [{ file: 'example/lib/notice.cjs', line: 12, column: 3 }] });
  assert.equal(warningMetadata({ name: message, code: message, stack }).code, null);
  assert(!JSON.stringify([events, warning]).includes(message));
  assert.equal(analysisStatus({ selected: 1, attempted: 1, processed: 1, allFilesProcessed: true,
    engineErrors: 0, configurationErrors: 0, fatalParsing: 0, diagnosticCount: 0 },
  events[0].bytes).completeAnalysisCertified, false, 'Observed output must not become silently accepted');
});

test('known type-service failures and root-node crashes retain safe, specific provenance', () => {
  const categories = new Map([['eslint-plugin/no-property-in-node', 'BestPractice'],
    ['es-x/no-set-prototype-union', 'Compatibility']]);
  const missing = engineFailure({ name: 'Error', ruleId: 'eslint-plugin/no-property-in-node',
    message: 'You have used a rule which requires parserServices to be generated. private-fixture-value',
    stack: 'Error: /node_modules/private-fixture-value.js:1:2\n'
      + '    at invalid (/node_modules/../private-fixture-value.js:1:2)\n'
      + '    at rule (/node_modules/eslint-plugin-eslint-plugin/lib/rules/no-property-in-node.js:92:17)\n'
      + '    at source (/src/private-fixture-value.mjs:1:2)',
  }, categories);
  assert.equal(missing.reason, 'missing-type-services');
  assert.deepEqual(missing.dependencyFrames, [{
    file: 'eslint-plugin-eslint-plugin/lib/rules/no-property-in-node.js', line: 92, column: 17,
  }]);
  assert(!JSON.stringify(missing).includes('private-fixture-value'));
  assert.equal(engineFailure({ name: 'TypeError', ruleId: 'es-x/no-set-prototype-union',
    message: "Cannot read properties of undefined (reading 'kind')" }, categories).reason, 'missing-node-kind');
});

test('immutable standard-source parsing uses single-run mode while custom extensions and JSON types remain supported', async (context) => {
  const { ESLint } = engineRequire('eslint');
  const eslint = new ESLint(repositoryProfile(await projection()));
  const standard = await eslint.calculateConfigForFile('/src/scripts/lint-budget.mjs');
  const typed = await eslint.calculateConfigForFile('/src/packages/infrastructure/client/src/index.ts');
  const vue = await eslint.calculateConfigForFile('/src/contracts/example.vue');
  const jsm = await eslint.calculateConfigForFile('/src/contracts/example.jsm');
  assert.deepEqual(standard.parserOptions.extraFileExtensions, []);
  assert.deepEqual(typed.parserOptions.extraFileExtensions, []);
  assert(vue.parserOptions.extraFileExtensions.includes('.vue'));
  assert(jsm.parserOptions.extraFileExtensions.includes('.jsm'));
  const previous = process.env.TSESTREE_SINGLE_RUN;
  try {
    process.env.TSESTREE_SINGLE_RUN = 'true';
    assert.equal(inferParserSingleRun(standard.parserOptions, engineRequire), true);
    assert.equal(inferParserSingleRun(typed.parserOptions, engineRequire), true);
    assert.equal(inferParserSingleRun(vue.parserOptions, engineRequire), false);
    assert.equal(inferParserSingleRun(jsm.parserOptions, engineRequire), false);
    const directory = mkdtempSync(path.join(tmpdir(), 'codacy-single-run-contract-'));
    context.after(() => rmSync(directory, { recursive: true, force: true }));
    const filename = path.join(directory, 'entry.ts');
    const source = 'import data from "./data.json"; export const value = data.count;';
    writeFileSync(filename, source);
    writeFileSync(path.join(directory, 'data.json'), '{"count":1}');
    writeFileSync(path.join(directory, 'tsconfig.json'), JSON.stringify({
      extends: '/tsconfig.json', compilerOptions: { module: 'ESNext', esModuleInterop: true },
      files: ['entry.ts', 'data.json'], include: [], exclude: [],
    }));
    const parsed = engineRequire('@typescript-eslint/parser').parseForESLint(source, {
      ...typed.parserOptions, project: path.join(directory, 'tsconfig.json'), filePath: filename,
      loc: true, range: true, raw: true, tokens: true, comment: true,
      eslintVisitorKeys: true, eslintScopeManager: true,
    });
    const program = parsed.services.program;
    assert(program.getSourceFile(path.join(directory, 'data.json')),
      'JSON module resolution must not depend on registering JSON as a source extension');
    const ast = program.getSourceFile(filename);
    const initializer = ast.statements[1].declarationList.declarations[0].initializer;
    const checker = program.getTypeChecker();
    assert.equal(checker.typeToString(checker.getTypeAtLocation(initializer)), 'number');
  } finally {
    if (previous === undefined) delete process.env.TSESTREE_SINGLE_RUN;
    else process.env.TSESTREE_SINGLE_RUN = previous;
  }
});

test('the explicit tracked type project includes dotfolders, build directories and same-basename JS and TS', (context) => {
  const directory = mkdtempSync(path.join(tmpdir(), 'codacy-explicit-project-contract-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  const files = ['same.ts', 'same.js', '.hidden/runtime.mjs', 'build/runtime.mjs'];
  for (const [index, file] of files.entries()) {
    const filename = path.join(directory, file);
    mkdirSync(path.dirname(filename), { recursive: true });
    writeFileSync(filename, `export const value = ${index};`);
  }
  const selection = selectTrackedFiles(Buffer.from(`${files.join('\0')}\0`));
  const spec = trackedTypeScriptProject(selection, directory);
  assert.deepEqual(spec.files, files.map((file) => path.join(directory, file)));
  assert.deepEqual(spec.include, []);
  assert.deepEqual(spec.exclude, []);
  const project = writeTrackedTypeScriptProject(selection, directory, directory);
  assert.equal(project.rootFileCount, files.length);
  assert.equal(createHash('sha256').update(readFileSync(project.filename)).digest('hex'), project.sha256);
  const previous = process.env.TSESTREE_SINGLE_RUN;
  try {
    process.env.TSESTREE_SINGLE_RUN = 'true';
    const filename = path.join(directory, '.hidden/runtime.mjs');
    const parsed = engineRequire('@typescript-eslint/parser').parseForESLint(readFileSync(filename, 'utf8'), {
      project: project.filename, extraFileExtensions: [], filePath: filename,
      ecmaVersion: 2022, sourceType: 'module', loc: true, range: true, raw: true,
      tokens: true, comment: true, eslintVisitorKeys: true, eslintScopeManager: true,
    });
    for (const file of files) assert(parsed.services.program.getSourceFile(path.join(directory, file)), file);
  } finally {
    if (previous === undefined) delete process.env.TSESTREE_SINGLE_RUN;
    else process.env.TSESTREE_SINGLE_RUN = previous;
  }
});

test('Vue maximum line length retains diagnostics for sources with large script token arrays', () => {
  const source = `export const values = [${'0,'.repeat(150_000)}0];`;
  assertReportedOrOriginalCrash(
    () => lint('vue/max-len', 'eslint-plugin-vue', 'max-len', source),
    /Maximum allowed is 80\./u,
    /Maximum call stack size exceeded/u,
  );
  assert.deepEqual(lint('vue/max-len', 'eslint-plugin-vue', 'max-len', 'export const values = [0];'), []);
});

test('Vue maximum line length retains comment diagnostics without spreading a large comment array', () => {
  const source = `${'// short\n'.repeat(300_000)}// ${'x'.repeat(90)}\nexport const value = 1;`;
  // The rule collects comments only when a comment option is enabled.
  const verify = (input) => lint('vue/max-len', 'eslint-plugin-vue', 'max-len', input,
    false, false, [{ comments: 80 }]);
  assertReportedOrOriginalCrash(
    () => verify(source),
    /comment length of 93\. Maximum allowed is 80\./u,
    /Maximum call stack size exceeded/u,
  );
  assert.deepEqual(verify('// short\nexport const value = 1;'), []);
});

test('Vue maximum line length preserves template and script ordering for large template token arrays', () => {
  const linter = new Linter();
  linter.defineParser('engine-vue', engineRequire('vue-eslint-parser'));
  linter.defineRule('vue/max-len', engineRequire('eslint-plugin-vue').rules['max-len']);
  const verify = (source) => linter.verify(source, {
    parser: 'engine-vue', parserOptions: { ecmaVersion: 2022, sourceType: 'module' },
    rules: { 'vue/max-len': 'error' },
  }, { filename: 'contract.vue' });
  const template = `<template><div>${'<b />'.repeat(150_000)}</div></template>`;
  const script = '<script>export default {};</script>';
  for (const source of [`${template}\n${script}`, `${script}\n${template}`]) {
    assertReportedOrOriginalCrash(() => verify(source), /Maximum allowed is 80\./u,
      /Maximum call stack size exceeded/u);
  }
  assert.deepEqual(verify('<template><div><b /></div></template>\n' + script), []);
});

test('output provenance accepts source-mapped dependency and bundled-engine frames without retaining source frames', () => {
  const stack = 'Error\n'
    + '    at dependency (/app/node_modules/example/src/warning.ts:4:8)\n'
    + '    at bundled (file:///app/lib/models/plugins.ts:12:6)\n'
    + '    at bundledConfig (/app/workspaces/codacy/src/configCreator.ts:20:3)\n'
    + '    at source (/src/apps/web/private-source.ts:1:2)\n'
    + '    at traversal (/app/lib/../private-source.ts:1:2)\n'
    + '    at moduleTraversal (/app/node_modules/example/../private-source.ts:1:2)';
  assert.deepEqual(dependencyFrames(stack), [
    { file: 'example/src/warning.ts', line: 4, column: 8 },
    { file: 'codacy-eslint9/lib/models/plugins.ts', line: 12, column: 6 },
    { file: 'codacy-eslint9/workspaces/codacy/src/configCreator.ts', line: 20, column: 3 },
  ]);
});

test('deeper output provenance restores the caller stack limit and bounds collection', () => {
  const original = Error.stackTraceLimit;
  try {
    Error.stackTraceLimit = 2;
    const recurse = (depth) => depth === 0 ? captureWriteStack() : recurse(depth - 1);
    const stack = recurse(60);
    assert.equal(Error.stackTraceLimit, 2);
    assert.equal(stack.split('\n').filter((line) => line.startsWith('    at ')).length, 40);
  } finally {
    Error.stackTraceLimit = original;
  }
});

test('historical lint text and compiler AST retain matching bytes and typed dependencies', () => {
  const directory = mkdtempSync(path.join(tmpdir(), 'diagnostic-program-'));
  try {
    const filename = path.join(directory, 'source.ts');
    const dependency = path.join(directory, 'dependency.ts');
    const project = path.join(directory, 'tsconfig.json');
    const current = 'export const current = 1;\n';
    const historical = 'import { value } from "./dependency";\nexport const historical = value;\n';
    writeFileSync(filename, current);
    writeFileSync(dependency, 'export const value: number = 42;\n');
    writeFileSync(project, JSON.stringify({ files: [filename, dependency], compilerOptions: { strict: true, types: [] } }));
    const result = createSourceOverrideProgram(engineRequire, project,
      new Map([[filename, Buffer.from(historical)]]));
    assert.equal(result.program.getSourceFile(filename).getFullText(), historical);
    assert.equal(readFileSync(filename, 'utf8'), current);
    assert.equal(result.program.getSourceFile(dependency).getFullText(), readFileSync(dependency, 'utf8'));
    assert.equal(result.rootFileCount, 2);
    assert.deepEqual(result.inputs, [{ file: filename, bytes: Buffer.byteLength(historical),
      sha256: createHash('sha256').update(historical).digest('hex'),
      compilerSourceSha256: createHash('sha256').update(historical).digest('hex') }]);
    const options = result.program.getCompilerOptions();
    assert(options.strict && options.noEmit && options.noUnusedLocals && options.noUnusedParameters);
    const diagnostics = result.program.getSemanticDiagnostics();
    assert.equal(diagnostics.length, 0, JSON.stringify(diagnostics.map((diagnostic) => ({
      code: diagnostic.code, file: path.basename(diagnostic.file?.fileName ?? ''), message: diagnostic.messageText,
    }))));
    assert.throws(() => createSourceOverrideProgram(engineRequire, project,
      new Map([[path.join(directory, 'untracked.ts'), Buffer.from(historical)]])), { code: 'ERR_ASSERTION' });
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('historical import reports preserve diagnostics instead of using current-file locations', () => {
  const directory = mkdtempSync(path.join(tmpdir(), 'diagnostic-import-'));
  const previousSingleRun = process.env.TSESTREE_SINGLE_RUN;
  try {
    process.env.TSESTREE_SINGLE_RUN = 'true';
    const originalPath = path.join(directory, 'original.ts');
    const historicalPath = path.join(directory, 'historical.ts');
    const project = path.join(directory, 'tsconfig.json');
    const source = 'import { unused } from "./dependency";\nexport const value = 1;\n';
    const current = ' '.repeat(100) + source;
    for (const filename of [originalPath, historicalPath]) writeFileSync(filename, current);
    writeFileSync(project, JSON.stringify({ files: [originalPath, historicalPath] }));
    const verify = (filename, programs) => {
      const linter = new Linter();
      linter.defineParser('engine-typescript', engineRequire('@typescript-eslint/parser'));
      linter.defineRule('unused-imports/no-unused-imports',
        engineRequire('eslint-plugin-unused-imports').rules['no-unused-imports']);
      return linter.verify(source, {
        parser: 'engine-typescript',
        parserOptions: { ecmaVersion: 2022, sourceType: 'module', project,
          disallowAutomaticSingleRunInference: false, ...(programs ? { programs } : {}) },
        rules: { 'unused-imports/no-unused-imports': 'error' },
      }, { filename });
    };
    assert.equal(process.env.TSESTREE_SINGLE_RUN, 'true');
    assert.throws(() => verify(originalPath), /Column number out of range/u);
    const diagnostic = createSourceOverrideProgram(engineRequire, project,
      new Map([[historicalPath, Buffer.from(source)]]));
    const messages = verify(historicalPath, [diagnostic.program]);
    assert.equal(messages.length, 1, JSON.stringify(messages));
    assert.equal(messages[0].ruleId, 'unused-imports/no-unused-imports');
    assert.equal(messages[0].fatal, undefined);
    assert.match(messages[0].message, /unused.*never used/u);
    assert.deepEqual(messages[0].fix.range, [0, source.indexOf('export')]);
    assert.equal(readFileSync(historicalPath, 'utf8'), current);
  } finally {
    if (previousSingleRun === undefined) delete process.env.TSESTREE_SINGLE_RUN;
    else process.env.TSESTREE_SINGLE_RUN = previousSingleRun;
    rmSync(directory, { recursive: true, force: true });
  }
});

test('UTF-8 repair hashes preserve every untouched engine byte', () => {
  for (const repair of repairs) {
    const directory = path.join('/node_modules', repair.package);
    assert.equal(JSON.parse(readFileSync(path.join(directory, 'package.json'), 'utf8')).version, repair.version);
    let bytes = readFileSync(path.join(directory, repair.file));
    assert.equal(createHash('sha256').update(bytes).digest('hex'),
      repaired ? repair.repairedSha256 : repair.sha256, repair.package);
    if (!repaired) {
      // Perform an independent byte replacement, preserving non-ASCII comments
      // and every other byte outside the reviewed replacement literals.
      for (const [before, after] of repair.replacements) {
        const needle = Buffer.from(before, 'utf8');
        const offset = bytes.indexOf(needle);
        assert(offset >= 0, repair.package);
        assert.equal(bytes.indexOf(needle, offset + needle.length), -1, repair.package);
        bytes = Buffer.concat([bytes.subarray(0, offset), Buffer.from(after, 'utf8'),
          bytes.subarray(offset + needle.length)]);
      }
      assert.equal(createHash('sha256').update(bytes).digest('hex'), repair.repairedSha256, repair.package);
    }
  }
});
