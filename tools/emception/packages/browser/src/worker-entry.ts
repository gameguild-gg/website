/**
 * Web Worker entry point for the Emception toolchain.
 *
 * This script runs inside a dedicated Worker. It boots the full toolchain
 * (VFS, LazyFS, IDBFS, ToolRunner, MiniShell) and communicates with the
 * main thread via the postMessage protocol defined in worker-protocol.ts.
 *
 * The main thread never touches WASM — all compilation happens here.
 * Asyncify handles async suspension/resume within Workers, and IndexedDB
 * is available, so the entire existing architecture works unchanged.
 */

import type { IOProvider, MainToWorkerMessage, WorkerToMainMessage } from 'emception';
import { OverlayFS } from 'emception';
import { detectAsyncStrategy } from './async-bridge.js';
import { ManifestCompatibilityError, parseManifest } from './manifest.js';
import { FetchBridge } from './net/fetch-bridge.js';
import { MiniShell } from './shell.js';
import { ToolRunner } from './tool-runner.js';
import { IDBFS } from './vfs/idb.js';
import { createVFSManager } from './vfs/index.js';
import type { FSManifest } from './vfs/lazy.js';
import { LazyFS } from './vfs/lazy.js';

interface BrotliModule {
  HEAPU8: Uint8Array;
  HEAPU32: Uint32Array;
  _malloc(size: number): number;
  _free(pointer: number): void;
  cwrap(
    name: 'brotli_decompress_buffer',
    returnType: 'number',
    argumentTypes: ['number', 'number', 'number'],
  ): (inputPtr: number, inputLen: number, outLenPtr: number) => number;
  cwrap(name: 'brotli_free_buffer', returnType: null, argumentTypes: ['number']): (pointer: number) => void;
  cwrap(name: 'brotli_get_last_error_message', returnType: 'string', argumentTypes: []): () => string;
}

interface BrotliModuleExports {
  default?: (options: {
    wasmBinary: ArrayBuffer;
    locateFile: (path: string) => string;
  }) => Promise<BrotliModule>;
}

const P = '[Emception:Worker]';

// Forward Worker console output to the main thread so Playwright
// (and DevTools) can capture [Emception:*] logs from the Worker.
const _nativeConsole = {
  log: console.log.bind(console),
  warn: console.warn.bind(console),
  error: console.error.bind(console),
  info: console.info.bind(console),
  debug: console.debug.bind(console),
};

function forwardLog(level: 'log' | 'warn' | 'error' | 'info' | 'debug', args: unknown[]): void {
  _nativeConsole[level](...args);
  try {
    // Only forward serialisable data; stringify complex objects.
    const safeArgs = args.map((a) =>
      typeof a === 'string' || typeof a === 'number' || typeof a === 'boolean' || a === null || a === undefined ? a : String(a),
    );
    self.postMessage({ type: 'log', level, args: safeArgs });
  } catch {
    /* ignore serialisation errors */
  }
}

console.log = (...args: unknown[]) => forwardLog('log', args);
console.warn = (...args: unknown[]) => forwardLog('warn', args);
console.error = (...args: unknown[]) => forwardLog('error', args);
console.info = (...args: unknown[]) => forwardLog('info', args);
console.debug = (...args: unknown[]) => forwardLog('debug', args);

/**
 * Page origin, set when the main thread sends the boot message.
 * Used to resolve relative URLs (e.g. /_next/static/...) that
 * the default Worker fetch() cannot handle because the Worker's
 * own URL is a webpack chunk/blob.
 */
let pageOrigin = '';

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function fetchManifestWithRetry(manifestUrl: string, attempts = 3): Promise<FSManifest> {
  let lastError: unknown;

  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const response = await fetch(manifestUrl, { cache: 'no-store' });
      const raw = await response.text();

      if (!response.ok) {
        throw new Error(`HTTP ${response.status} ${response.statusText}`);
      }

      const trimmed = raw.trimStart();
      if (trimmed.startsWith('<')) {
        throw new Error('received HTML instead of manifest JSON');
      }

      return parseManifest(JSON.parse(raw), { onLegacy: (message) => console.warn(`${message} Source: ${manifestUrl}`) });
    } catch (err) {
      if (err instanceof ManifestCompatibilityError) throw err;
      lastError = err;
      if (attempt < attempts) {
        await sleep(200 * attempt);
      }
    }
  }

  throw new Error(`Failed to load manifest from ${manifestUrl}: ${lastError instanceof Error ? lastError.message : String(lastError)}`);
}

