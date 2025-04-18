import { App } from './app';
import { MemFS } from './memfs';
import { msToSec } from './shared';
import { Tar } from './tar';

// Import assets directly so webpack can handle them
import clangWasmUrl from '../../../../../../public/assets/clang.wasm';
import lldWasmUrl from '../../../../../../public/assets/lld.wasm';
import sysrootTarUrl from '../../../../../../public/assets/sysroot.tar';

// These will be properly resolved by webpack during build
const clangUrl = clangWasmUrl;
const lldUrl = lldWasmUrl;
const sysrootUrl = sysrootTarUrl;

interface APIOptions {
  hostWrite: (message: string) => void;
}

interface CompileOptions {
  input: string;
  contents: string;
  obj: string;
  opt?: string;
}

export class API {
  private moduleCache: { [key: string]: WebAssembly.Module };
  private hostWrite: (message: string) => void;
  private clangCommonArgs: string[];
  private memfs: MemFS;
  public ready: Promise<void>;
  private currentStage: 'init' | 'compile' | 'link' | 'execute' = 'init';

  // Public methods for file system operations
  public setStdinStr(input: string): void {
    this.memfs.setStdinStr(input);
  }

  public getFileContents(path: string): Uint8Array {
    return this.memfs.getFileContents(path);
  }

  public addFile(path: string, content: string | Uint8Array): void {
    this.memfs.addFile(path, content);
  }

  public addDirectory(path: string): void {
    this.memfs.addDirectory(path);
  }

  constructor(options: APIOptions) {
    this.moduleCache = {};
    this.currentStage = 'init';
    this.hostWrite = options.hostWrite;

    this.clangCommonArgs = [
      '-disable-free',
      '-isysroot',
      '/',
      '-internal-isystem',
      '/include/c++/v1',
      '-internal-isystem',
      '/include',
      '-internal-isystem',
      '/lib/clang/8.0.1/include',
      '-ferror-limit',
      '19',
      '-fmessage-length',
      '80',
      '-fcolor-diagnostics',
    ];

    this.memfs = new MemFS({
      hostWrite: this.hostWrite,
    });
    this.ready = this.memfs.ready.then(() => {
      return this.untar(this.memfs, sysrootUrl);
    });
  }

  async getModule(name: string): Promise<WebAssembly.Module> {
    if (this.moduleCache[name]) return this.moduleCache[name];
    const m = await this.hostLogAsync(
      `Fetching and compiling ${name}`,
      (async () => {
        const response = await fetch(name);
        return WebAssembly.compile(await response.arrayBuffer());
      })(),
    );
    this.moduleCache[name] = m;
    return m;
  }

  hostLog(message: string): void {
    this.hostWrite(message);
  }

  async hostLogAsync<T>(message: string, promise: Promise<T>): Promise<T> {
    const start = +new Date();
    this.hostLog(`${message}...`);
    const result = await promise;
    const end = +new Date();
    this.hostWrite('done.\n');
    return result;
  }

  async untar(memfs: MemFS, url: string): Promise<void> {
    await this.memfs.ready;
    const promise = (async () => {
      const tar = new Tar(await fetch(url).then((result) => result.arrayBuffer()));
      tar.untar(this.memfs);
    })();
    await this.hostLogAsync(`Untarring ${url}`, promise);
  }

  async compile(options: CompileOptions): Promise<App | null> {
    this.currentStage = 'compile';
    const input = options.input;
    const contents = options.contents;
    const obj = options.obj;
    const opt = options.opt || '2';

    await this.ready;
    this.memfs.addFile(input, contents);
    const clang = await this.getModule(clangUrl);
    const result = await this.run(clang, 'clang', '-cc1', '-emit-obj', ...this.clangCommonArgs, '-O2', '-o', obj, '-x', 'c++', '-std=c++2a', input);
    this.hostWrite('\n'); // Add a separator after compilation
    return result;
  }

  async link(obj: string, wasm: string): Promise<App | null> {
    this.currentStage = 'link';
    const stackSize = 1024 * 1024;

    const libdir = 'lib/wasm32-wasi';
    const crt1 = `${libdir}/crt1.o`;

    await this.ready;
    const lld = await this.getModule(lldUrl);
    const result = await this.run(
      lld,
      'wasm-ld',
      '--no-threads',
      '--export-dynamic',
      '-z',
      `stack-size=${stackSize}`,
      `-L${libdir}`,
      crt1,
      obj,
      '-lc',
      '-lc++',
      '-lc++abi',
      '-o',
      wasm,
    );
    this.hostWrite('\n'); // Add a separator after linking
    return result;
  }

  async run(module: WebAssembly.Module, ...args: string[]): Promise<App | null> {
    // Only change to execute stage if not running a toolchain command
    if (!args[0].includes('clang') && !args[0].includes('wasm-ld')) {
      this.currentStage = 'execute';
      this.hostWrite('\n'); // Add a separator before execution
    }

    this.hostLog(`${args.join(' ')}\n`);
    const app = new App(module, this.memfs, ...args);
    const stillRunning = await app.run();
    return stillRunning ? app : null;
  }

  async compileLinkRun(contents: string): Promise<App | null> {
    const input = `test.cpp`;
    const obj = `test.o`;
    const wasm = `test.wasm`;

    try {
      await this.compile({ input, contents, obj });
      await this.link(obj, wasm);

      const buffer = this.memfs.getFileContents(wasm);
      const testMod = await WebAssembly.compile(buffer);
      return await this.run(testMod, wasm);
    } catch (error) {
      this.hostWrite(`Error: ${error}\n`);
      return null;
    }
  }
}
