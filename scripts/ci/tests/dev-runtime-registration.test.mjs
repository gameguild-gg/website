import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { copyFile, mkdir, mkdtemp, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { promisify } from 'node:util';
import { test } from 'node:test';

const executeFile = promisify(execFile);

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'gameguild-runtime-contract-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(join(root, 'scripts'));
  const temporary = join(root, 'temporary');
  await mkdir(temporary);
  const module = join(root, 'scripts/dev-runtime.mjs');
  await copyFile(new URL('../../dev-runtime.mjs', import.meta.url), module);
  return {
    moduleUrl: pathToFileURL(module).href,
    env: { ...process.env, TMPDIR: temporary, TMP: temporary, TEMP: temporary },
  };
}

test('asynchronous registration preserves identity, atomic publication and synchronous cleanup', async (t) => {
  const setup = await fixture(t);
  const result = await executeFile(process.execPath, ['--input-type=module', '-e', `
    import assert from 'node:assert/strict';
    import { readFile, readdir, writeFile } from 'node:fs/promises';
    import { existsSync } from 'node:fs';
    import { join } from 'node:path';
    import { registerDevProcess, listRegisteredDevProcesses, runtimeDirectory, repositoryRoot } from ${JSON.stringify(setup.moduleUrl)};
    assert.deepEqual(await listRegisteredDevProcesses(), []);
    const unregister = await registerDevProcess('synthetic-contract');
    const records = await listRegisteredDevProcesses();
    assert.equal(records.length, 1);
    assert.equal(records[0].pid, process.pid);
    assert.equal(records[0].mode, 'synthetic-contract');
    assert.equal(records[0].root, repositoryRoot);
    assert.equal(JSON.parse(await readFile(records[0].file, 'utf8')).version, 1);
    assert.equal((await readdir(runtimeDirectory)).some(name => name.endsWith('.tmp')), false);
    const malformed = join(runtimeDirectory, 'malformed.json');
    const foreign = join(runtimeDirectory, 'foreign.json');
    await writeFile(malformed, '{');
    await writeFile(foreign, JSON.stringify({pid: process.pid, mode: 'foreign', root: repositoryRoot + '-other'}));
    assert.equal((await listRegisteredDevProcesses()).length, 1);
    assert.equal(existsSync(malformed), false);
    assert.equal(existsSync(foreign), false);
    unregister();
    assert.equal(existsSync(records[0].file), false);
    unregister();
    assert.deepEqual(await listRegisteredDevProcesses(), []);
    console.log('registration-contract-ok');
  `], { env: setup.env, timeout: 10_000 });
  assert.equal(result.stderr, '');
  assert.equal(result.stdout.trim(), 'registration-contract-ok');
});

test('normal process exit removes the published registration', async (t) => {
  const setup = await fixture(t);
  const result = await executeFile(process.execPath, ['--input-type=module', '-e', `
    import { registerDevProcess, listRegisteredDevProcesses, runtimeDirectory } from ${JSON.stringify(setup.moduleUrl)};
    await registerDevProcess('synthetic-exit');
    const records = await listRegisteredDevProcesses();
    if (records.length !== 1) throw new Error('registration missing');
    console.log(JSON.stringify({directory: runtimeDirectory, file: records[0].file}));
  `], { env: setup.env, timeout: 10_000 });
  assert.equal(result.stderr, '');
  const registration = JSON.parse(result.stdout);
  assert.deepEqual(await readdir(registration.directory), []);
  await assert.rejects(readFile(registration.file), { code: 'ENOENT' });
});
