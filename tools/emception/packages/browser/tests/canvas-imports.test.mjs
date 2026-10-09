import assert from 'node:assert/strict';
import test from 'node:test';

import { startCanvasArtifact } from '../dist/canvas-runtime.js';

// A real WASM application imports a C GL symbol and calls it from its entrypoint.
// Only the generated JS factory boundary is controlled in these unit regressions.
const bytes = (value) => {
  const result = [];
  do {
    const next = value & 0x7f;
    value >>>= 7;
    result.push(next | (value ? 0x80 : 0));
  } while (value);
  return result;
};
const text = (value) => {
  const encoded = [...new TextEncoder().encode(value)];
  return [...bytes(encoded.length), ...encoded];
};
const section = (id, payload) => [id, ...bytes(payload.length), ...payload];

function applicationWasm(profile, importedName = 'glBindBuffer') {
  const sdl = profile === 'sdl3-runtime';
  const types = [
    4,
    0x60, 2, 0x7f, 0x7f, 0,
    0x60, 3, 0x7f, 0x7f, 0x7f, 1, 0x7f,
    0x60, 1, 0x7f, 1, 0x7f,
    0x60, 2, 0x7f, 0x7f, 1, 0x7f,
  ];
  const init = [0, 0x41, ...bytes(34962), 0x41, 7, 0x10, 0, 0x41, 0, 0x0b];
  const iterate = [0, 0x41, 1, 0x0b];
  const exports = sdl
    ? [3, ...text('memory'), 2, 0, ...text('SDL_AppInit'), 0, 1, ...text('SDL_AppIterate'), 0, 2]
    : [2, ...text('memory'), 2, 0, ...text('main'), 0, 1];
  return new Uint8Array([
    0, 97, 115, 109, 1, 0, 0, 0,
    ...section(1, types),
    ...section(2, [1, ...text('env'), ...text(importedName), 0, 0]),
    ...section(3, sdl ? [2, 1, 2] : [1, 3]),
    ...section(5, [1, 0, 1]),
    ...section(7, exports),
    ...section(10, sdl ? [2, ...bytes(init.length), ...init, ...bytes(iterate.length), ...iterate]
                      : [1, ...bytes(init.length), ...init]),
  ]);
}

function fixture(profile, importedName) {
  const forwarded = [];
  const revoked = [];
  const artifact = {
    phase: 'ready',
    compile: { exitCode: 0, stdout: '', stderr: '', durationMs: 1, timedOut: false },
    link: { exitCode: 0, stdout: '', stderr: '', durationMs: 1, timedOut: false },
    runtimeProfile: profile,
    runtimePath: `/usr/lib/emscripten/${profile}.mjs`,
    wasmPath: '/workspace/main.wasm',
    runtimeGlue: new Uint8Array(),
    wasm: applicationWasm(profile, importedName),
  };
  const dependencies = {
    createModuleUrl: () => 'memory:canvas-import-regression',
    revokeModuleUrl: (url) => revoked.push(url),
    importModule: async () => ({
      default: async (config) => {
        const imports = { env: { emscripten_glBindBuffer: (...args) => forwarded.push(args) } };
        const instance = config.instantiateWasm
          ? await new Promise((resolve) => config.instantiateWasm(imports, resolve))
          : (await WebAssembly.instantiate(config.wasmBinary, imports)).instance;
        return { _main: (...args) => instance.exports.main(...args) };
      },
    }),
  };
  return { artifact, dependencies, forwarded, revoked };
}

function animationFrames(t) {
    const originalRequest = globalThis.requestAnimationFrame;
    const originalCancel = globalThis.cancelAnimationFrame;
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => {};
    t.after(() => {
      globalThis.requestAnimationFrame = originalRequest;
      globalThis.cancelAnimationFrame = originalCancel;
    });
}

for (const profile of ['sdl3-runtime', 'raylib-runtime', 'allegro-runtime']) {
  test(`${profile} forwards C GL imports to the generated Emscripten binding`, async (t) => {
    animationFrames(t);
    const { artifact, dependencies, forwarded, revoked } = fixture(profile);
    assert.equal(WebAssembly.validate(artifact.wasm), true);
    const session = await startCanvasArtifact(artifact, { canvas: { width: 800, height: 600 } }, dependencies);
    assert.deepEqual(forwarded, [[34962, 7]]);
    session.stop();
    session.stop();
    assert.deepEqual(revoked, ['memory:canvas-import-regression']);
  });
}

test('an unsupported SDL import rejects startup and disposes the runtime URL', { timeout: 1500 }, async (t) => {
  animationFrames(t);
  const { artifact, dependencies, revoked } = fixture('sdl3-runtime', 'glUnavailableFuture');
  await assert.rejects(
    startCanvasArtifact(artifact, { canvas: { width: 800, height: 600 } }, dependencies),
    /glUnavailableFuture/,
  );
  assert.deepEqual(revoked, ['memory:canvas-import-regression']);
});

test('the Emscripten main-loop unwind keeps the started canvas session alive', async () => {
  const { artifact, dependencies, revoked } = fixture('raylib-runtime');
  let pauses = 0;
  dependencies.importModule = async () => ({ default: async () => ({
    _main() { throw 'unwind'; },
    pauseMainLoop() { pauses += 1; },
  }) });
  const session = await startCanvasArtifact(artifact, { canvas: { width: 800, height: 600 } }, dependencies);
  assert.deepEqual(revoked, []);
  session.stop();
  session.stop();
  assert.equal(pauses, 1);
  assert.deepEqual(revoked, ['memory:canvas-import-regression']);
});

test('a real runtime trap rejects startup instead of being treated as a main-loop unwind', async () => {
  const { artifact, dependencies, revoked } = fixture('raylib-runtime');
  const trap = new WebAssembly.RuntimeError('unreachable');
  dependencies.importModule = async () => ({ default: async () => ({ _main() { throw trap; } }) });
  await assert.rejects(startCanvasArtifact(artifact, { canvas: { width: 800, height: 600 } }, dependencies), (error) => error === trap);
  assert.deepEqual(revoked, ['memory:canvas-import-regression']);
});
