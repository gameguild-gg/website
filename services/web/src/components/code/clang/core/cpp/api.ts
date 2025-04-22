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

  private sendStageOutput(stage: 'init' | 'compile' | 'link' | 'execute', message: string) {
    // Send raw message for better readability
    this.hostWrite(message);
  }

  async getModule(name: string): Promise<WebAssembly.Module> {
    if (this.moduleCache[name]) return this.moduleCache[name];
    
    const moduleName = name.split('/').pop()?.split('.')[0] || name;
    const m = await (async () => {
      this.sendStageOutput('init', `Loading ${moduleName} module...\n`);
      const response = await fetch(name);
      return WebAssembly.compile(await response.arrayBuffer());
    })();
    
    this.moduleCache[name] = m;
    return m;
  }

  async untar(memfs: MemFS, url: string): Promise<void> {
    await this.memfs.ready;
    this.sendStageOutput('init', 'Downloading system files...\n');
    const tar = new Tar(await fetch(url).then((result) => result.arrayBuffer()));
    this.sendStageOutput('init', 'Extracting system files...\n');
    tar.untar(this.memfs);
    this.sendStageOutput('init', 'System environment ready.\n');
  }

  async compile(options: CompileOptions): Promise<App | null> {
    const input = options.input;
    const contents = options.contents;
    const obj = options.obj;
    const opt = options.opt || '2';

    await this.ready;
    this.memfs.addFile(input, contents);
    const clang = await this.getModule(clangUrl);
    
    const args = ['clang', '-cc1', '-emit-obj', ...this.clangCommonArgs, '-O2', '-o', obj, '-x', 'c++', '-std=c++2a', input];
    this.sendStageOutput('compile', 'Running Clang compiler...\n');
    this.sendStageOutput('compile', `${args.join(' ')}\n`);
    
    const result = await this.run(clang, ...args);
    return result;
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
    
    this.sendStageOutput('link', 'Running linker...\n');
    this.sendStageOutput('link', `${args.join(' ')}\n`);
    
    return await this.run(lld, ...args);
  }

  async run(module: WebAssembly.Module, ...args: string[]): Promise<App | null> {
    const isToolchain = args[0].includes('clang') || args[0].includes('wasm-ld');
    const stage = isToolchain ? this.currentStage : 'execute';
    
    // First argument in args is the program name, which matches the App constructor requirement
    const app = new App(module, this.memfs, args[0], ...args.slice(1));
    const stillRunning = await app.run();
    
    if (!isToolchain && stage === 'execute') {
      // Only show output for non-toolchain commands (actual program execution)
      this.sendStageOutput(stage, '');
    }
    
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
