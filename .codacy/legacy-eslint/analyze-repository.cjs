'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { appendFileSync, mkdirSync, readFileSync, statSync, writeFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const path = require('node:path');
const { createProjection, createRepositoryProfile, readNodeTargets, selectTrackedFiles } = require('./project-configuration.cjs');

function publicDiagnostics(messages, filename, categories) {
  const diagnostics = [];
  const securityCounts = {};
  let configurationErrors = 0;
  for (const message of messages) {
    const known = message.ruleId === null || categories.has(message.ruleId);
    const ruleId = known ? message.ruleId : null;
    const category = ruleId === null ? 'ParserOrConfiguration' : categories.get(ruleId);
    const missingRule = typeof message.message === 'string'
      && message.message.startsWith('Definition for rule ');
    if (!known || missingRule) configurationErrors += 1;
    if (category === 'Security' || /^(?:security(?:-node)?|no-unsanitized|xss)\//u.test(ruleId || '')) {
      securityCounts[ruleId] = (securityCounts[ruleId] || 0) + 1;
      continue;
    }
    // Never retain message text, snippets, fixes or attacker-controlled values.
    diagnostics.push({
      file: filename,
      ruleId,
      category,
      severity: message.severity,
      fatal: message.fatal === true,
      line: message.line ?? null,
      column: message.column ?? null,
    });
  }
  return { diagnostics, securityCounts, configurationErrors };
}

function errorKind(error) {
  const kinds = ['Error', 'TypeError', 'RangeError', 'SyntaxError', 'ReferenceError', 'AssertionError'];
  return kinds.includes(error?.name) ? error.name : 'Error';
}

function engineRule(error, categories) {
  if (categories.has(error?.ruleId)) return error.ruleId;
  const matches = typeof error?.message === 'string'
    ? [...error.message.matchAll(/^Rule: ['"]([^'"]+)['"]$/gm)] : [];
  const id = matches.at(-1)?.[1];
  return categories.has(id) ? id : null;
}

function analysisStatus(report, toolOutputBytes) {
  const complete = Number.isSafeInteger(report.selected) && report.selected > 0
    && report.attempted === report.selected && report.processed === report.selected
    && report.allFilesProcessed === true && report.engineErrors === 0
    && report.configurationErrors === 0 && report.fatalParsing === 0
    && !report.initializationError && toolOutputBytes === 0;
  return { completeAnalysisCertified: complete, analysisGatePassed: complete && report.diagnosticCount === 0 };
}

