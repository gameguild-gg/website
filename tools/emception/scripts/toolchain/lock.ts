import { createHash } from 'node:crypto';
import { readFile, rename, rm, writeFile } from 'node:fs/promises';

import { toolchainPaths } from './paths.ts';

export type ToolName =
  | 'emsdk'
  | 'llvm'
  | 'binaryen'
  | 'python'
  | 'sdl3'
  | 'cmake'
  | 'brotli'
  | 'imgui'
  | 'raylib'
  | 'raygui'
  | 'physac'
  | 'allegro'
  | 'curlLite'
  | 'zstdWindows'
  | 'msys2Make';

export interface ReviewedArchiveVariant {
  sha256: string;
  gitTree: string;
  review: string;
}

export type LockedSource =
  | { kind: 'archive'; url: string; sha256: string }
  | { kind: 'git-archive'; repository: string; commit: string; url: string; sha256: string; reviewedArchiveVariants?: ReviewedArchiveVariant[] }
  | { kind: 'emsdk-component'; emsdkVersion: string; revision: string; contentHash: string }
  | { kind: 'workspace'; path: string; contentHash: string };

export interface LockedTool {
  version: string;
  derivedFrom?: ToolName;
  source: LockedSource;
}

export interface ToolchainLock {
  schemaVersion: 1;
  configHash: string;
  tools: Record<string, LockedTool>;
}

export interface ToolchainConfig {
  schemaVersion: 1;
  runtimeAbi: string;
  constraints: { cmake: '<4' };
  emsdkGroup: readonly ToolName[];
  channels?: Record<string, unknown>;
  overlays?: Record<string, string>;
}

function sortDeep(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(sortDeep);
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, entry]) => [key, sortDeep(entry)]),
    );
  }
  return value;
}

function canonicalJson(value: unknown): string {
  return JSON.stringify(sortDeep(value));
}

export function calculateConfigHash(config: ToolchainConfig): string {
  return createHash('sha256').update(canonicalJson(config)).digest('hex');
}

export function serializeToolchainLock(lock: ToolchainLock): string {
  return `${JSON.stringify(sortDeep(lock), null, 2)}\n`;
}

function assertHash(value: unknown, label: string): asserts value is string {
  if (typeof value !== 'string' || !/^[0-9a-f]{64}$/.test(value)) {
    throw new Error(`${label} must be a lowercase SHA-256 hash`);
  }
}

function validateSource(name: string, source: LockedSource): void {
  if (!source || typeof source !== 'object' || typeof source.kind !== 'string') {
    throw new Error(`${name}.source is missing`);
  }
  if ('reviewedArchiveVariants' in source && source.kind !== 'git-archive') {
    throw new Error(`${name}.source.reviewedArchiveVariants requires a pinned git-archive`);
  }
  if (source.kind === 'archive') {
    if (!source.url) throw new Error(`${name}.source.url is missing`);
    assertHash(source.sha256, `${name}.source.sha256`);
    return;
  }
  if (source.kind === 'git-archive') {
    if (!source.repository || !source.url) throw new Error(`${name}.source repository or URL is missing`);
    if (!/^[0-9a-f]{40}$/.test(source.commit)) throw new Error(`${name}.source.commit must be immutable`);
    assertHash(source.sha256, `${name}.source.sha256`);
    if ('reviewedArchiveVariants' in source) {
      const variants = source.reviewedArchiveVariants;
      const label = `${name}.source.reviewedArchiveVariants`;
      if (!Array.isArray(variants) || variants.length === 0) throw new Error(`${label} must be a nonempty reviewed list`);
      const hashes = new Set([source.sha256]);
      const trees = new Set<string>();
      for (const variant of variants) {
        if (!variant || typeof variant !== 'object') throw new Error(`${label} entries must contain review evidence`);
        assertHash(variant.sha256, `${label}.sha256`);
        if (hashes.has(variant.sha256)) throw new Error(`${label} hashes must be unique`);
        if (typeof variant.gitTree !== 'string' || !/^[0-9a-f]{40}$/.test(variant.gitTree)) {
          throw new Error(`${label}.gitTree must identify the reviewed immutable Git tree`);
        }
        if (typeof variant.review !== 'string' || !variant.review.trim()) throw new Error(`${label}.review is missing`);
        hashes.add(variant.sha256);
        trees.add(variant.gitTree);
      }
      if (trees.size !== 1) throw new Error(`${label} must refer to the same reviewed Git tree`);
    }
    return;
  }
  if (source.kind === 'emsdk-component') {
    if (!source.emsdkVersion || !source.revision) throw new Error(`${name}.source EMSDK identity is missing`);
    assertHash(source.contentHash, `${name}.source.contentHash`);
    return;
  }
  if (source.kind === 'workspace') {
    if (!source.path) throw new Error(`${name}.source.path is missing`);
    assertHash(source.contentHash, `${name}.source.contentHash`);
    return;
  }
  throw new Error(`${name}.source.kind is unsupported`);
}

