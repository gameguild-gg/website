import { AbortError } from './errors';
import { Memory } from './memory';
import { assert, ESUCCESS, getImportObject } from './shared';

// Import WebAssembly module directly - webpack will handle the path
import memfsWasmUrl from '../../../../../../public/assets/memfs.wasm';
const memfsUrl = memfsWasmUrl;

interface MemFSOptions {
  hostWrite: (message: string) => void;
  stdinStr?: string;
}

interface MemFSExports extends WebAssembly.Exports {
  memory: WebAssembly.Memory;
  init(): void;
  GetPathBuf(): number;
  AddDirectoryNode(pathLength: number): void;
  AddFileNode(pathLength: number, fileLength: number): number;
  GetFileNodeAddress(inode: number): number;
  GetFileNodeSize(inode: number): number;
  FindNode(pathLength: number): number;
}

export class MemFS {
  private hostWrite: (message: string) => void;
  private stdinStr: string;
  private stdinStrPos: number;
  private hostMem_: Memory | null;
  private exports!: MemFSExports;
  private mem!: Memory;
  public ready: Promise<void>;
  private initialized: boolean = false;

  constructor(options: MemFSOptions) {
    if (!options.hostWrite || typeof options.hostWrite !== 'function') {
      throw new Error('MemFS requires a valid hostWrite function');
    }

    this.hostWrite = options.hostWrite;
    this.stdinStr = options.stdinStr || '';
    this.stdinStrPos = 0;
    this.hostMem_ = null;

    const env = getImportObject(this, ['abort', 'host_write', 'host_read', 'memfs_log', 'copy_in', 'copy_out']);

    this.ready = (async () => {
      try {
        const response = await fetch(memfsUrl);
        const buffer = await response.arrayBuffer();
        const module = await WebAssembly.compile(buffer);
        const instance = await WebAssembly.instantiate(module, { env });
        
        this.exports = instance.exports as MemFSExports;
        this.mem = new Memory(this.exports.memory);
        this.exports.init();
        this.initialized = true;
      } catch (error) {
        this.hostWrite(`MemFS initialization error: ${error}\n`);
        throw error;
      }
    })();
  }

  get wasmExports(): MemFSExports {
    if (!this.initialized) {
      throw new Error('MemFS not initialized');
    }
    return this.exports;
  }

  set hostMem(mem: Memory) {
    this.hostMem_ = mem;
  }

  setStdinStr(str: string): void {
    this.stdinStr = str;
    this.stdinStrPos = 0;
  }

  writeToHost(message: string): void {
    this.hostWrite(message);
  }

  async ensureInitialized(): Promise<void> {
    if (!this.initialized) {
      await this.ready;
    }
  }

  addDirectory(path: string): void {
    if (!this.initialized) {
      throw new Error('MemFS not initialized');
    }
    this.mem.check();
    this.mem.write(this.exports.GetPathBuf(), path);
    this.exports.AddDirectoryNode(path.length);
  }

  addFile(path: string, contents: ArrayBuffer | Uint8Array | string): void {
    if (!this.initialized) {
      throw new Error('MemFS not initialized');
    }
    const length = contents instanceof ArrayBuffer ? contents.byteLength : contents.length;
    this.mem.check();
    this.mem.write(this.exports.GetPathBuf(), path);
    const inode = this.exports.AddFileNode(path.length, length);
    const addr = this.exports.GetFileNodeAddress(inode);
    this.mem.check();
    this.mem.write(addr, contents);
  }

  getFileContents(path: string): Uint8Array {
    if (!this.initialized) {
      throw new Error('MemFS not initialized');
    }
    this.mem.check();
    this.mem.write(this.exports.GetPathBuf(), path);
    const inode = this.exports.FindNode(path.length);
    const addr = this.exports.GetFileNodeAddress(inode);
    const size = this.exports.GetFileNodeSize(inode);
    return new Uint8Array(this.mem.buffer, addr, size);
  }

  abort(): never {
    throw new AbortError();
  }

  host_write(fd: number, iovs: number, iovs_len: number, nwritten_out: number): number {
    this.hostMem_!.check();
    assert(fd <= 2);
    let size = 0;
    let str = '';
    for (let i = 0; i < iovs_len; ++i) {
      const buf = this.hostMem_!.read32(iovs);
      iovs += 4;
      const len = this.hostMem_!.read32(iovs);
      iovs += 4;
      str += this.hostMem_!.readStr(buf, len);
      size += len;
    }
    this.hostMem_!.write32(nwritten_out, size);
    this.hostWrite(str);
    return ESUCCESS;
  }

  host_read(fd: number, iovs: number, iovs_len: number, nread: number): number {
    this.hostMem_!.check();
    assert(fd === 0);
    let size = 0;
    for (let i = 0; i < iovs_len; ++i) {
      const buf = this.hostMem_!.read32(iovs);
      iovs += 4;
      const len = this.hostMem_!.read32(iovs);
      iovs += 4;
      const lenToWrite = Math.min(len, this.stdinStr.length - this.stdinStrPos);
      if (lenToWrite === 0) {
        break;
      }
      this.hostMem_!.write(buf, this.stdinStr.substr(this.stdinStrPos, lenToWrite));
      size += lenToWrite;
      this.stdinStrPos += lenToWrite;
      if (lenToWrite !== len) {
        break;
      }
    }
    this.hostMem_!.write32(nread, size);
    return ESUCCESS;
  }

  memfs_log(buf: number, len: number): void {
    this.mem.check();
    console.log(this.mem.readStr(buf, len));
  }

  copy_out(clang_dst: number, memfs_src: number, size: number): void {
    this.hostMem_!.check();
    const dst = new Uint8Array(this.hostMem_!.buffer, clang_dst, size);
    this.mem.check();
    const src = new Uint8Array(this.mem.buffer, memfs_src, size);
    dst.set(src);
  }

  copy_in(memfs_dst: number, clang_src: number, size: number): void {
    this.mem.check();
    const dst = new Uint8Array(this.mem.buffer, memfs_dst, size);
    this.hostMem_!.check();
    const src = new Uint8Array(this.hostMem_!.buffer, clang_src, size);
    dst.set(src);
  }
}