function finalizeEvidence(output) {
  const filename = path.join(output, 'analysis-summary.json');
  const report = JSON.parse(readFileSync(filename, 'utf8'));
  assert.equal(report.head, process.env.GAMEGUILD_CODACY_SOURCE_HEAD, 'Evidence source differs');
  const bytes = statSync(path.join(output, 'disposable-tool-output.log')).size;
  Object.assign(report, analysisStatus(report, bytes), {
    unreviewedToolOutputBytes: bytes, toolOutputReviewPending: false,
  });
  writeFileSync(filename, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify({ head: report.head, selected: report.selected,
    attempted: report.attempted, processed: report.processed, engineErrors: report.engineErrors,
    configurationErrors: report.configurationErrors, fatalParsing: report.fatalParsing,
    diagnosticCount: report.diagnosticCount, toolOutputBytes: bytes,
    completeAnalysisCertified: report.completeAnalysisCertified, analysisGatePassed: report.analysisGatePassed })}\n`);
  if (!report.analysisGatePassed) process.exitCode = 1;
}

async function analyze(output) {
  const head = process.env.GAMEGUILD_CODACY_SOURCE_HEAD;
  assert.match(head || '', /^[a-f0-9]{40}$/u, 'An exact source head is required');
  assert(path.isAbsolute(output), 'An absolute evidence directory is required');
  mkdirSync(output, { recursive: true });
  const progress = path.join(output, 'file-progress.jsonl');
  const diagnostics = path.join(output, 'diagnostics.jsonl');
  writeFileSync(progress, '');
  writeFileSync(diagnostics, '');
  const report = {
    head,
    selected: 0,
    attempted: 0,
    processed: 0,
    engineErrors: 0,
    engineErrorsByRule: {},
    configurationErrors: 0,
    fatalParsing: 0,
    diagnosticCount: 0,
    countsByRule: {},
    securityCounts: {},
    allFilesProcessed: false,
    completeAnalysisCertified: false,
    activeScannerChanged: false,
    securityDetailsRetained: false,
    scope: 'Full selected-source ESLint pass. Public output contains code-quality metadata and aggregate security counts only; no security-boundary remediation or cloud closure is inferred.',
  };
  const save = () => writeFileSync(path.join(output, 'analysis-summary.json'), `${JSON.stringify(report, null, 2)}\n`);
  const record = (value) => appendFileSync(progress, `${JSON.stringify(value)}\n`);
  const started = Date.now();
  try {
    const engineRequire = createRequire('/node_modules/package.json');
    const { ESLint } = engineRequire('eslint');
    const { patternIdToEslint } = engineRequire('/dist/src/model/patterns.js');
    const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));
    const catalog = JSON.parse(readFileSync('/docs/patterns.json', 'utf8'));
    const manifest = readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES);
    const selection = selectTrackedFiles(manifest);
    report.manifestSha256 = createHash('sha256').update(manifest).digest('hex');
    report.selected = selection.sourceFileCount;
    report.selection = selection;
    save();
    const projection = await createProjection(snapshot, catalog, engineRequire);
    const targets = readNodeTargets(selection.packageManifestFiles);
    report.projectionReceipt = projection.receipt;
    report.nodeTargets = targets;
    const options = createRepositoryProfile(projection, targets);
    assert.equal(options.baseConfig.noInlineConfig, true);
    const eslint = new ESLint(options);
    const categories = new Map(snapshot.patterns.map((row) =>
      [patternIdToEslint(row.patternId.slice('ESLint8_'.length)), row.category]));
    for (const filename of selection.sourceFiles) {
      report.attempted += 1;
      const fileStarted = Date.now();
      record({ file: filename, state: 'started' });
      try {
        const source = readFileSync(path.join('/src', filename), 'utf8');
        const results = await eslint.lintText(source, { filePath: path.join('/src', filename) });
        assert.equal(results.length, 1, 'Every selected file must have a lint result');
        const result = results[0];
        const redacted = publicDiagnostics(result.messages, filename, categories);
        report.configurationErrors += redacted.configurationErrors;
        report.fatalParsing += result.messages.filter((message) => message.fatal === true).length;
        report.diagnosticCount += result.messages.length;
        for (const message of result.messages) {
          const id = categories.has(message.ruleId) ? message.ruleId : 'parser-or-unmapped-rule';
          report.countsByRule[id] = (report.countsByRule[id] || 0) + 1;
        }
        for (const [id, count] of Object.entries(redacted.securityCounts)) {
          report.securityCounts[id] = (report.securityCounts[id] || 0) + count;
        }
        for (const diagnostic of redacted.diagnostics) {
          appendFileSync(diagnostics, `${JSON.stringify(diagnostic)}\n`);
        }
        report.processed += 1;
        record({ file: filename, state: 'processed', milliseconds: Date.now() - fileStarted });
      } catch (error) {
        report.engineErrors += 1;
        const ruleId = engineRule(error, categories);
        const id = ruleId || 'unknown-rule';
        report.engineErrorsByRule[id] = (report.engineErrorsByRule[id] || 0) + 1;
        record({ file: filename, state: 'engine-error', kind: errorKind(error), ruleId, milliseconds: Date.now() - fileStarted });
      }
      if (report.attempted % 25 === 0) save();
    }
    report.allFilesProcessed = report.processed === report.selected;
  } catch (error) {
    report.initializationError = errorKind(error);
  } finally {
    report.milliseconds = Date.now() - started;
    // Docker stdout is a pipe. The host finalizer must inspect the actual
    // redirected output file before this evidence can be accepted.
    report.unreviewedToolOutputBytes = null;
    report.toolOutputReviewPending = true;
    report.completeAnalysisCertified = false;
    save();
  }
  if (!analysisStatus(report, 0).analysisGatePassed) process.exitCode = 1;
}

module.exports = { publicDiagnostics, engineRule, analysisStatus };

if (require.main === module) {
  if (process.argv[2] === '--finalize') finalizeEvidence(process.argv[3]);
  else analyze(process.argv[2]).catch(() => { process.exitCode = 1; });
}
