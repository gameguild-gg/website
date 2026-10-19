import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';
import provenance from '../legacy-eslint/tool-output-provenance.cjs';
import loaderProvenance from './loader-provenance.cjs';

const engineRequire = createRequire('/app/package.json');
const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
const output = path.resolve(process.argv[2]);
mkdirSync(output, { recursive: true });
const write = (name, value) => writeFileSync(path.join(output, name), `${JSON.stringify(value, null, 2)}\n`);
const append = (name, value) => appendFileSync(path.join(output, name), `${JSON.stringify(value)}\n`);
const expected = { eslint: '9.39.5', typescript: '6.0.3', '@typescript-eslint/parser': '8.64.0' };
const root = '/src';
const report = {
  sourceHead: process.env.GAMEGUILD_CODACY_SOURCE_HEAD,
  scope: 'Tracked JavaScript and TypeScript inputs with the repository root flat configuration. This is additional source coverage, not legacy-rule parity, cloud closure, or CSharp/Opengrep verification.',
  selected: 0, attempted: 0, processed: 0, engineErrors: 0, fatalParsing: 0,
  configurationErrors: 0, diagnostics: 0, allFilesProcessed: false,
  completeSourceAnalysis: false, cleanAnalysis: false, certifiedDestinationClosures: 0,
};
write('summary.json', report);
let stage = 'loader-warning-transparency';

