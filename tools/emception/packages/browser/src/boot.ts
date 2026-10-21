/**
 * Boot sequence: load manifest, init VFS, start tool runner and shell.
 */

import { TTYBridge } from '@gameguild/emception-xterm';
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

export interface BootResult {
  runner: ToolRunner;
  vfs: ReturnType<typeof createVFSManager>;
  shell: MiniShell;
  tty: TTYBridge;
}

export async function boot(manifestUrl: string, terminalContainerOrTerminal: HTMLElement | import('@xterm/xterm').Terminal): Promise<BootResult> {
  const tBoot = performance.now();
  const P = '[Emception:Boot]';
  const ms = (t0: number) => `${(performance.now() - t0).toFixed(1)}ms`;

  console.log(`${P} ===== BOOT START =====`);
  console.log(`${P} Async strategy: ${detectAsyncStrategy()}`);

  // Step 1: Fetch manifest
  const t1 = performance.now();
  console.log(`${P} Step 1/6: Fetching manifest from ${manifestUrl}...`);
  const manifest = await fetchManifestWithRetry(manifestUrl);
  // Derive the CDN base URL from the manifest URL's directory.
  // e.g. "https://host/gameguild/cdn/manifest.json" → "https://host/gameguild/cdn"
  const manifestDir = manifestUrl.replace(/\/[^/]*$/, '');
  manifest.baseUrl = manifestDir;

  // Resolve bundle URLs relative to the manifest directory
  if (manifest.bundles) {
    for (const bundle of Object.values(manifest.bundles)) {
      if (bundle.url && !bundle.url.startsWith('http')) {
        const bakedBase = '/cdn';
        const relativePath = bundle.url.startsWith(bakedBase)
          ? bundle.url.slice(bakedBase.length)
          : bundle.url;
        bundle.url = manifestDir + relativePath;
      }
    }
  }
  const fileCount = Object.keys(manifest.files).length;
  const bundleCount = Object.keys(manifest.bundles || {}).length;
  console.log(`${P} Step 1/6 done: manifest loaded (${fileCount} files, ${bundleCount} bundles, baseUrl=${manifest.baseUrl}) in ${ms(t1)}`);

  // Step 2: Initialize LazyFS (CDN cache)
  const t2 = performance.now();
  console.log(`${P} Step 2/6: Initializing LazyFS (CDN + IDB cache)...`);
  const lazyFs = new LazyFS(manifest);
  await lazyFs.init();
  console.log(`${P} Step 2/6 done: LazyFS ready in ${ms(t2)}`);

  // Step 3: Initialize writable layers (all IDBFS)
  const t3 = performance.now();
  const manifestVersion = [
    manifest.baseUrl,
    String(manifest.version),
    manifest.generated,
    String(fileCount),
  ].join(':');
  console.log(`${P} Step 3/6: Initializing IDBFS layers (version=${manifestVersion})...`);
  const writeFs = new IDBFS('overlay-writes', { version: manifestVersion }); // persistent write-layer with version invalidation
  const tmpFs = new IDBFS('tmp-fs', { volatile: true });                     // volatile /tmp — ephemeral scratch space
  const idbFs = new IDBFS('user-files');                                     // persistent /home
  await writeFs.init();
  await tmpFs.init();
  await idbFs.init();
  console.log(`${P} Step 3/6 done: writable FS layers ready in ${ms(t3)}`);

  // Step 4: Assemble OverlayFS + VFSManager
  const t4 = performance.now();
  console.log(`${P} Step 4/6: Assembling OverlayFS + VFSManager...`);
  const overlay = new OverlayFS(lazyFs, writeFs);
  overlay.mount('/tmp', tmpFs);
  overlay.mount('/home', idbFs);
  await overlay.mkdir('/tmp');
  await overlay.mkdir('/home');
  await overlay.mkdir('/home/user');
  const vfs = createVFSManager(overlay, lazyFs);
  console.log(`${P} Step 4/6 done: VFS assembled in ${ms(t4)}`);

  // Step 5: Create runner, TTY, shell
  const t5 = performance.now();
  console.log(`${P} Step 5/6: Creating ToolRunner, TTYBridge, MiniShell...`);
  new FetchBridge();

  const runner = new ToolRunner(vfs, {
    pythonMajorMinor: manifest.toolVersions?.pythonMajorMinor ?? '3.13',
    pythonMajorMinorCompact: manifest.toolVersions?.pythonMajorMinorCompact ?? '313',
  });
  const isTerminalInstance = 'writeln' in terminalContainerOrTerminal && typeof terminalContainerOrTerminal.writeln === 'function';
  console.log(`${P}   TTYBridge: reusing existing Terminal=${isTerminalInstance}`);
  const tty = new TTYBridge(terminalContainerOrTerminal);
  const shell = new MiniShell(runner, tty, vfs);
  console.log(`${P} Step 5/6 done: all components created in ${ms(t5)}`);

  // Step 6: Start shell
  const t6 = performance.now();
  console.log(`${P} Step 6/6: Starting shell...`);
  shell.start();
  console.log(`${P} Step 6/6 done: shell started in ${ms(t6)}`);

  console.log(`${P} ===== BOOT COMPLETE in ${ms(tBoot)} =====`);

  return { runner, vfs, shell, tty };
}

