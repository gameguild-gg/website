import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(here, '../../../..');
const fixtureParent = path.join(repository, 'artifacts', 'test-results', 'ensure-emception-libs-fixtures');
const original = await readFile(path.resolve(here, '../ensure-emception-libs.mjs'), 'utf8');

async function fixture({ cache = false, complete = false, failure = false, foreignLock = false } = {}) {
  await mkdir(fixtureParent, { recursive: true });
  const root = await mkdtemp(path.join(fixtureParent, 'case-'));
  const emception = path.join(root, 'tools', 'emception');
  await mkdir(path.join(emception, 'scripts'), { recursive: true });
  let source = original;
  if (foreignLock) {
    // Reduce only the fixture's wait deadline to exercise timeout ownership.
    // Production startup and browser-test deadlines retain their original values.
    assert.ok(source.includes('10 * 60 * 1000'));
    source = source.replace('10 * 60 * 1000', '100');
  }
  const script = path.join(emception, 'scripts', 'ensure-emception-libs.mjs');
  await writeFile(script, source);
  for (const name of ['core', 'xterm', 'browser', 'ide']) {
    const dist = path.join(emception, 'packages', name, 'dist');
    await mkdir(dist, { recursive: true });
    await writeFile(path.join(dist, 'index.js'), 'export {};');
  }
  if (complete) {
    await mkdir(path.join(emception, 'dist'), { recursive: true });
    await writeFile(path.join(emception, 'dist', 'index.js'), 'export {};');
  }
  if (cache || foreignLock) await mkdir(path.join(root, '.cache'), { recursive: true });
  const lock = path.join(root, '.cache', 'emception-libs.lock');
  if (foreignLock) {
    await mkdir(lock);
    await writeFile(path.join(lock, 'owner.txt'), 'another-builder');
  }
  await writeFile(path.join(emception, 'package.json'), JSON.stringify({
    name: 'emception-library-fixture', private: true,
    scripts: { 'build:lib': 'node build-fixture.mjs' },
  }));
  await writeFile(path.join(emception, 'build-fixture.mjs'), failure
    ? 'process.exitCode = 7;'
    : "import {mkdirSync,writeFileSync} from 'node:fs'; mkdirSync('dist',{recursive:true}); writeFileSync('dist/index.js','export {};');");
  return { root, emception, script, lock };
}

async function execute(script, timeoutMs = 5000) {
  return await new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [script], { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    let output = '';
    let timedOut = false;
    child.stdout.on('data', (data) => { output += data; });
    child.stderr.on('data', (data) => { output += data; });
    const timeout = setTimeout(() => { timedOut = true; child.kill(); }, timeoutMs);
    child.on('error', (error) => { clearTimeout(timeout); reject(error); });
    child.on('close', (code, signal) => {
      clearTimeout(timeout);
      resolve({ code, signal, timedOut, output });
    });
  });
}

async function cleanup(root) {
  const absolute = path.resolve(root);
  assert.equal(path.dirname(absolute), path.resolve(fixtureParent));
  assert.ok(path.basename(absolute).startsWith('case-'));
  await rm(absolute, { recursive: true, force: true });
}

test('fresh cache parent permits the required library build and releases its lock', async () => {
  const value = await fixture();
  try {
    const result = await execute(value.script);
    assert.equal(result.timedOut, false, result.output);
    assert.equal(result.code, 0, result.output);
    assert.ok(existsSync(path.join(value.emception, 'dist', 'index.js')));
    assert.equal(existsSync(value.lock), false);
  } finally { await cleanup(value.root); }
});

test('an existing cache parent still permits the library build', async () => {
  const value = await fixture({ cache: true });
  try {
    const result = await execute(value.script);
    assert.equal(result.code, 0, result.output);
    assert.ok(existsSync(path.join(value.emception, 'dist', 'index.js')));
    assert.equal(existsSync(value.lock), false);
  } finally { await cleanup(value.root); }
});

test('complete libraries skip work without requiring a cache parent', async () => {
  const value = await fixture({ complete: true });
  try {
    const result = await execute(value.script);
    assert.equal(result.code, 0, result.output);
    assert.match(result.output, /all dists present/);
    assert.equal(existsSync(value.lock), false);
  } finally { await cleanup(value.root); }
});

test('a failed library build releases only its acquired lock', async () => {
  const value = await fixture({ cache: true, failure: true });
  try {
    const result = await execute(value.script);
    assert.equal(result.timedOut, false, result.output);
    assert.notEqual(result.code, 0, result.output);
    assert.equal(existsSync(value.lock), false, 'failed build retained its lock');
  } finally { await cleanup(value.root); }
});

test('wait timeout preserves the foreign lock and never builds without ownership', async () => {
  const value = await fixture({ foreignLock: true });
  try {
    const result = await execute(value.script);
    assert.equal(result.timedOut, false, result.output);
    assert.notEqual(result.code, 0, result.output);
    assert.equal(existsSync(path.join(value.emception, 'dist', 'index.js')), false);
    assert.equal(await readFile(path.join(value.lock, 'owner.txt'), 'utf8'), 'another-builder');
  } finally { await cleanup(value.root); }
});
