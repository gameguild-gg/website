'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const key = Symbol.for('gameguild.codacy.loader-startup');

// Node runs --require before --import. Capture the original output functions
// before either vendor TypeScript loader is initialized, without wrapping them.
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

module.exports = { assertOriginalWarningOutput, assertWarningEmissionUnchanged };