try {
  report.warningOutput = loaderProvenance.assertOriginalWarningOutput();
  provenance.installToolOutputProvenance(output);
  const observedStderrWrite = process.stderr.write;
  stage = 'manifest';
  const manifest = readFileSync(path.join(output, 'tracked-files.nul'));
  const tracked = manifest.toString('utf8').split('\0').filter(Boolean);
  assert.equal(tracked.length, new Set(tracked).size, 'Duplicate tracked source');
  for (const file of tracked) {
    assert(!path.posix.isAbsolute(file) && !file.includes('\\') && !file.split('/').includes('..'));
  }
  const selected = tracked.filter((file) => /\.(?:[cm]?[jt]s|[jt]sx)$/u.test(file));
  assert(selected.length > 0);
  const selectedSet = new Set(selected);
  stage = 'original-priority-scope';
  const baselineBytes = readFileSync(path.join(root, '.codacy/modern-eslint/priority-scope.json'));
  const baseline = JSON.parse(baselineBytes);
  assert.equal(baseline.issues.length, 241);
  assert.equal(new Set(baseline.issues.map((issue) => issue.issueId)).size, 241);
  const priority = baseline.issues.filter((issue) => issue.tool === 'ESLint');
  assert.equal(priority.length, 204);
  for (const issue of priority) assert(selectedSet.has(issue.file), 'Original ESLint priority input omitted');

  stage = 'engine-identity';
  const identities = Object.fromEntries(Object.entries(expected).map(([name, version]) => {
    const filename = engineRequire.resolve(`${name}/package.json`);
    const bytes = readFileSync(filename);
    const actual = JSON.parse(bytes).version;
    assert.equal(actual, version, `${name}: immutable engine version drift`);
    return [name, { version: actual, filename, sha256: digest(bytes) }];
  }));
  assert(process.execArgv.includes('--import=/app/node_modules/tsx/dist/loader.mjs'));
  const loaderBytes = readFileSync(engineRequire.resolve('tsx/package.json'));
  assert.equal(JSON.parse(loaderBytes).version, '4.23.1');
  report.loader = { name: 'tsx', version: '4.23.1', packageSha256: digest(loaderBytes) };
  const factoryPath = '/app/workspaces/codacy/src/configCreator.ts';
  const factoryHash = digest(readFileSync(factoryPath));
  stage = 'native-configuration-factory';
  assert.equal(factoryHash, '75b27a0bf89655b6dfc9c94b3d9c1936862530b3196f9de78d57f64609f15858');
  const { createEslintConfig } = await import(factoryPath);
  const { ESLint } = engineRequire('eslint');
  const [options, files] = await createEslintConfig(root, {
    files: selected, tools: [{ name: 'eslint-9', patterns: [] }], options: {},
  });
  assert.deepEqual(files, selected, 'Cloud wrapper changed explicit tracked inputs');
  assert.equal(options.overrideConfigFile, '/src/eslint.config.mjs');
  const native = new ESLint({ ...options, cwd: root });
  // Observe native exclusions separately. The coverage pass force-includes
  // tracked source and disallows inline suppression without changing the tool.
  const eslint = new ESLint({ ...options, cwd: root, ignore: false,
    allowInlineConfig: false, warnIgnored: true, errorOnUnmatchedPattern: true });
  stage = 'catalog-parity';
  const catalogBytes = readFileSync('/docs/patterns.json');
  const catalog = JSON.parse(catalogBytes);
  assert.equal(catalog.name, 'eslint-9');
  assert.equal(catalog.version, expected.eslint);
  const catalogIds = new Set(catalog.patterns.map((row) => row.patternId));
  const securityIds = new Set(catalog.patterns.filter((row) => row.category === 'Security')
    .map((row) => row.patternId));
  const legacyBytes = readFileSync(path.join(root, '.codacy/legacy-eslint/effective-patterns.json'));
  const legacy = JSON.parse(legacyBytes);
  const legacyParity = legacy.patterns.map((row) => {
    const patternId = row.patternId.replace(/^ESLint8_/u, '');
    return { originalPatternId: row.patternId, modernCatalogHasSameEncodedId: catalogIds.has(patternId) };
  });
  write('catalog-parity.json', {
    scope: 'Encoded rule ID availability only. Options, semantics, applicability and actual activation require separate review; no rule is disabled or declared equivalent.',
    modernCatalogSha256: digest(catalogBytes), legacyProfileSha256: digest(legacyBytes),
    originalEnabledRules: legacyParity.length, rules: legacyParity,
  });
  Object.assign(report, {
    manifestSha256: digest(manifest), tracked: tracked.length, selected: selected.length,
    identities, factorySha256: factoryHash,
    configSha256: digest(readFileSync('/src/eslint.config.mjs')),
    baselineSha256: digest(baselineBytes), originalPriorityIdsRetained: baseline.issues.length,
    originalEslintPriorityIds: priority.length, rootConfigurationSelectedByFactory: true,
    nativeIgnorePatterns: options.ignorePatterns, nativeIgnored: [],
    coveragePolicy: { ignore: false, allowInlineConfig: false }, securityCounts: {},
  });
  write('summary.json', report);
  const profiles = new Set();
  const priorityByFile = Map.groupBy(priority, (issue) => issue.file);
  stage = 'source-analysis';
  for (const file of selected) {
    const bytes = readFileSync(path.join(root, file));
    append('source-inputs.jsonl', { file, sha256: digest(bytes), bytes: bytes.length });
    append('progress.jsonl', { phase: 'before', file });
    report.attempted += 1;
    try {
      if (await native.isPathIgnored(file)) report.nativeIgnored.push(file);
      assert.equal(await eslint.isPathIgnored(file), false, 'Coverage pass ignored tracked source');
      const config = await eslint.calculateConfigForFile(file);
      assert(config && Object.keys(config.rules).length > 0, 'No effective rules for selected source');
      const rules = Object.fromEntries(Object.entries(config.rules).sort(([a], [b]) => a.localeCompare(b)));
      const profile = digest(JSON.stringify(rules));
      if (!profiles.has(profile)) {
        profiles.add(profile);
        append('effective-configurations.jsonl', { profile, rules,
          parser: config.languageOptions.parser.meta, sourceType: config.languageOptions.sourceType });
      }
      const enabled = new Set(Object.entries(rules).filter(([, value]) => value[0] > 0)
        .map(([name]) => name.replaceAll('_', '__').replaceAll('/', '_')));
      for (const issue of priorityByFile.get(file) || []) {
        append('original-priority-coverage.jsonl', { ...issue, profile,
          sameEncodedRuleEnabled: enabled.has(issue.patternId.replace(/^ESLint8_/u, '')),
          resolutionCertified: false });
      }
      const [result] = await eslint.lintText(bytes.toString('utf8'), { filePath: file, warnIgnored: true });
      assert(result, 'No lint result for selected source');
      for (const message of result.messages) {
        if (message.fatal) report.fatalParsing += 1;
        if (!message.ruleId && !message.fatal) report.configurationErrors += 1;
        if (message.ruleId && (securityIds.has(message.ruleId.replaceAll('_', '__').replaceAll('/', '_'))
          || /^(?:security(?:-node)?|no-unsanitized|xss)\//u.test(message.ruleId))) {
          report.securityCounts[message.ruleId] = (report.securityCounts[message.ruleId] || 0) + 1;
          continue;
        }
        // Public evidence never contains messages, snippets, suggested fixes,
        // source values or raw exception text.
        append('diagnostics.jsonl', { file, profile, ruleId: message.ruleId,
          severity: message.severity, fatal: message.fatal === true,
          line: message.line ?? null, column: message.column ?? null });
      }
      report.diagnostics += result.messages.length;
      report.processed += 1;
      append('progress.jsonl', { phase: 'after', file, diagnostics: result.messages.length });
    } catch (error) {
      report.engineErrors += 1;
      append('engine-errors.jsonl', { file,
        kind: ['TypeError', 'SyntaxError', 'RangeError', 'AssertionError'].includes(error?.name) ? error.name : 'Error',
        dependencyFrames: provenance.dependencyFrames(error?.stack) });
    }
    write('summary.json', report);
  }
  report.warningOutputAfterAnalysis = loaderProvenance.assertWarningEmissionUnchanged();
  assert.equal(process.stderr.write, observedStderrWrite, 'The observed stderr writer changed during analysis');
  report.allFilesProcessed = report.processed === selected.length && report.attempted === selected.length;
  report.completeSourceAnalysis = report.allFilesProcessed && report.engineErrors === 0
    && report.fatalParsing === 0 && report.configurationErrors === 0;
  report.cleanAnalysis = report.completeSourceAnalysis && report.diagnostics === 0;
} catch (error) {
  report.initializationError = { stage,
    kind: ['TypeError', 'SyntaxError', 'AssertionError'].includes(error?.name) ? error.name : 'Error',
    dependencyFrames: provenance.dependencyFrames(error?.stack) };
}
write('summary.json', report);
if (!report.completeSourceAnalysis) process.exitCode = 1;
