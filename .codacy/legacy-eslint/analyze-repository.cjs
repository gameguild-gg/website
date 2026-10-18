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

function engineFailure(error, categories) {
  const message = typeof error?.message === 'string' ? error.message : '';
  const property = message.match(/^Cannot read properties of (?:null|undefined) \(reading '([A-Za-z]+)'\)/u)?.[1];
  const knownProperties = new Set(['name', 'type', 'body', 'value', 'imported', 'references',
    'range', 'loc', 'start', 'end', 'attributes', 'parent', 'node']);
  let reason = 'unclassified-engine-error';
  if (knownProperties.has(property)) reason = `missing-node-${property}`;
  else if (message.startsWith('No ESLint configuration found in ')) reason = 'missing-eslint-configuration';
  else if (message.startsWith('Fix objects must not be overlapped in a report.')) reason = 'overlapping-autofix';
  else if (message.startsWith('propValue.lastIndexOf is not a function')) reason = 'non-string-role-value';
  const frames = [];
  for (const line of typeof error?.stack === 'string' ? error.stack.split('\n') : []) {
    const match = line.match(/\/node_modules\/((?:@?[A-Za-z0-9_.+-]+\/)*[A-Za-z0-9_.+-]+\.js):(\d+):(\d+)\)?$/u);
    if (match) frames.push({ file: match[1], line: Number(match[2]), column: Number(match[3]) });
    if (frames.length === 6) break;
  }
  return { kind: errorKind(error), ruleId: engineRule(error, categories), reason, dependencyFrames: frames };
}

function partitionSources(files, index, count) {
  assert(Number.isSafeInteger(count) && count > 0 && count <= files.length, 'Invalid partition count');
  assert(Number.isSafeInteger(index) && index >= 0 && index < count, 'Invalid partition index');
  return files.filter((filename, position) => position % count === index);
}

function analysisStatus(report, toolOutputBytes) {
  const complete = Number.isSafeInteger(report.selected) && report.selected > 0
    && report.attempted === report.selected && report.processed === report.selected
    && report.allFilesProcessed === true && report.engineErrors === 0
    && report.configurationErrors === 0 && report.fatalParsing === 0
    && !report.initializationError && toolOutputBytes === 0;
  return { completeAnalysisCertified: complete, analysisGatePassed: complete && report.diagnosticCount === 0 };
}

function qualifiedAnalysisStatus(report, bytes) {
  const status = analysisStatus(report, bytes);
  return { ...status, partitionAnalysisComplete: status.completeAnalysisCertified,
    completeAnalysisCertified: status.completeAnalysisCertified
      && report.partition?.count === 1 && report.diagnosticSample !== true };
}

function finalizeEvidence(output) {
  const filename = path.join(output, 'analysis-summary.json');
  const report = JSON.parse(readFileSync(filename, 'utf8'));
  assert.equal(report.head, process.env.GAMEGUILD_CODACY_SOURCE_HEAD, 'Evidence source differs');
  for (const phase of ['before', 'after']) {
    assert.equal(readFileSync(path.join(output, `source-${phase}.txt`), 'utf8').trim(),
      'tracked-files-match-checkout', 'Tracked source must match the checkout before and after analysis');
  }
  const bytes = statSync(path.join(output, 'disposable-tool-output.log')).size;
  Object.assign(report, qualifiedAnalysisStatus(report, bytes), {
    unreviewedToolOutputBytes: bytes, toolOutputReviewPending: false,
    workingTreeVerifiedBeforeAndAfter: true,
  });
  writeFileSync(filename, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify({ head: report.head, selected: report.selected,
    attempted: report.attempted, processed: report.processed, engineErrors: report.engineErrors,
    configurationErrors: report.configurationErrors, fatalParsing: report.fatalParsing,
    diagnosticCount: report.diagnosticCount, toolOutputBytes: bytes,
    partition: report.partition, diagnosticSample: report.diagnosticSample,
    partitionAnalysisComplete: report.partitionAnalysisComplete,
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
  const sourceHashes = path.join(output, 'source-hashes.jsonl');
  writeFileSync(progress, '');
  writeFileSync(diagnostics, '');
  writeFileSync(sourceHashes, '');
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
    partitionAnalysisComplete: false,
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
    const index = Number(process.env.GAMEGUILD_CODACY_PARTITION_INDEX ?? 0);
    const count = Number(process.env.GAMEGUILD_CODACY_PARTITION_COUNT ?? 1);
    let assigned = partitionSources(selection.sourceFiles, index, count);
    const casesPath = process.env.GAMEGUILD_CODACY_DIAGNOSTIC_CASES;
    const cases = casesPath ? JSON.parse(readFileSync(casesPath, 'utf8')) : [];
    report.diagnosticSample = Boolean(casesPath);
    if (casesPath) {
      assert.equal(count, 1, 'A diagnostic sample is separate from the complete partitioned pass');
      assert(Array.isArray(cases) && cases.length > 0, 'Diagnostic cases are required');
      assigned = cases.map((row) => row.file);
      assert.equal(new Set(assigned).size, assigned.length, 'Duplicate diagnostic source');
      assert(assigned.every((file) => selection.sourceFiles.includes(file)), 'Untracked diagnostic source');
    }
    report.partition = { index, count, totalSourceFiles: selection.sourceFileCount };
    report.assignedSourceFiles = assigned;
    report.scope = casesPath
      ? 'Diagnostic sample of immutable observed crash inputs. It cannot certify full source coverage; its counts overlap the complete partitions.'
      : `Partition ${index + 1} of ${count} of the complete tracked-source list. Full analysis requires every partition and independent source-byte qualification.`;
    report.manifestSha256 = createHash('sha256').update(manifest).digest('hex');
    report.selected = assigned.length;
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
    for (const filename of assigned) {
      report.attempted += 1;
      const fileStarted = Date.now();
      record({ file: filename, state: 'started' });
      try {
        const source = readFileSync(path.join('/src', filename));
        const sourceHash = createHash('sha256').update(source).digest('hex');
        if (casesPath) assert.equal(sourceHash, cases.find((row) => row.file === filename).sha256,
          'The actual diagnostic source must match its immutable recorded input');
        appendFileSync(sourceHashes, `${JSON.stringify({ file: filename, bytes: source.length,
          sha256: sourceHash })}\n`);
        const results = await eslint.lintText(source.toString('utf8'), { filePath: path.join('/src', filename) });
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
        if (redacted.diagnostics.length) {
          appendFileSync(diagnostics, `${redacted.diagnostics.map((row) => JSON.stringify(row)).join('\n')}\n`);
        }
        report.processed += 1;
        record({ file: filename, state: 'processed', milliseconds: Date.now() - fileStarted });
      } catch (error) {
        report.engineErrors += 1;
        const ruleId = engineRule(error, categories);
        const id = ruleId || 'unknown-rule';
        report.engineErrorsByRule[id] = (report.engineErrorsByRule[id] || 0) + 1;
        record({ file: filename, state: 'engine-error', ...engineFailure(error, categories),
          milliseconds: Date.now() - fileStarted });
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

module.exports = { publicDiagnostics, engineRule, engineFailure, partitionSources,
  analysisStatus, qualifiedAnalysisStatus };

if (require.main === module) {
  if (process.argv[2] === '--finalize') finalizeEvidence(process.argv[3]);
  else analyze(process.argv[2]).catch(() => { process.exitCode = 1; });
}
