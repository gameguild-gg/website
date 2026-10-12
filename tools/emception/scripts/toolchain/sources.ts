import { spawnSync } from 'node:child_process';
import { isUtf8 } from 'node:buffer';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { extract as extractTar } from 'tar';

import type { LockedTool, ToolName, ToolchainConfig, ToolchainLock } from './lock.ts';
import { lockedArchiveHashes } from './lock.ts';
import { toolchainPaths } from './paths.ts';

function sha256File(filename: string): string {
  return createHash('sha256').update(fs.readFileSync(filename)).digest('hex');
}

function walkDirectory(root: string, relative = ''): string[] {
  const directory = path.join(root, relative);
  return fs.readdirSync(directory, { withFileTypes: true })
    .sort((left, right) => left.name.localeCompare(right.name))
    .flatMap((entry) => {
      const child = path.join(relative, entry.name);
      return entry.isDirectory() ? [child, ...walkDirectory(root, child)] : [child];
    });
}

function normalizeTextLineEndings(content: Buffer): Buffer {
  if (!isUtf8(content)) return content;
  return Buffer.from(content.toString('utf8').replace(/\r\n?/g, '\n'));
}

/** Hash a workspace source tree without timestamps or absolute paths. */
export function hashDirectory(root: string): string {
  if (!fs.existsSync(root) || !fs.statSync(root).isDirectory()) {
    throw new Error(`Workspace source directory is missing: ${root}`);
  }
  const hash = createHash('sha256');
  for (const relative of walkDirectory(root)) {
    const absolute = path.join(root, relative);
    const stat = fs.lstatSync(absolute);
    const normalized = relative.split(path.sep).join('/');
    if (stat.isSymbolicLink()) {
      hash.update(`link\0${normalized}\0${fs.readlinkSync(absolute)}\0`);
    } else if (stat.isDirectory()) {
      hash.update(`directory\0${normalized}\0`);
    } else if (stat.isFile()) {
      hash.update(`file\0${normalized}\0`);
      hash.update(normalizeTextLineEndings(fs.readFileSync(absolute)));
      hash.update('\0');
    }
  }
  return hash.digest('hex');
}

function assertGeneratedDestination(root: string, destination: string): void {
  const cache = toolchainPaths(root).cache;
  const relative = path.relative(cache, path.resolve(destination));
  if (!relative || relative.startsWith('..') || path.isAbsolute(relative)) {
    throw new Error(`Source destination must be inside ${cache}: ${destination}`);
  }
}

export function verifyLockedArchive(filename: string, source: Extract<LockedTool['source'], { kind: 'archive' | 'git-archive' }>): string {
  const expected = lockedArchiveHashes(source);
  const actual = sha256File(filename);
  if (!expected.includes(actual)) {
    throw new Error(`Checksum mismatch for ${filename}: expected ${expected.join(' or ')}, got ${actual}`);
  }
  return actual;
}

function downloadLockedArchive(url: string, destination: string, source: Extract<LockedTool['source'], { kind: 'archive' | 'git-archive' }>): string {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  if (fs.existsSync(destination)) {
    return verifyLockedArchive(destination, source);
  }

  const temporary = `${destination}.tmp-${process.pid}`;
  fs.rmSync(temporary, { force: true });
  const result = spawnSync(
    'curl',
    ['-fSL', '--http1.1', '--retry', '8', '--retry-all-errors', '--retry-delay', '2', '-o', temporary, url],
    { stdio: 'inherit' },
  );
  if (result.status !== 0) {
    fs.rmSync(temporary, { force: true });
    throw new Error(`Failed to download locked source: ${url}`);
  }
  try {
    const actualHash = verifyLockedArchive(temporary, source);
    fs.renameSync(temporary, destination);
    return actualHash;
  } catch (error) {
    fs.rmSync(temporary, { force: true });
    throw error;
  }
}

export function extractArchive(
  archive: string,
  destination: string,
  stripComponents = 1,
): void {
  fs.mkdirSync(destination, { recursive: true });
  extractTar({
    file: archive,
    cwd: destination,
    strip: stripComponents,
    sync: true,
    preservePaths: false,
  });
}

