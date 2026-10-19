'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { existsSync, readFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const key = Symbol.for('gameguild.codacy.loader-startup');

// Node runs --require before --import. Capture the original output functions
// before the vendor TypeScript loader is initialized, without wrapping them.
if (process.execArgv.includes(`--require=${__filename}`) && !Object.hasOwn(globalThis, key)) {
  Object.defineProperty(globalThis, key, {
    value: Object.freeze({ emit: process.emit, emitWarning: process.emitWarning, stderrWrite: process.stderr.write,
      sourceSha256: createHash('sha256').update(readFileSync(__filename)).digest('hex') }),
    configurable: false, enumerable: false, writable: false,
  });
}

function assertWarningEmissionUnchanged() {
  const original = globalThis[key];
  assert(original, 'The warning baseline must be preloaded before the vendor loader');
  assert.equal(process.emit, original.emit, 'The loader changed process.emit');
  assert.equal(process.emitWarning, original.emitWarning, 'The loader changed process.emitWarning');
  return { earlyBaselinePresent: true, emitUnchanged: true, emitWarningUnchanged: true,
    baselineSourceSha256: original.sourceSha256 };
}

function assertOriginalWarningOutput() {
  const state = assertWarningEmissionUnchanged();
  assert.equal(process.stderr.write, globalThis[key].stderrWrite, 'The loader changed stderr.write');
  return { ...state, stderrWriteUnchanged: true };
}

function readLoaderIdentity() {
  assert(process.execArgv.includes('--import=/app/register.js'));
  const engineRequire = createRequire('/app/package.json');
  const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
  const manifest = readFileSync(engineRequire.resolve('ts-node/package.json'));
  const packageSha256 = digest(manifest);
  assert.equal(JSON.parse(manifest).version, '10.9.2');
  assert.equal(packageSha256, '80961faab4990ab53d33976bf716aa32444d70c64f73a487c23c21b0ca81398e');
  const resolverSha256 = digest(readFileSync('/app/node_modules/ts-node/dist-raw/node-internal-modules-esm-resolve.js'));
  const errorDefinitionsSha256 = digest(readFileSync('/app/node_modules/ts-node/dist-raw/node-internal-errors.js'));
  assert.equal(errorDefinitionsSha256, '7a1ae4531b80168e3a83eda10e5c1d46ba75afc3feab8fbf2e6057591428a6c7');
  const originalSha256 = '205cce0aabf93b76c805bebaf092f57900e2a458089805ca3009239f6a649365';
  const repairedSha256 = '597100e286a0b95b3d5d742d138b37a4784056eac23491a4e8eccb89debcfd3d';
  assert([originalSha256, repairedSha256].includes(resolverSha256), 'Unreviewed loader source');
  const repaired = resolverSha256 === repairedSha256;
  const receiptPath = '/opt/gameguild-codacy/loader-repair.json';
  assert.equal(existsSync(receiptPath), repaired);
  const repairReceipt = repaired ? JSON.parse(readFileSync(receiptPath)) : null;
  if (repaired) {
    assert.equal(repairReceipt.package, 'ts-node');
    assert.equal(repairReceipt.version, '10.9.2');
    assert.equal(repairReceipt.packageSha256, packageSha256);
    assert.equal(repairReceipt.originalSha256, originalSha256);
    assert.equal(repairReceipt.repairedSha256, resolverSha256);
    assert.equal(repairReceipt.readbackSha256, resolverSha256);
    assert.equal(repairReceipt.repairScriptSha256,
      digest(readFileSync('/opt/gameguild-codacy/repair-loader.cjs')));
    assert.deepEqual(repairReceipt.changedFiles, ['dist-raw/node-internal-modules-esm-resolve.js']);
  }
  return { name: 'ts-node', version: '10.9.2', packageSha256, resolverSha256,
    errorDefinitionsSha256, repaired, repairReceipt };
}

module.exports = { assertOriginalWarningOutput, assertWarningEmissionUnchanged, readLoaderIdentity };
