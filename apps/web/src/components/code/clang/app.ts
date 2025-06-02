import { NotImplemented, ProcExit } from './errors';
import { Memory } from './memory';
import { ESUCCESS, getImportObject, RAF_PROC_EXIT_CODE } from './shared';
import { MemFS } from './memfs';

interface AppExports extends WebAssembly.Exports {
  memory: WebAssembly.Memory;
  _start(): void;
}

export class App {
  private argv: string[];
  private environ: { [key: string]: string };
  private memfs: MemFS;
  private allowRequestAnimationFrame: boolean;
  private handles: Map<number, any>;
  private nextHandle: number;
  private exports!: AppExports;
  private mem!: Memory;
  public ready: Promise<void>;

  constructor(module: WebAssembly.Module, memfs: MemFS, name: string, ...args: string[]) {
    this.argv = [name, ...args];
    this.environ = { USER: 'alice' };
    this.memfs = memfs;
    this.allowRequestAnimationFrame = true;
    this.handles = new Map();
    this.nextHandle = 0;

    const env = getImportObject(this);

    const wasi_unstable = getImportObject(this, [
      'proc_exit',
      'environ_sizes_get',
      'environ_get',
      'args_sizes_get',
      'args_get',
      'random_get',
      'clock_time_get',
      'poll_oneoff',
    ]);

    // Fill in some WASI implementations from memfs.
    Object.assign(wasi_unstable, this.memfs.wasmExports);

    this.ready = WebAssembly.instantiate(module, { wasi_unstable, env }).then((instance) => {
      this.exports = instance.exports as AppExports;
      this.mem = new Memory(this.exports.memory);
      this.memfs.hostMem = this.mem;
    });
  }

  async run(): Promise<boolean> {
    await this.ready;
    try {
      this.exports._start();
    } catch (exn) {
      let writeStack = true;
      if (exn instanceof ProcExit) {
        if (exn.code === RAF_PROC_EXIT_CODE) {
          console.log('Allowing rAF after exit.');
          return true;
        }
        // Don't allow rAF unless you return the right code.
        console.log(`Disallowing rAF since exit code is ${exn.code}.`);
        this.allowRequestAnimationFrame = false;
        if (exn.code == 0) {
          return false;
        }
        writeStack = false;
      }

      // Write error message.
      let msg = `${(exn as Error).message}`;
      if (writeStack) {
        msg = msg + `\n${(exn as Error).stack}`;
      }
      this.memfs.writeToHost(msg);

      // Propagate error.
      throw exn;
    }
    return false;
  }

  proc_exit(code: number): never {
    throw new ProcExit(code);
  }

  environ_sizes_get(environ_count_out: number, environ_buf_size_out: number): number {
    this.mem.check();
    let size = 0;
    const names = Object.getOwnPropertyNames(this.environ);
    for (const name of names) {
      const value = this.environ[name];
      // +2 to account for = and \0 in "name=value\0".
      size += name.length + value.length + 2;
    }
    this.mem.write64(environ_count_out, names.length);
    this.mem.write64(environ_buf_size_out, size);
    return ESUCCESS;
  }

  environ_get(environ_ptrs: number, environ_buf: number): number {
    this.mem.check();
    const names = Object.getOwnPropertyNames(this.environ);
    for (const name of names) {
      this.mem.write32(environ_ptrs, environ_buf);
      environ_ptrs += 4;
      environ_buf += this.mem.writeStr(environ_buf, `${name}=${this.environ[name]}`);
    }
    this.mem.write32(environ_ptrs, 0);
    return ESUCCESS;
  }

  args_sizes_get(argc_out: number, argv_buf_size_out: number): number {
    this.mem.check();
    let size = 0;
    for (let arg of this.argv) {
      size += arg.length + 1; // "arg\0".
    }
    this.mem.write64(argc_out, this.argv.length);
    this.mem.write64(argv_buf_size_out, size);
    return ESUCCESS;
  }

  args_get(argv_ptrs: number, argv_buf: number): number {
    this.mem.check();
    for (let arg of this.argv) {
      this.mem.write32(argv_ptrs, argv_buf);
      argv_ptrs += 4;
      argv_buf += this.mem.writeStr(argv_buf, arg);
    }
    this.mem.write32(argv_ptrs, 0);
    return ESUCCESS;
  }

  random_get(buf: number, buf_len: number): void {
    const data = new Uint8Array(this.mem.buffer, buf, buf_len);
    for (let i = 0; i < buf_len; ++i) {
      data[i] = (Math.random() * 256) | 0;
    }
  }

  clock_time_get(clock_id: number, precision: number, time_out: number): never {
    throw new NotImplemented('wasi_unstable', 'clock_time_get');
  }

  poll_oneoff(in_ptr: number, out_ptr: number, nsubscriptions: number, nevents_out: number): never {
    throw new NotImplemented('wasi_unstable', 'poll_oneoff');
  }
}
