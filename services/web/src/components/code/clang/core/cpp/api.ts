import { App } from './app';
import { MemFS } from './memfs';
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
  private currentStage: 'init' | 'compile' | 'link' | 'execute' = 'init';
  private memfs: MemFS;
  public ready: Promise<void>;
  private sysroot: Uint8Array;
  private hostWrite: (message: string) => void;
  private clangCommonArgs: string[];
  private stdin: string = '';
  private messagePort: MessagePort | null = null;

  constructor(options: { hostWrite: (message: string) => void }) {
    if (!options || typeof options.hostWrite !== 'function') {
      throw new Error('hostWrite function is required');
    }

    this.hostWrite = options.hostWrite;
    this.clangCommonArgs = ['-cc1', '-emit-obj', '-disable-free', '-isysroot', '/',
      '-internal-isystem', '/include/c++/v1', '-internal-isystem', '/include',
      '-internal-isystem', '/lib/clang/8.0.1/include', '-Wall', '-O2'];

    this.memfs = new MemFS({
      hostWrite: this.hostWrite,
      stdinStr: ''
    });

    this.ready = this.init();
  }

  setMessagePort(port: MessagePort) {
    this.messagePort = port;
    if (this.hostWrite) {
      const originalHostWrite = this.hostWrite;
      this.hostWrite = (message: string) => {
        originalHostWrite(message);
        if (this.messagePort) {
          this.messagePort.postMessage({ id: 'write', data: message });
        }
      };
    }
  }

  private async init() {
    await this.memfs.ready;
    this.hostWrite('Downloading system files...\n');
    
    const sysrootResponse = await fetch(sysrootUrl);
    const sysrootBuffer = await sysrootResponse.arrayBuffer();
    this.sysroot = new Uint8Array(sysrootBuffer);
    
    this.hostWrite('Extracting system files...\n');
    // Convert to ArrayBuffer using array copy if needed
    const buffer = (sysrootBuffer as ArrayBuffer).slice(0);
    const tar = new Tar(buffer);
    tar.untar(this.memfs);
    
    this.hostWrite('System environment ready.\n');
  }

  private async getModule(url: string): Promise<WebAssembly.Module> {
    const response = await fetch(url);
    const buffer = await response.arrayBuffer();
    return WebAssembly.compile(buffer);
  }

  setStdinStr(stdin: string) {
    this.stdin = stdin;
    this.memfs.setStdinStr(stdin);
  }

  getFileContents(path: string): Uint8Array {
    return this.memfs.getFileContents(path);
  }

  addFile(path: string, contents: Uint8Array): void {
    this.memfs.addFile(path, contents);
  }

  addDirectory(path: string): void {
    this.memfs.addDirectory(path);
  }

  async compile(options: CompileOptions): Promise<App | null> {
    const input = options.input;
    const contents = options.contents;
    const obj = options.obj;
    await this.ready;

    if (typeof contents !== 'string') {
      throw new Error('Contents must be a string');
    }

    this.memfs.addFile(input, contents);
    const clang = await this.getModule(clangUrl);

    const args = ['clang', ...this.clangCommonArgs, '-O2', '-o', obj, '-x', 'c++', '-std=c++2a', input];
    this.hostWrite('Running Clang compiler...\n');
    this.hostWrite(`${args.join(' ')}\n`);

    return await this.run(clang, ...args);
  }

  async link(obj: string, wasm: string): Promise<App | null> {
    const stackSize = 1024 * 1024;
    const libdir = 'lib/wasm32-wasi';
    const crt1 = `${libdir}/crt1.o`;

    await this.ready;
    const lld = await this.getModule(lldUrl);

    const args = [
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
    ];

    this.hostWrite('Running linker...\n');
    this.hostWrite(`${args.join(' ')}\n`);

    return await this.run(lld, ...args);
  }

  async run(module: WebAssembly.Module, ...args: string[]): Promise<App | null> {
    const app = new App(module, this.memfs, args[0], ...args.slice(1));
    const stillRunning = await app.run();
    return stillRunning ? app : null;
  }
}