// Patch fetch so relative URLs (starting with /) are resolved
// against the page origin instead of the Worker's blob URL.
const nativeFetch = self.fetch.bind(self);
// eslint-disable-next-line @typescript-eslint/no-explicit-any
(self as any).fetch = (input: RequestInfo | URL, init?: RequestInit) => {
  if (typeof input === 'string' && input.startsWith('/') && pageOrigin) {
    input = `${pageOrigin}${input}`;
  }
  return nativeFetch(input, init);
};

/* ------------------------------------------------------------------ */
/*  State                                                              */
/* ------------------------------------------------------------------ */

let runner: ToolRunner | null = null;
let vfs: ReturnType<typeof createVFSManager> | null = null;
let shell: MiniShell | null = null;

const STDIN_RING_SIZE = 4096;
const STDIN_CTRL_READ = 0;
const STDIN_CTRL_WRITE = 1;
const STDIN_CTRL_CLOSED = 2;

interface SharedStdinChannel {
  controlBuffer: SharedArrayBuffer;
  dataBuffer: SharedArrayBuffer;
  readByte: () => number;
  close: () => void;
}

function createSharedStdinChannel(): SharedStdinChannel {
  const controlBuffer = new SharedArrayBuffer(Int32Array.BYTES_PER_ELEMENT * 3);
  const dataBuffer = new SharedArrayBuffer(STDIN_RING_SIZE);
  const control = new Int32Array(controlBuffer);
  const data = new Uint8Array(dataBuffer);

  return {
    controlBuffer,
    dataBuffer,
    readByte: () => {
      while (true) {
        const read = Atomics.load(control, STDIN_CTRL_READ);
        const write = Atomics.load(control, STDIN_CTRL_WRITE);

        if (read !== write) {
          const byte = data[read] ?? 0;
          Atomics.store(control, STDIN_CTRL_READ, (read + 1) % STDIN_RING_SIZE);
          return byte;
        }

        if (Atomics.load(control, STDIN_CTRL_CLOSED) === 1) {
          return -1;
        }

        Atomics.wait(control, STDIN_CTRL_WRITE, write);
      }
    },
    close: () => {
      Atomics.store(control, STDIN_CTRL_CLOSED, 1);
      Atomics.notify(control, STDIN_CTRL_WRITE, 1);
    },
  };
}

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

function post(msg: WorkerToMainMessage, transfer?: Transferable[]): void {
  if (transfer && transfer.length > 0) {
    self.postMessage(msg, { transfer });
  } else {
    self.postMessage(msg);
  }
}

/**
 * IOProvider that proxies all I/O back to the main thread's xterm.js.
 * The MiniShell uses this instead of a direct TTYBridge.
 */
class WorkerIOProvider implements IOProvider {
  private byteBuffer: number[] = [];
  private byteResolvers: Array<(byte: number) => void> = [];
  private shellSharedStdin: SharedStdinChannel | null = null;

  supportsSynchronousExclusiveStdin = true;

  readByte(): number | null | Promise<number> {
    if (this.byteBuffer.length > 0) {
      return this.byteBuffer.shift()!;
    }
    return new Promise<number>((resolve) => {
      this.byteResolvers.push(resolve);
    });
  }

  /** Called when the main thread sends a keystroke. */
  pushByte(byte: number): void {
    if (this.byteResolvers.length > 0) {
      this.byteResolvers.shift()!(byte);
    } else {
      this.byteBuffer.push(byte);
    }
  }

  writeLine(text: string): void {
    post({ type: 'shellOutput', text });
  }

  write(text: string): void {
    post({ type: 'shellWrite', text });
  }

