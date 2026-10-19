import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const [originalDirectory, candidateDirectory] = process.argv.slice(2).map((directory) => path.resolve(directory));
const read = (directory, name) => readFileSync(path.join(directory, name));
const original = JSON.parse(read(originalDirectory, 'synthetic-factory.json'));
const candidate = JSON.parse(read(candidateDirectory, 'synthetic-factory.json'));
assert.equal(original.complete, true);
assert.equal(candidate.complete, true);
assert.equal(original.loader.name, 'ts-node');
assert.equal(candidate.loader.name, 'tsx');
for (const field of ['sourceHead', 'scriptSha256', 'configSha256', 'factorySha256',
  'factoryOptionsSha256', 'initializedPlugins', 'identities', 'cases', 'warningOutput', 'warningOutputAfterFactory']) {
  assert.deepEqual(candidate[field], original[field], `Loader comparison changed ${field}`);
}
for (const stream of ['stdout', 'stderr']) {
  assert.equal(read(candidateDirectory, `vendor-${stream}.txt`).length, 0,
    'The replacement loader must produce no unreviewed output');
}
writeFileSync(path.join(candidateDirectory, 'loader-comparison.json'), `${JSON.stringify({
  sourceHead: candidate.sourceHead, original: original.loader, candidate: candidate.loader,
  nativeFactoryAndConfigurationIdentical: true, syntheticResultsIdentical: true,
  warningOutputFunctionsUnchanged: true, candidateOutputBytes: 0,
  sourceAnalysisReplacedByControls: false, certifiedDestinationClosures: 0,
}, null, 2)}\n`);