/* ------------------------------------------------------------------ */
/*  Worker-based boot                                                  */
/* ------------------------------------------------------------------ */

export interface WorkerBootResult {
  client: import('./worker-client.js').WorkerClient;
  tty: TTYBridge;
}

/**
 * Boot the toolchain inside a Web Worker.
 * The main thread only handles terminal I/O — all WASM execution
 * happens off the main thread so the UI never freezes.
 */
export async function bootInWorker(
  manifestUrl: string,
  terminalContainerOrTerminal: HTMLElement | import('@xterm/xterm').Terminal,
): Promise<WorkerBootResult> {
  const P = '[Emception:Boot]';
  const t0 = performance.now();
  const ms = (t: number) => `${(performance.now() - t).toFixed(1)}ms`;

  console.log(`${P} ===== BOOT (Worker mode) START =====`);

  // Create the terminal (stays on main thread)
  const isTerminalInstance = 'writeln' in terminalContainerOrTerminal && typeof terminalContainerOrTerminal.writeln === 'function';
  console.log(`${P} TTYBridge: reusing existing Terminal=${isTerminalInstance}`);
  const tty = new TTYBridge(terminalContainerOrTerminal);

  // Create the Worker.
  // Next.js / webpack: `new Worker(new URL(...), { type: 'module' })` triggers
  // the bundler's worker plugin to create a separate entry point.
  const worker = new Worker(
    new URL('./worker-entry', import.meta.url),
    { type: 'module', name: 'emception-toolchain' },
  );

  // Create the client proxy
  const { WorkerClient } = await import('./worker-client.js');
  const client = new WorkerClient(worker, tty);

  // Resolve to absolute URL — Workers may not share the page's base URL,
  // so relative paths like "/cdn/manifest.json" would fail in fetch().
  const absoluteManifestUrl = new URL(manifestUrl, self.location.href).href;

  // Boot inside the Worker
  console.log(`${P} Sending boot message to Worker...`);
  await client.boot(absoluteManifestUrl);
  console.log(`${P} ===== BOOT (Worker mode) COMPLETE in ${ms(t0)} =====`);

  // Forward terminal keystrokes to the Worker (for the shell REPL).
  // We hook at the xterm.js level: every character the user types gets
  // sent to the Worker as individual bytes.  The Worker's IOProvider
  // queues them for the MiniShell REPL.
  // When exclusive stdin is active (a WASI program is reading stdin),
  // keystrokes are routed through TTYBridge.readByteExclusive → feedStdin
  // instead, so we must NOT also send them to the shell (id: 0).
  tty.onData((data: string) => {
    if (tty.isExclusiveStdin) return;
    for (let i = 0; i < data.length; i++) {
      worker.postMessage({ type: 'stdin', id: 0, byte: data.charCodeAt(i) });
    }
  });

  return { client, tty };
}

export { LineBuffer } from 'emception';
export type { IOProvider } from 'emception';
export {
  createEmception,
  type BrowserEmceptionAPI,
  type BrowserRunOptions,
  type BrowserStdin,
  type CreateEmceptionOptions,
} from './createEmception.js';
export type { BrowserEmceptionAPI as EmceptionAPI } from './createEmception.js';
export { createBrowserBridge, SUBPROCESS_SHIM, type BrowserBridge } from './emscripten/index.js';
export { decompressBrotli, isBrotliSupported } from './loader/brotli.js';
export { clearModuleCache, loadModuleFactory } from './loader/wasm-module.js';
export type { RunOptions, ToolResult } from './tool-runner.js';
export type { VFSManager } from './vfs/index.js';
export { createVFSManager, detectAsyncStrategy, MiniShell, ToolRunner, TTYBridge };

