import assert from 'node:assert/strict';
import test from 'node:test';

import { ToolchainPreset } from 'emception';
import { wrapWorkerClient } from '../dist/createEmception.js';
import { compileAndRun } from '../dist/presets.js';

function makeStubClient(overrides = {}) {
  return {
    run: async () => ({ exitCode: 0, stdout: 'hi\n', stderr: '', durationMs: 1, timedOut: false }),
    getFile: async () => null,
    writeFile: async () => {},
    deleteFile: async () => {},
    listDir: async () => [],
    resetVfs: async () => {},
    boot: async () => {},
    terminate: () => {},
    ...overrides,
  };
}

test('facade.compileAndRun returns a ToolResult-shaped value', async () => {
  const api = wrapWorkerClient(makeStubClient());
  const result = await api.compileAndRun('int main(){return 0;}');
  assert.equal(typeof result.exitCode, 'number');
  assert.equal(typeof result.stdout, 'string');
  assert.equal(typeof result.stderr, 'string');
  assert.equal(typeof result.durationMs, 'number');
  assert.equal(typeof result.timedOut, 'boolean');
});

test('facade.runTests compiles persisted plan sources without replacing them with main.cpp', async () => {
  const writes = [];
  const runs = [];
  const api = wrapWorkerClient(makeStubClient({
    writeFile: async (path, content) => { writes.push({ path, content }); },
    run: async (cmd, argv) => {
      runs.push({ cmd, argv });
      return { exitCode: 0, stdout: '5\n', stderr: '', durationMs: 1, timedOut: false };
    },
  }));

  const report = await api.runTests({
    build: {
      toolchain: ToolchainPreset.CPP,
      sources: ['/user/main.cpp', '/home/user/functional_0_test.cpp'],
    },
    cases: [{ kind: 'stdio', expectedStdout: '5\n' }],
  });

  assert.equal(report.passed, 1);
  assert.deepEqual(writes, [], 'existing workspace files must not be overwritten');
  assert.deepEqual(
    runs.filter(({ cmd }) => cmd === 'clang').map(({ argv }) => argv.at(-1)),
    ['/user/main.cpp', '/home/user/functional_0_test.cpp'],
  );
  const link = runs.find(({ cmd }) => cmd === 'wasm-ld');
  assert.ok(link, 'links all compiled source objects');
  assert.equal(link.argv.filter((value) => /^\/tmp\/emception-build-\d+-\d+\.o$/.test(value)).length, 2);
});

test('facade isolates native scratch outputs between sequential test runs', async () => {
  const runs = [];
  const api = wrapWorkerClient(makeStubClient({
    run: async (cmd, argv) => {
      runs.push({ cmd, argv });
      return { exitCode: 0, stdout: '5\n', stderr: '', durationMs: 1, timedOut: false };
    },
  }));

  const plan = {
    build: {
      toolchain: ToolchainPreset.CPP,
      sources: ['/user/main.cpp'],
    },
    cases: [{ kind: 'stdio', expectedStdout: '5\n' }],
  };

  await api.runTests(plan);
  await api.runTests(plan);

  const links = runs.filter(({ cmd }) => cmd === 'wasm-ld');
  assert.equal(links.length, 2);
  const outputs = links.map(({ argv }) => argv[argv.indexOf('-o') + 1]);
  assert.notEqual(outputs[0], outputs[1], 'each invocation must use a fresh WASM output path');
});

test('facade removes an explicit native output before relinking it', async () => {
  const deletedPaths = [];
  const api = wrapWorkerClient(makeStubClient({
    deleteFile: async (path) => { deletedPaths.push(path); },
  }));

  await api.compileAndRun(undefined, {
    build: {
      toolchain: ToolchainPreset.CPP,
      sources: ['/user/main.cpp'],
      output: '/user/program.wasm',
    },
  });

  assert.equal(deletedPaths[0], '/user/program.wasm');
  assert.ok(
    deletedPaths.some((path) => /^\/tmp\/emception-build-\d+-0\.o$/.test(path)),
    'private object files are cleaned after execution',
  );
});

test('facade.runTests resolves a TestReport-shaped value', async () => {
  const api = wrapWorkerClient(makeStubClient());
  const report = await api.runTests({
    build: { toolchain: ToolchainPreset.CPP },
    cases: [{ kind: 'stdio', expectedStdout: 'hi' }],
  });
  assert.equal(typeof report.passed, 'number');
  assert.equal(typeof report.failed, 'number');
  assert.equal(typeof report.totalDurationMs, 'number');
  assert.ok(Array.isArray(report.cases));
  assert.equal(report.passed + report.failed, report.cases.length);
});

test('facade preserves the core API surface', () => {
  const api = wrapWorkerClient(makeStubClient());
  assert.ok(api.workspace);
  assert.equal(typeof api.run, 'function');
  assert.equal(typeof api.compileAndRun, 'function');
  assert.equal(typeof api.runTests, 'function');
  assert.equal(typeof api.on, 'function');
  assert.equal(typeof api.dispose, 'function');
  assert.equal(typeof api.workspace.deleteFile, 'function');
});

test('facade forwards workspace deletion to the Worker client', async () => {
  const deletedPaths = [];
  const api = wrapWorkerClient(makeStubClient({
    deleteFile: async (path) => { deletedPaths.push(path); },
  }));

  await api.workspace.deleteFile('/home/user/private-test.cpp');

  assert.deepEqual(deletedPaths, ['/home/user/private-test.cpp']);
});