  writeError(text: string): void {
    post({ type: 'shellOutput', text: `\x1b[31m${text}\x1b[0m` });
  }

  clear(): void {
    post({ type: 'shellClear' });
  }

  setStdinEcho(enabled: boolean): void {
    post({ type: 'shellSetEcho', enabled });
  }

  enterExclusiveStdin(): void {
    if (this.shellSharedStdin || typeof SharedArrayBuffer === 'undefined') {
      return;
    }
    this.shellSharedStdin = createSharedStdinChannel();
    post({
      type: 'shellStdinRequest',
      controlBuffer: this.shellSharedStdin.controlBuffer,
      dataBuffer: this.shellSharedStdin.dataBuffer,
    });
  }

  exitExclusiveStdin(): void {
    this.shellSharedStdin?.close();
    this.shellSharedStdin = null;
    post({ type: 'shellExclusiveStdin', enter: false });
  }

  readByteExclusive(): number {
    return this.shellSharedStdin?.readByte() ?? -1;
  }
}

const workerIO = new WorkerIOProvider();

/* ------------------------------------------------------------------ */
/*  Boot                                                               */
/* ------------------------------------------------------------------ */

async function handleBoot(manifestUrl: string, toolVersions?: { pythonMajorMinor?: string; pythonMajorMinorCompact?: string }): Promise<void> {
  const tBoot = performance.now();
  const ms = (t0: number) => `${(performance.now() - t0).toFixed(1)}ms`;

  console.log(`${P} ===== BOOT START =====`);
  console.log(`${P} Async strategy: ${detectAsyncStrategy()}`);

  // Step 1: Fetch manifest
  const t1 = performance.now();
  const manifest = await fetchManifestWithRetry(manifestUrl);
  // Derive the CDN base URL from the manifest URL's directory.
  // e.g. "https://host/gameguild/cdn/manifest.json" → "https://host/gameguild/cdn"
  // This works regardless of deploy subpath (GitHub Pages, custom domain, etc.)
  const manifestDir = manifestUrl.replace(/\/[^/]*$/, ''); // strip filename
  manifest.baseUrl = manifestDir;

  // Also resolve bundle URLs relative to the manifest directory
  if (manifest.bundles) {
    for (const bundle of Object.values(manifest.bundles)) {
      if (bundle.url && !bundle.url.startsWith('http')) {
        // Bundle URLs like "/cdn/usr/lib/clang.tar.br" need the same treatment.
        // Strip the baseUrl prefix (e.g. "/cdn") from the baked-in URL to get
        // the relative path, then resolve against the manifest directory.
        const bakedBase = '/cdn';
        const relativePath = bundle.url.startsWith(bakedBase) ? bundle.url.slice(bakedBase.length) : bundle.url;
        bundle.url = manifestDir + relativePath;
      }
    }
  }
  const fileCount = Object.keys(manifest.files).length;
  const bundleCount = manifest.bundles ? Object.keys(manifest.bundles).length : 0;
  console.log(`${P} Step 1/6 done: manifest loaded (${fileCount} files, ${bundleCount} bundles, baseUrl=${manifest.baseUrl}) in ${ms(t1)}`);

  // Step 2: Initialize LazyFS
  const t2 = performance.now();
  const lazyFs = new LazyFS(manifest);
  await lazyFs.init();
  console.log(`${P} Step 2/6 done: LazyFS ready in ${ms(t2)}`);

  // Step 3: Initialize writable layers
  const t3 = performance.now();
  const manifestVersion = [
    manifest.baseUrl,
    String(manifest.version),
    manifest.generated,
    String(Object.keys(manifest.files).length),
  ].join(':');
  const writeFs = new IDBFS('overlay-writes', { version: manifestVersion });
  const tmpFs = new IDBFS('tmp-fs', { volatile: true });
  const idbFs = new IDBFS('user-files');
  await writeFs.init();
  await tmpFs.init();
  await idbFs.init();
  console.log(`${P} Step 3/6 done: writable FS layers ready in ${ms(t3)}`);

  // Step 4: Assemble OverlayFS + VFSManager
  const t4 = performance.now();
  const overlay = new OverlayFS(lazyFs, writeFs);
  overlay.mount('/tmp', tmpFs);
  overlay.mount('/home', idbFs);
  await overlay.mkdir('/tmp');
  await overlay.mkdir('/home');
  await overlay.mkdir('/home/user');
  vfs = createVFSManager(overlay, lazyFs);
  console.log(`${P} Step 4/6 done: VFS assembled in ${ms(t4)}`);

  // Step 5: Create runner and shell
  const t5 = performance.now();
  new FetchBridge();

  const versions = {
    pythonMajorMinor: toolVersions?.pythonMajorMinor ?? manifest.toolVersions?.pythonMajorMinor ?? '3.13',
    pythonMajorMinorCompact: toolVersions?.pythonMajorMinorCompact ?? manifest.toolVersions?.pythonMajorMinorCompact ?? '313',
  };
  runner = new ToolRunner(vfs, versions);
  shell = new MiniShell(runner, workerIO, vfs);
  console.log(`${P} Step 5/6 done: all components created in ${ms(t5)}`);

  // Step 6: Start shell (runs the REPL loop asynchronously)
  shell.start();
  console.log(`${P} Step 6/6 done: shell started`);

  console.log(`${P} ===== BOOT COMPLETE in ${ms(tBoot)} =====`);
}