export function lockedArchiveHashes(source: Extract<LockedSource, { kind: 'archive' | 'git-archive' }>): readonly string[] {
  validateSource('locked archive', source);
  return [source.sha256, ...(source.kind === 'git-archive' ? source.reviewedArchiveVariants?.map((variant) => variant.sha256) ?? [] : [])];
}

/** Release provenance describes every reviewed lock identity, rather than labelling a variant as the primary archive. */
export function lockedSourceProvenance(lock: ToolchainLock) {
  return Object.fromEntries(Object.entries(lock.tools).map(([name, tool]) => {
    validateSource(name, tool.source);
    const source = tool.source;
    const sha256 = source.kind === 'archive' || source.kind === 'git-archive' ? source.sha256 : source.contentHash;
    const revision = source.kind === 'git-archive' ? source.commit : source.kind === 'emsdk-component' ? source.revision : undefined;
    return [name, { version: tool.version, ...(revision ? { revision } : {}), sha256,
      ...(source.kind === 'git-archive' && source.reviewedArchiveVariants
        ? { reviewedArchiveVariants: source.reviewedArchiveVariants.map((variant) => ({ ...variant })) } : {}) }];
  }));
}

export function validateToolchainState(config: ToolchainConfig, lock: ToolchainLock): void {
  if (config.schemaVersion !== 1 || lock.schemaVersion !== 1) {
    throw new Error('unsupported toolchain config or lock schema');
  }
  const expectedConfigHash = calculateConfigHash(config);
  if (lock.configHash !== expectedConfigHash) {
    throw new Error(`toolchain lock configHash mismatch: expected ${expectedConfigHash}, got ${lock.configHash}`);
  }
  assertHash(lock.configHash, 'toolchain lock configHash');

  for (const [name, tool] of Object.entries(lock.tools)) {
    if (!tool || typeof tool.version !== 'string' || tool.version.length === 0) {
      throw new Error(`${name}.version is missing`);
    }
    validateSource(name, tool.source);
    if (tool.derivedFrom && !lock.tools[tool.derivedFrom]) {
      throw new Error(`${name}.derivedFrom references absent tool ${tool.derivedFrom}`);
    }
  }

  const cmake = lock.tools.cmake;
  if (!cmake) throw new Error('cmake is missing from toolchain lock');
  if (config.constraints.cmake === '<4' && Number.parseInt(cmake.version, 10) >= 4) {
    throw new Error(`CMake ${cmake.version} violates configured constraint <4`);
  }

  for (const name of config.emsdkGroup) {
    const tool = lock.tools[name];
    if (!tool) throw new Error(`${name} is missing from the EMSDK group lock`);
    if (tool.derivedFrom !== 'emsdk') throw new Error(`${name} must derive from emsdk`);
  }
}

export async function loadToolchainState(root: string = process.cwd(), lockOverride?: string) {
  const paths = toolchainPaths(root);
  const lockFile = lockOverride ?? process.env.EMCEPTION_TOOLCHAIN_LOCK ?? paths.lockFile;
  const [configSource, lockSource] = await Promise.all([
    readFile(paths.configFile, 'utf8'),
    readFile(lockFile, 'utf8'),
  ]);
  const config = JSON.parse(configSource) as ToolchainConfig;
  const lock = JSON.parse(lockSource) as ToolchainLock;
  validateToolchainState(config, lock);
  return { config, lock, paths } as const;
}

export async function writeToolchainLockAtomic(
  root: string,
  config: ToolchainConfig,
  lock: ToolchainLock,
): Promise<void> {
  validateToolchainState(config, lock);
  const paths = toolchainPaths(root);
  const temporary = `${paths.lockFile}.tmp-${process.pid}`;
  try {
    await writeFile(temporary, serializeToolchainLock(lock), { encoding: 'utf8', flag: 'wx' });
    await rename(temporary, paths.lockFile);
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
}
