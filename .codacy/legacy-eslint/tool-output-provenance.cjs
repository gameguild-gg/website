'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { appendFileSync, writeFileSync } = require('node:fs');
const path = require('node:path');
const { types } = require('node:util');

function dependencyFrames(stack) {
  const frames = [];
  for (const line of typeof stack === 'string' ? stack.split('\n') : []) {
    if (!/^\s+at /u.test(line)) continue;
    const match = line.match(/\/node_modules\/((?:@?[A-Za-z0-9_.+-]+\/)*[A-Za-z0-9_.+-]+\.(?:c|m)?js):(\d+):(\d+)\)?$/u);
    if (match && !match[1].split('/').some((part) => part === '.' || part === '..')) {
      frames.push({ file: match[1], line: Number(match[2]), column: Number(match[3]) });
    }
    if (frames.length === 6) break;
  }
  return frames;
}

function observeWrite(write, stream, record, stack = () => new Error().stack) {
  assert(['stdout', 'stderr'].includes(stream));
  return function (chunk, ...args) {
    const caller = stack();
    const encoding = typeof args[0] === 'string' ? args[0] : 'utf8';
    const bytes = typeof chunk === 'string' && Buffer.isEncoding(encoding)
      ? Buffer.from(chunk, encoding) : types.isUint8Array(chunk) ? Buffer.from(chunk) : null;
    const sha256 = bytes === null ? null : createHash('sha256').update(bytes).digest('hex');
    // Preserve the receiver, callback, return value/backpressure and failures.
    // The original output stays visible and remains unreviewed by the gate.
    const result = Reflect.apply(write, this, [chunk, ...args]);
    record({ event: 'write', stream, bytes: bytes?.length ?? null, sha256,
      dependencyFrames: dependencyFrames(caller) });
    return result;
  };
}

function warningMetadata(warning) {
  const kinds = ['Warning', 'DeprecationWarning', 'ExperimentalWarning', 'Error', 'TypeError'];
  const code = typeof warning?.code === 'string' ? warning.code : '';
  return { event: 'warning', kind: kinds.includes(warning?.name) ? warning.name : 'Warning',
    code: /^DEP\d{4}$/u.test(code) ? code : null,
    dependencyFrames: dependencyFrames(warning?.stack) };
}

function installToolOutputProvenance(output) {
  assert(path.isAbsolute(output));
  const filename = path.join(output, 'tool-output-provenance.jsonl');
  writeFileSync(filename, '');
  const record = (value) => appendFileSync(filename, `${JSON.stringify(value)}\n`);
  process.stdout.write = observeWrite(process.stdout.write, 'stdout', record);
  process.stderr.write = observeWrite(process.stderr.write, 'stderr', record);
  process.on('warning', (warning) => record(warningMetadata(warning)));
}

module.exports = { dependencyFrames, observeWrite, warningMetadata, installToolOutputProvenance };
