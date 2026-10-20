/**
 * Overlay filesystem: routes reads/writes to mounted backends with write-through.
 */

import { FSStats, IFileSystem } from './interface.js';

export class OverlayFS implements IFileSystem {
  private mounts = new Map<string, IFileSystem>();
  private writeLayer: IFileSystem;
  private defaultFs: IFileSystem;

  constructor(defaultFs: IFileSystem, writeLayer: IFileSystem) {
    this.defaultFs = defaultFs;
    this.writeLayer = writeLayer;
  }

  mount(path: string, fs: IFileSystem): void {
    this.mounts.set(this.normalizePath(path), fs);
  }

  /** Clear all data in a mounted filesystem (e.g. /tmp, /home). */
  async clearMount(path: string): Promise<void> {
    const normalized = this.normalizePath(path);
    const fs = this.mounts.get(normalized);
    if (fs && typeof fs.clear === 'function') {
      await fs.clear();
    }
  }

  private getFs(path: string): { fs: IFileSystem; relativePath: string } {
    const normalized = this.normalizePath(path);
    let bestMatch = '';
    let bestFs = this.defaultFs;
    for (const [mountPoint, fs] of this.mounts) {
      if ((normalized === mountPoint || normalized.startsWith(mountPoint + '/')) && mountPoint.length > bestMatch.length) {
        bestMatch = mountPoint;
        bestFs = fs;
      }
    }
    const relativePath = bestMatch ? normalized.slice(bestMatch.length) || '/' : normalized;
    return { fs: bestFs, relativePath };
  }

  async readFile(path: string): Promise<Uint8Array | null> {
    const writeResult = await this.writeLayer.readFile(path);
    if (writeResult) return writeResult;
    const { fs, relativePath } = this.getFs(path);
    return fs.readFile(relativePath);
  }

  async writeFile(path: string, data: Uint8Array): Promise<boolean> {
    const { fs, relativePath } = this.getFs(path);
    const wrote = await fs.writeFile(relativePath, data);
    if (!wrote) {
      return this.writeLayer.writeFile(path, data);
    }
    return true;
  }

  async exists(path: string): Promise<boolean> {
    if (await this.writeLayer.exists(path)) return true;
    const { fs, relativePath } = this.getFs(path);
    return fs.exists(relativePath);
  }

  async stat(path: string): Promise<FSStats | null> {
    const writeStat = await this.writeLayer.stat(path);
    if (writeStat) return writeStat;
    const { fs, relativePath } = this.getFs(path);
    return fs.stat(relativePath);
  }

  async readdir(path: string): Promise<string[]> {
    const normalized = this.normalizePath(path);
    const { fs, relativePath } = this.getFs(path);
    const entries = new Set(await fs.readdir(relativePath));
    const writeEntries = await this.writeLayer.readdir(normalized);
    for (const e of writeEntries) entries.add(e);
    const prefix = normalized === '/' ? '/' : normalized + '/';
    for (const mountPoint of this.mounts.keys()) {
      if (mountPoint.startsWith(prefix) && mountPoint !== normalized) {
        const relative = mountPoint.slice(prefix.length);
        if (!relative.includes('/')) entries.add(relative);
      }
    }
    return [...entries];
  }

  async deleteFile(path: string): Promise<boolean> {
    const { fs, relativePath } = this.getFs(path);
    return fs.deleteFile(relativePath);
  }

  async mkdir(path: string): Promise<boolean> {
    const { fs, relativePath } = this.getFs(path);
    // If the entry already exists in the target FS, treat as success.
    // Without this check, the writeLayer fallback creates full-path entries
    // that leak into mount-relative readdir results, causing phantom nested
    // directories (e.g. /tmp/tmp/tmp/...).
    if (await fs.exists(relativePath)) return true;
    const ok = await fs.mkdir(relativePath);
    if (!ok) return this.writeLayer.mkdir(path);
    return true;
  }

  // ---------- Synchronous accessors ----------
  // These check the IDBFS write cache and LazyFS manifest metadata.
  // LazyFS no longer caches file data in RAM — readFileSync returns null
  // for CDN files; the async Asyncify hooks handle on-demand loading.
  // Used by the Emscripten VFSFS for fast-path bypass when data is available.

  /** Sync helper: check if FS supports a sync method */

  private hasSyncMethod<K extends keyof IFileSystem>(
    fs: IFileSystem,
    method: K,
  ): fs is IFileSystem & Required<Pick<IFileSystem, K>> {
    return typeof fs[method] === 'function';
  }

  readFileSync(path: string): Uint8Array | null {
    // Check write layer first
    if (this.hasSyncMethod(this.writeLayer, 'readFileSync')) {
      const data = this.writeLayer.readFileSync(path);
      if (data) return data;
    }
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'readFileSync')) {
      return fs.readFileSync(relativePath);
    }
    return null;
  }

  writeFileSync(path: string, data: Uint8Array): void {
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'writeFileSync')) {
      fs.writeFileSync(relativePath, data);
    } else if (this.hasSyncMethod(this.writeLayer, 'writeFileSync')) {
      this.writeLayer.writeFileSync(path, data);
    }
  }

  statSync(path: string): FSStats | null {
    // Check write layer first
    if (this.hasSyncMethod(this.writeLayer, 'statSync')) {
      const s = this.writeLayer.statSync(path);
      if (s) return s;
    }
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'statSync')) {
      return fs.statSync(relativePath);
    }
    return null;
  }

  readdirSync(path: string): string[] {
    const normalized = this.normalizePath(path);
    const entries = new Set<string>();

    // Get entries from the routed FS
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'readdirSync')) {
      for (const e of fs.readdirSync(relativePath)) entries.add(e);
    }

    // Merge write layer entries
    if (this.hasSyncMethod(this.writeLayer, 'readdirSync')) {
      for (const e of this.writeLayer.readdirSync(normalized)) entries.add(e);
    }

    // Add mount point children
    const prefix = normalized === '/' ? '/' : normalized + '/';
    for (const mountPoint of this.mounts.keys()) {
      if (mountPoint.startsWith(prefix) && mountPoint !== normalized) {
        const relative = mountPoint.slice(prefix.length);
        if (!relative.includes('/')) entries.add(relative);
      }
    }

    return [...entries];
  }

  existsSync(path: string): boolean {
    if (this.hasSyncMethod(this.writeLayer, 'existsSync')) {
      if (this.writeLayer.existsSync(path)) return true;
    }
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'existsSync')) {
      return fs.existsSync(relativePath);
    }
    return false;
  }

  mkdirSync(path: string): void {
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'mkdirSync')) {
      fs.mkdirSync(relativePath);
    } else if (this.hasSyncMethod(this.writeLayer, 'mkdirSync')) {
      this.writeLayer.mkdirSync(path);
    }
  }

  deleteFileSync(path: string): boolean {
    const { fs, relativePath } = this.getFs(path);
    if (this.hasSyncMethod(fs, 'deleteFileSync')) {
      return fs.deleteFileSync(relativePath);
    }
    return false;
  }

  private normalizePath(path: string): string {
    const parts = path.split('/').filter((p) => p && p !== '.');
    const result: string[] = [];
    for (const part of parts) {
      if (part === '..') result.pop();
      else result.push(part);
    }
    return '/' + result.join('/');
  }
}
