import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const output = path.resolve(process.argv[2]);
const read = (name) => readFileSync(path.join(output, name), 'utf8').trim();
const report = JSON.parse(read('summary.json'));
assert.equal(report.sourceHead, process.env.GAMEGUILD_CODACY_SOURCE_HEAD);
for (const phase of ['before', 'after']) {
  assert.equal(read(`source-${phase}.txt`), 'tracked-files-match-checkout');
}
const toolOutput = readFileSync(path.join(output, 'disposable-tool-output.log'));
report.toolOutputBytes = toolOutput.length;
report.toolOutputSha256 = createHash('sha256').update(toolOutput).digest('hex');
report.workingTreeVerifiedBeforeAndAfter = true;
report.completeSourceAnalysis = report.completeSourceAnalysis === true
  && !report.initializationError && report.allFilesProcessed === true
  && report.selected > 0 && report.attempted === report.selected && report.processed === report.selected
  && report.engineErrors === 0 && report.fatalParsing === 0 && report.configurationErrors === 0
  && toolOutput.length === 0 && read('analysis-exit.txt') === '0';
report.cleanAnalysis = report.completeSourceAnalysis && report.diagnostics === 0;
writeFileSync(path.join(output, 'summary.json'), `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(`${JSON.stringify({ selected: report.selected, processed: report.processed,
  engineErrors: report.engineErrors, fatalParsing: report.fatalParsing,
  configurationErrors: report.configurationErrors, diagnostics: report.diagnostics,
  toolOutputBytes: report.toolOutputBytes, completeSourceAnalysis: report.completeSourceAnalysis,
  cleanAnalysis: report.cleanAnalysis, certifiedDestinationClosures: 0 })}\n`);
if (!report.completeSourceAnalysis) process.exitCode = 1;