/** Materialize an exact lock entry into the disposable source cache. */
export function ensureLockedSource(
  root: string,
  lock: ToolchainLock,
  name: ToolName,
  destination: string,
  keyFile: string,
): string {
  const tool = lock.tools[name];
  if (!tool) throw new Error(`${name} is missing from the toolchain lock`);

  if (tool.source.kind === 'workspace') {
    const workspace = path.resolve(root, tool.source.path);
    const actual = hashDirectory(workspace);
    if (actual !== tool.source.contentHash) {
      throw new Error(`Workspace hash mismatch for ${name}: expected ${tool.source.contentHash}, got ${actual}`);
    }
    return workspace;
  }
  if (tool.source.kind === 'emsdk-component') {
    throw new Error(`${name} is supplied by EMSDK and cannot be extracted independently`);
  }

  assertGeneratedDestination(root, destination);
  const expectedHash = tool.source.sha256;
  const acceptedHashes = lockedArchiveHashes(tool.source);
  const marker = path.join(destination, '.emception-source.json');
  if (fs.existsSync(path.join(destination, keyFile)) && fs.existsSync(marker)) {
    const identity = JSON.parse(fs.readFileSync(marker, 'utf8')) as { name?: string; version?: string; sha256?: string };
    if (identity.name === name && identity.version === tool.version && identity.sha256 && acceptedHashes.includes(identity.sha256)) return destination;
  }

  const paths = toolchainPaths(root);
  const archive = path.join(paths.downloads, `${name}-${expectedHash}.archive`);
  const actualHash = downloadLockedArchive(tool.source.url, archive, tool.source);

  const temporary = `${destination}.extract-${process.pid}`;
  fs.rmSync(temporary, { recursive: true, force: true });
  try {
    extractArchive(archive, temporary);
    if (!fs.existsSync(path.join(temporary, keyFile))) {
      throw new Error(`Failed to extract locked source ${name}@${tool.version}`);
    }
    fs.writeFileSync(
      path.join(temporary, '.emception-source.json'),
      `${JSON.stringify({ name, version: tool.version, sha256: actualHash }, null, 2)}\n`,
    );
    fs.rmSync(destination, { recursive: true, force: true });
    fs.renameSync(temporary, destination);
  } catch (error) {
    fs.rmSync(temporary, { recursive: true, force: true });
    throw error;
  }
  return destination;
}

export interface ToolSourceProvider {
  resolve(name: ToolName, requested: string, current?: LockedTool): Promise<LockedTool>;
  inspectEmsdk(emsdk: LockedTool): Promise<Partial<Record<ToolName, LockedTool>>>;
  latestVersion(name: ToolName, current: LockedTool): Promise<string>;
}

export interface OutdatedTool {
  name: ToolName;
  current: string;
  latest: string;
}

function cloneLock(lock: ToolchainLock): ToolchainLock {
  return structuredClone(lock);
}

function isToolName(value: string): value is ToolName {
  return [
    'emsdk', 'llvm', 'binaryen', 'python', 'sdl3', 'cmake', 'brotli', 'imgui',
    'raylib', 'raygui', 'physac', 'allegro', 'curlLite', 'zstdWindows', 'msys2Make',
  ].includes(value);
}

async function updateOne(
  config: ToolchainConfig,
  lock: ToolchainLock,
  name: ToolName,
  requested: string,
  provider: ToolSourceProvider,
): Promise<void> {
  const current = lock.tools[name];
  if (!current) throw new Error(`unknown tool: ${name}`);
  if (current.derivedFrom === 'emsdk' || config.emsdkGroup.includes(name)) {
    throw new Error(`${name} is controlled by emsdk; update emsdk instead`);
  }

  const resolved = await provider.resolve(name, requested, current);
  lock.tools[name] = resolved;

  if (name !== 'emsdk') return;
  const components = await provider.inspectEmsdk(resolved);
  for (const componentName of config.emsdkGroup) {
    const component = components[componentName];
    if (!component) throw new Error(`EMSDK inspection did not resolve ${componentName}`);
    if (component.derivedFrom !== 'emsdk') {
      throw new Error(`EMSDK component ${componentName} must declare derivedFrom=emsdk`);
    }
    lock.tools[componentName] = component;
  }
}

export async function planToolchainUpdate(
  config: ToolchainConfig,
  input: ToolchainLock,
  target: ToolName | 'all',
  requested: string,
  provider: ToolSourceProvider,
): Promise<ToolchainLock> {
  const lock = cloneLock(input);
  if (target !== 'all') {
    await updateOne(config, lock, target, requested, provider);
    return lock;
  }

  const independent = Object.entries(lock.tools)
    .filter(([, tool]) => !tool.derivedFrom && tool.source.kind !== 'workspace')
    .map(([name]) => name)
    .filter(isToolName)
    .sort((left, right) => {
      if (left === 'emsdk') return -1;
      if (right === 'emsdk') return 1;
      return left.localeCompare(right);
    });
  for (const name of independent) {
    await updateOne(config, lock, name, requested, provider);
  }
  return lock;
}

export async function findOutdatedTools(
  _config: ToolchainConfig,
  lock: ToolchainLock,
  provider: ToolSourceProvider,
  target: ToolName | 'all' = 'all',
): Promise<OutdatedTool[]> {
  const candidates = Object.entries(lock.tools)
    .filter(([name, tool]) => (target === 'all' || name === target) && !tool.derivedFrom && tool.source.kind !== 'workspace')
    .map(([name, tool]) => [name as ToolName, tool] as const)
    .sort(([left], [right]) => left.localeCompare(right));
  const result: OutdatedTool[] = [];
  for (const [name, tool] of candidates) {
    const latest = await provider.latestVersion(name, tool);
    if (latest !== tool.version) result.push({ name, current: tool.version, latest });
  }
  return result;
}