/* ------------------------------------------------------------------ */
/*  Run handler                                                        */
/* ------------------------------------------------------------------ */

async function handleRun(
  id: number,
  tool: string,
  argv: string[],
  options: { env?: Record<string, string>; cwd?: string; wantStdin?: boolean; hints?: { bundlesNeeded?: string[] } },
): Promise<void> {
  if (!runner) {
    post({ type: 'runResult', id, exitCode: 1, stdout: '', stderr: 'Worker not booted' });
    return;
  }

  // Create a stdin provider that waits for bytes from the main thread
  let stdinFn: (() => number | null | Promise<number>) | undefined;
  let sharedStdin: SharedStdinChannel | null = null;
  if (options.wantStdin) {
    if (typeof SharedArrayBuffer !== 'undefined') {
      sharedStdin = createSharedStdinChannel();
      stdinFn = () => sharedStdin!.readByte();
      post({
        type: 'stdinRequest',
        id,
        controlBuffer: sharedStdin.controlBuffer,
        dataBuffer: sharedStdin.dataBuffer,
      });
    } else {
      console.warn(`${P} SharedArrayBuffer unavailable; interactive WASI stdin will be degraded`);
      stdinFn = () => -1;
    }
  }

  const result = await runner.run(tool, argv, {
    env: options.env,
    cwd: options.cwd,
    onStdout: (text) => post({ type: 'stdout', id, text }),
    onStderr: (text) => post({ type: 'stderr', id, text }),
    stdin: stdinFn,
    hints: options.hints,
  });

  sharedStdin?.close();

  post({ type: 'runResult', id, exitCode: result.exitCode, stdout: result.stdout, stderr: result.stderr });
}

/* ------------------------------------------------------------------ */
/*  Message handler                                                    */
/* ------------------------------------------------------------------ */

