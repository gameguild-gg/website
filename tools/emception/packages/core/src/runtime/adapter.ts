/**
 * RuntimeAdapter.
 *
 * Plugs platform-specific primitives (worker spawn, manifest fetch, workspace
 * persistence, transferable detection) into the runtime-agnostic
 * `@emception/core` so the same orchestration code can run in browsers
 * (`@emception/browser`).
 *
 * The browser adapter wraps `Worker` + `fetch` + IndexedDB; the Node adapter
 * wraps `worker_threads` + `node:fs` + a fs-backed workspace store.
 *
 * Pure types — no runtime dependencies.
 */

import type { FSManifest } from '../vfs/manifest.js';

interface WorkerEvents {
  message: { data: unknown };
  error: { error?: unknown; message?: string };
}

/** Minimal worker handle abstraction shared across runtimes. */
export interface WorkerHandle {
  postMessage(message: unknown, transfer?: Transferable[]): void;
  terminate(): void | Promise<void>;
  addEventListener(type: 'message', listener: (ev: { data: unknown }) => void): void;
  addEventListener(type: 'error', listener: (ev: { error?: unknown; message?: string }) => void): void;
  removeEventListener<K extends keyof WorkerEvents>(type: K, listener: (ev: WorkerEvents[K]) => void): void;
}

export interface SpawnWorkerOptions {
  /** Optional human-readable name for diagnostics. */
  name?: string;
  /** Workspace cwd or seed dir for the worker, if the runtime needs one. */
  workspaceRoot?: string;
}

export interface ManifestSource {
  /** Browser: fetch from URL. */
  url?: string;
  /** Node: read from disk. */
  path?: string;
}

export interface WorkspaceStoreOptions {
  /** Workspace identifier (e.g. `'assignment-42'`). */
  name: string;
  /** Persistence backend hint. Adapters may override. */
  kind?: 'idb' | 'fs' | 'memory';
  /** When `kind === 'fs'`, the root directory for workspaces. */
  fsRoot?: string;
  /** When `kind === 'idb'`, the IndexedDB namespace (defaults to `'emception'`). */
  idbNamespace?: string;
}

/**
 * Opaque workspace store handle. Concrete shape lives in the runtime adapter
 * package (`@emception/browser` IDB implementation, fs
 * implementation). Core never touches this directly — it is passed through
 * to the worker entry which knows how to consume it.
 */
export interface WorkspaceStoreHandle {
  readonly name: string;
  readonly kind: 'idb' | 'fs' | 'memory';
  /** Adapter-specific resource (e.g. open IDB connection or fs path). */
  readonly resource: unknown;
  close(): Promise<void>;
}

export interface RuntimeAdapter {
  /** Human-readable runtime label (e.g. `'browser'`, `'node'`). */
  readonly name: string;

  /**
   * Spawn a worker that runs the toolchain. The entry module is adapter-defined
   * (browser: a Worker URL; Node: a `worker_threads` script path).
   */
  spawnWorker(opts?: SpawnWorkerOptions): Promise<WorkerHandle>;

  /**
   * Resolve and load a manifest from either a URL (browser) or a disk path
   * (Node). Adapters MAY accept both and pick the right one for the platform.
   */
  loadManifest(source: ManifestSource): Promise<FSManifest>;

  /**
   * Open (or create) a workspace store. The returned handle is passed to the
   * worker so it can read/write files for that workspace.
   */
  openWorkspaceStore(opts: WorkspaceStoreOptions): Promise<WorkspaceStoreHandle>;

  /**
   * Inspect a value and return any `Transferable` objects the adapter wants to
   * transfer (zero-copy) when posting to a worker. Browser: ArrayBuffer,
   * MessagePort, OffscreenCanvas. Node: MessagePort.
   */
  transferable(value: unknown): Transferable[];

  /**
   * True iff this runtime supports SharedArrayBuffer (browsers require COOP/COEP).
   * Used by Asyncify strategy detection.
   */
  hasSharedArrayBuffer(): boolean;
}