test('preset helper forwards facade output and stdin through the public API', async () => {
  const stdout = [];
  const stderr = [];
  const phases = [];
  const runs = [];
  const writes = [];
  const inputBytes = [];
  const api = wrapWorkerClient(makeStubClient({
    writeFile: async (path, content) => { writes.push({ path, content }); },
    run: async (cmd, argv, options) => {
      runs.push({ cmd, argv, cwd: options.cwd });
      options.onStdout?.(`${cmd}: output\n`);
      options.onStderr?.(`${cmd}: diagnostic\n`);
      if (cmd === 'wasi-run') {
        assert.equal(typeof options.stdin, 'function');
        for (let byte = await options.stdin(); byte !== null; byte = await options.stdin()) {
          inputBytes.push(byte);
        }
      } else {
        assert.equal(options.stdin, undefined);
      }
      return { exitCode: 0, stdout: `${cmd}: output\n`, stderr: `${cmd}: diagnostic\n` };
    },
  }));

  const result = await compileAndRun(api, {
    toolchain: ToolchainPreset.CPP,
    source: 'int main(){return 0;}',
    paths: { sourcePath: '/user/hello.cpp' },
    stdin: 'olá',
    onStdout: (text) => stdout.push(text),
    onStderr: (text) => stderr.push(text),
    onPhase: (phase) => phases.push(phase),
  });

  assert.equal(result.finalPhase, 'run');
  assert.equal(result.exitCode, 0);
  assert.deepEqual(phases, ['write', 'compile', 'link', 'run']);
  assert.deepEqual(runs.map(({ cmd }) => cmd), ['clang', 'wasm-ld', 'wasi-run']);
  assert.ok(runs.every(({ cwd }) => cwd === '/user'));
  assert.deepEqual(stdout, ['clang: output\n', 'wasm-ld: output\n', 'wasi-run: output\n']);
  assert.deepEqual(stderr, ['clang: diagnostic\n', 'wasm-ld: diagnostic\n', 'wasi-run: diagnostic\n']);
  assert.equal(new TextDecoder().decode(Uint8Array.from(inputBytes)), 'olá\n');
  assert.equal(writes[0].path, '/user/hello.cpp');
  assert.equal(new TextDecoder().decode(writes[0].content), 'int main(){return 0;}');
});

test('preset helper retains direct Worker callbacks and byte stdin', async () => {
  const stdout = [];
  const stderr = [];
  const inputBytes = [];
  const runs = [];
  const client = makeStubClient({
    run: async (cmd, argv, options) => {
      runs.push({ cmd, cwd: options.cwd });
      options.onStdout?.(cmd);
      options.onStderr?.(`${cmd}: warning`);
      if (cmd === 'wasi-run') {
        for (let byte = await options.stdin(); byte !== null; byte = await options.stdin()) {
          inputBytes.push(byte);
        }
      }
      return { exitCode: 0, stdout: cmd, stderr: '' };
    },
  });

  await compileAndRun(client, {
    toolchain: ToolchainPreset.CPP,
    source: 'int main(){return 0;}',
    cwd: '/custom',
    stdin: 'ready\n',
    onStdout: (text) => stdout.push(text),
    onStderr: (text) => stderr.push(text),
  });

  assert.deepEqual(runs.map(({ cmd }) => cmd), ['clang', 'wasm-ld', 'wasi-run']);
  assert.ok(runs.every(({ cwd }) => cwd === '/custom'));
  assert.deepEqual(stdout, ['clang', 'wasm-ld', 'wasi-run']);
  assert.deepEqual(stderr, ['clang: warning', 'wasm-ld: warning', 'wasi-run: warning']);
  assert.equal(new TextDecoder().decode(Uint8Array.from(inputBytes)), 'ready\n');
});

test('preset helper decodes split UTF-8 public sinks without corrupting output', async () => {
  const stdout = [];
  const stderr = [];
  const encoder = new TextEncoder();
  const api = {
    workspace: { writeFile: async () => {} },
    run: async (cmd, argv, options) => {
      if (cmd === 'wasi-run') {
        const out = encoder.encode('café');
        const err = encoder.encode('漢');
        options.stdout(out.subarray(0, 4));
        options.stdout(out.subarray(4));
        options.stderr(err.subarray(0, 1));
        options.stderr(err.subarray(1));
        assert.equal(options.stdin, undefined);
      }
      return { exitCode: 0, stdout: '', stderr: '', durationMs: 1, timedOut: false };
    },
  };

  await compileAndRun(api, {
    toolchain: ToolchainPreset.CPP,
    source: 'int main(){return 0;}',
    onStdout: (text) => stdout.push(text),
    onStderr: (text) => stderr.push(text),
  });

  assert.equal(stdout.join(''), 'café');
  assert.equal(stderr.join(''), '漢');
});

for (const failedPhase of ['compile', 'link']) {
  test(`preset helper stops after a facade ${failedPhase} failure`, async () => {
    const runs = [];
    const failingTool = failedPhase === 'compile' ? 'clang' : 'wasm-ld';
    const api = wrapWorkerClient(makeStubClient({
      run: async (cmd) => {
        runs.push(cmd);
        return { exitCode: cmd === failingTool ? 9 : 0, stdout: '', stderr: '' };
      },
    }));

    const result = await compileAndRun(api, {
      toolchain: ToolchainPreset.CPP,
      source: 'int main(){return 0;}',
    });

    assert.equal(result.finalPhase, failedPhase);
    assert.equal(result.exitCode, 9);
    assert.equal(result.run, undefined);
    assert.deepEqual(runs, failedPhase === 'compile' ? ['clang'] : ['clang', 'wasm-ld']);
  });
}