self.onmessage = async (ev: MessageEvent<MainToWorkerMessage>) => {
  const msg = ev.data;

  switch (msg.type) {
    case 'boot':
      try {
        pageOrigin = msg.origin || '';
        // Pre-load the locally-built brotli decompressor (Emscripten module
        // produced by tools/emception/scripts/build-brotli.ts) and inject it
        // into LazyFS before boot. This is required for browsers (or worker
        // contexts) where DecompressionStream('br') is not available.
        //
        // The brotli build is a MODULARIZE'd ES module (-sMODULARIZE=1
        // -sEXPORT_ES6=1 -sINVOKE_RUN=0): importing it does NOT auto-fetch
        // the .wasm; we pass `wasmBinary` explicitly to the factory.
        try {
          const manifestDir = msg.manifestUrl.replace(/\/[^/]*$/, '');
          const jsUrl = `${manifestDir}/brotli_wasm.js`;
          const wasmCandidate = `${manifestDir}/brotli_wasm.wasm`;

          // Fetch the .wasm bytes (HEAD is unreliable behind SPA dev servers
          // like Vite that return 200 + index.html for unknown paths).
          const wasmResp = await nativeFetch(wasmCandidate, { cache: 'no-store' }).catch(() => null);
          if (!wasmResp || !wasmResp.ok) {
            throw new Error(`Failed to fetch ${wasmCandidate}: HTTP ${wasmResp?.status ?? 'no response'}`);
          }
          const wasmBinary = await wasmResp.arrayBuffer();
          const view = new Uint8Array(wasmBinary);
          if (view.length < 4 || view[0] !== 0x00 || view[1] !== 0x61 || view[2] !== 0x73 || view[3] !== 0x6d) {
            const head = [...view.subarray(0, 4)].map(b => b.toString(16).padStart(2, '0')).join(' ');
            throw new Error(`${wasmCandidate} is not a wasm module (got ${view.length}B starting with ${head})`);
          }

          const jsResp = await nativeFetch(jsUrl, { cache: 'no-store' });
          if (!jsResp.ok) {
            throw new Error(`Failed to fetch ${jsUrl}: HTTP ${jsResp.status}`);
          }
          const jsText = await jsResp.text();
          if (jsText.trimStart().startsWith('<')) {
            throw new Error(`Failed to fetch ${jsUrl}: received HTML instead of JS`);
          }
          const jsBlob = new Blob([jsText], { type: 'text/javascript' });
          const jsBlobUrl = URL.createObjectURL(jsBlob);

          const brotliMod = (await import(/* @vite-ignore */ /* webpackIgnore: true */ jsBlobUrl)) as BrotliModuleExports;
          if (typeof brotliMod.default !== 'function') {
            URL.revokeObjectURL(jsBlobUrl);
            throw new Error(
              `brotli_wasm.js does not export a default factory — rebuild with ` +
              `-sMODULARIZE=1 -sEXPORT_ES6=1 (see tools/emception/scripts/build-brotli.ts).`,
            );
          }
          const brotliModule = await brotliMod.default({
            wasmBinary,
            // Defensive: if the factory ever tries to fetch on its own, point
            // it back at the same URL we already validated.
            locateFile: (p: string) => (p.endsWith('.wasm') ? wasmCandidate : p),
          });
          URL.revokeObjectURL(jsBlobUrl);

          // Wrappers exposed by toolchain/overlays/brotli/brotli-wrapper.c:
          //   uint8_t* brotli_decompress_buffer(const uint8_t* in, size_t in_len, size_t* out_len);
          //   void     brotli_free_buffer(uint8_t* ptr);
          //   const char* brotli_get_last_error_message(void);
          const brotliDecompressBuffer = brotliModule.cwrap('brotli_decompress_buffer', 'number', [
            'number', 'number', 'number',
          ]);
          const brotliFreeBuffer = brotliModule.cwrap('brotli_free_buffer', null, ['number']);
          const brotliGetLastError = brotliModule.cwrap('brotli_get_last_error_message', 'string', []);

          LazyFS.customBrotliDecompressor = (data: Uint8Array): Uint8Array => {
            const inputPtr = brotliModule._malloc(Math.max(data.length, 1));
            const outLenPtr = brotliModule._malloc(4);
            let outputPtr = 0;
            try {
              if (data.length > 0) brotliModule.HEAPU8.set(data, inputPtr);
              brotliModule.HEAPU32[outLenPtr >>> 2] = 0;
              outputPtr = brotliDecompressBuffer(inputPtr, data.length, outLenPtr);
              if (!outputPtr) {
                throw new Error(brotliGetLastError() || 'brotli decompression failed');
              }
              const outLen = brotliModule.HEAPU32[outLenPtr >>> 2] >>> 0;
              // Copy out before freeing; HEAPU8.subarray would alias WASM memory.
              return new Uint8Array(brotliModule.HEAPU8.subarray(outputPtr, outputPtr + outLen));
            } finally {
              if (outputPtr) brotliFreeBuffer(outputPtr);
              brotliModule._free(outLenPtr);
              brotliModule._free(inputPtr);
            }
          };
          console.log(`${P} local brotli (Emscripten) loaded from ${wasmCandidate} and injected into LazyFS`);
        } catch (e) {
          console.warn(`${P} brotli pre-load failed, will rely on DecompressionStream:`, e);
        }
        await handleBoot(msg.manifestUrl, msg.toolVersions);
        post({ type: 'booted' });
      } catch (err) {
        post({ type: 'bootError', error: err instanceof Error ? err.message : String(err) });
      }
      break;

    case 'run':
      handleRun(msg.id, msg.tool, msg.argv, msg.options);
      break;

    case 'stdin': {
      if (msg.id === 0) {
        // Shell input — feed to the Worker IO provider
        workerIO.pushByte(msg.byte);
      }
      break;
    }

    case 'getFile':
      if (!vfs) {
        post({ type: 'getFileResult', id: msg.id, data: null });
        break;
      }
      try {
        const data = await vfs.fetchFile(msg.path);
        if (data) {
          // Transfer the buffer (zero-copy)
          const copy = new Uint8Array(data);
          post({ type: 'getFileResult', id: msg.id, data: copy }, [copy.buffer]);
        } else {
          post({ type: 'getFileResult', id: msg.id, data: null });
        }
      } catch {
        post({ type: 'getFileResult', id: msg.id, data: null });
      }
      break;

    case 'writeFile':
      if (!vfs) {
        post({ type: 'writeFileResult', id: msg.id, ok: false, error: 'Not booted' });
        break;
      }
      try {
        await vfs.overlay.writeFile(msg.path, msg.data);
        post({ type: 'writeFileResult', id: msg.id, ok: true });
      } catch (err) {
        post({ type: 'writeFileResult', id: msg.id, ok: false, error: String(err) });
      }
      break;

    case 'deleteFile':
      if (!vfs) {
        post({ type: 'deleteFileResult', id: msg.id, ok: false, error: 'Not booted' });
        break;
      }
      try {
        const deleted = await vfs.overlay.deleteFile(msg.path);
        if (!deleted) {
          post({ type: 'deleteFileResult', id: msg.id, ok: false, error: `File not found: ${msg.path}` });
        } else {
          post({ type: 'deleteFileResult', id: msg.id, ok: true });
        }
      } catch (err) {
        post({ type: 'deleteFileResult', id: msg.id, ok: false, error: String(err) });
      }
      break;

    case 'listDir':
      if (!vfs) {
        post({ type: 'listDirResult', id: msg.id, entries: [] });
        break;
      }
      try {
        const entries = await vfs.overlay.readdir(msg.path);
        post({ type: 'listDirResult', id: msg.id, entries });
      } catch {
        post({ type: 'listDirResult', id: msg.id, entries: [] });
      }
      break;

    case 'resetVfs':
      if (!vfs) {
        post({ type: 'resetVfsResult', id: msg.id, ok: false, error: 'Not booted' });
        break;
      }
      try {
        console.log(`${P} ===== VFS RESET START =====`);
        const t0 = performance.now();
        // Clear /tmp (volatile IDBFS)
        await vfs.overlay.clearMount('/tmp');
        await vfs.overlay.mkdir('/tmp');
        // Clear /home (persistent IDBFS) — wipes user build artifacts
        await vfs.overlay.clearMount('/home');
        await vfs.overlay.mkdir('/home');
        await vfs.overlay.mkdir('/home/user');
        console.log(`${P} ===== VFS RESET COMPLETE in ${(performance.now() - t0).toFixed(1)}ms =====`);
        post({ type: 'resetVfsResult', id: msg.id, ok: true });
      } catch (err) {
        console.error(`${P} VFS reset failed:`, err);
        post({ type: 'resetVfsResult', id: msg.id, ok: false, error: String(err) });
      }
      break;
  }
};

console.log(`${P} Worker script loaded, waiting for boot message...`);
