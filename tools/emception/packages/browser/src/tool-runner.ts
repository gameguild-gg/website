/**
 * Micro-kernel Tool Runner
 *
 * Each tool invocation spawns an isolated WASM process with its own linear
 * memory, libc, heap, and Emscripten filesystem. The kernel (this module)
 * manages:
 *
 *   - Process lifecycle: create → configure → run → teardown
 *   - VFS ↔ process FS bridging: pre-populate input files, harvest outputs
 *   - TTY: route stdout/stderr from the process to the caller
 *   - ENV: inject environment variables into each process
 *
 * There is NO shared MAIN_MODULE, NO dlopen/dlsym, NO shared linear memory.
 * Each tool is compiled as a standalone Emscripten module (MODULARIZE + EXPORT_ES6).
 */

import { SUBPROCESS_SHIM } from './emscripten/subprocess-shim.js';
import { loadModuleFactory } from './loader/wasm-module.js';
import { mountVFSFS, type VFSFSRuntime } from './vfs/emscripten-vfsfs.js';
import type { VFSManager } from './vfs/index.js';

const LOG_PREFIX = '[Emception:Kernel]';
function elapsed(t0: number): string {
  return `${(performance.now() - t0).toFixed(1)}ms`;
}
function fmtSize(n: number): string {
  if (n < 1024) return `${n}B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)}KB`;
  return `${(n / (1024 * 1024)).toFixed(1)}MB`;
}

/** Sentinel error thrown by WASI proc_exit to unwind the call stack. */
class WasiExit extends Error {
  code: number;
  constructor(code: number) {
    super(`WASI proc_exit(${code})`);
    this.code = code;
  }
}

/* ------------------------------------------------------------------ */
/*  Public interfaces                                                  */
/* ------------------------------------------------------------------ */

export interface ToolResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

export interface RunOptions {
  env?: Record<string, string>;
  cwd?: string;
  onStdout?: (text: string) => void;
  onStderr?: (text: string) => void;
  stdin?: () => number | null | Promise<number>;
  /**
   * When true, the tool invocation is an info/version query (e.g. --version).
   * setupProcessFS will skip mounting VFSFS since the tool doesn't need
   * filesystem access.
   */
  isInfoQuery?: boolean;
  /**
   * Performance hints for bundle preloading.
   * - `bundlesNeeded`: names of CDN bundles (e.g. 'sdl3', 'raylib', 'allegro')
   *   that the tool invocation requires. When provided, only those graphics
   *   bundles are preloaded — others are skipped to avoid wasted network/IDB
   *   traffic on unrelated builds (e.g. a terminal C++ build doesn't need sdl3).
   */
  hints?: {
    bundlesNeeded?: string[];
  };
}

/* ------------------------------------------------------------------ */
/*  Tool descriptors — standalone .wasm modules                        */
/* ------------------------------------------------------------------ */

interface ToolDescriptor {
  /** Path to the standalone .wasm module (also used to derive the .mjs glue URL) */
  modulePath: string;
  /** Directories whose contents should be harvested back to kernel VFS after run */
  harvestDirs?: string[];
  /** Extra environment variables injected when this tool is spawned */
  env?: Record<string, string>;
}

/**
 * Tool registry — maps tool names to standalone WASM module paths.
 * No shared libraries, no entry symbols needed (each module has standard main()).
 */
const TOOL_REGISTRY: Record<string, ToolDescriptor> = {
  clang: {
    modulePath: '/usr/lib/clang.wasm',
  },
  'clang++': {
    modulePath: '/usr/lib/clang.wasm',
  },
  lld: {
    modulePath: '/usr/lib/lld.wasm',
  },
  'wasm-ld': {
    modulePath: '/usr/lib/lld.wasm',
  },
  'llvm-nm': {
    modulePath: '/usr/lib/llvm-nm.wasm',
  },
  'llvm-ar': {
    modulePath: '/usr/lib/llvm-ar.wasm',
  },
  'llvm-objcopy': {
    modulePath: '/usr/lib/llvm-objcopy.wasm',
  },
  llc: {
    modulePath: '/usr/lib/llc.wasm',
  },
  'wasm-opt': {
    modulePath: '/usr/lib/wasm-opt.wasm',
  },
  'wasm-as': {
    modulePath: '/usr/lib/wasm-as.wasm',
  },
  'wasm-ctor-eval': {
    modulePath: '/usr/lib/wasm-ctor-eval.wasm',
  },
  'wasm-emscripten-finalize': {
    modulePath: '/usr/lib/wasm-emscripten-finalize.wasm',
  },
  'wasm-metadce': {
    modulePath: '/usr/lib/wasm-metadce.wasm',
  },
  // Note: ninja.wasm removed - handled inline via special case
  cmake: {
    modulePath: '/usr/lib/cmake.wasm',
    env: {
      CMAKE_ROOT: '/usr/share/cmake-4.3',
      CC: '/usr/bin/clang',
      CXX: '/usr/bin/clang++',
    },
  },
  curl: {
    modulePath: '/usr/lib/curl.wasm',
  },
};

/**
 * Optional post-processing tools: these strip debug info, optimize, etc.
 * If their WASM modules don't exist or they crash at runtime, they can be
 * safely skipped since the output from wasm-ld is already a valid WASM binary.
 */
const OPTIONAL_TOOLS = new Set(['llvm-objcopy', 'llvm-strip', 'wasm-opt', 'wasm-metadce', 'wasm-ctor-eval', 'wasm-emscripten-finalize']);

/* ------------------------------------------------------------------ */
/*  Pre-generated cmake config files                                   */
/*                                                                     */
/*  cmake's determination scripts call configure_file() to generate    */
/*  compiler info files at ${buildDir}/CMakeFiles/<version>/.  In WASM */
/*  the FS bridge may fail on those writes.  Pre-seeding these files   */
/*  in fileData ensures cmake's C++ code finds cached results and      */
/*  skips or short-circuits the problematic detection flow.            */
/* ------------------------------------------------------------------ */

const CMAKE_BUILD_VERSION = '4.3.1';

const CMAKE_SYSTEM_PRESEED = `\
set(CMAKE_HOST_SYSTEM "Generic-1")
set(CMAKE_HOST_SYSTEM_NAME "Generic")
set(CMAKE_HOST_SYSTEM_VERSION "1")
set(CMAKE_HOST_SYSTEM_PROCESSOR "wasm32")

set(CMAKE_SYSTEM "Generic-1")
set(CMAKE_SYSTEM_NAME "Generic")
set(CMAKE_SYSTEM_VERSION "1")
set(CMAKE_SYSTEM_PROCESSOR "wasm32")

set(CMAKE_CROSSCOMPILING "TRUE")

set(CMAKE_SYSTEM_LOADED 1)
`;

const CMAKE_CXX_COMPILER_PRESEED = `\
set(CMAKE_CXX_COMPILER "/usr/bin/clang++")
set(CMAKE_CXX_COMPILER_ARG1 "")
set(CMAKE_CXX_COMPILER_ID "Clang")
set(CMAKE_CXX_COMPILER_VERSION "20.0.0")
set(CMAKE_CXX_COMPILER_VERSION_INTERNAL "")
set(CMAKE_CXX_COMPILER_WRAPPER "")
set(CMAKE_CXX_STANDARD_COMPUTED_DEFAULT "17")
set(CMAKE_CXX_EXTENSIONS_COMPUTED_DEFAULT "ON")
set(CMAKE_CXX_STANDARD_LATEST "26")
set(CMAKE_CXX_COMPILE_FEATURES "cxx_std_98;cxx_std_11;cxx_std_14;cxx_std_17;cxx_std_20;cxx_std_23;cxx_std_26")
set(CMAKE_CXX98_COMPILE_FEATURES "cxx_std_98")
set(CMAKE_CXX11_COMPILE_FEATURES "cxx_std_11")
set(CMAKE_CXX14_COMPILE_FEATURES "cxx_std_14")
set(CMAKE_CXX17_COMPILE_FEATURES "cxx_std_17")
set(CMAKE_CXX20_COMPILE_FEATURES "cxx_std_20")
set(CMAKE_CXX23_COMPILE_FEATURES "cxx_std_23")
set(CMAKE_CXX26_COMPILE_FEATURES "cxx_std_26")

set(CMAKE_CXX_PLATFORM_ID "")
set(CMAKE_CXX_SIMULATE_ID "")
set(CMAKE_CXX_COMPILER_FRONTEND_VARIANT "GNU")
set(CMAKE_CXX_COMPILER_APPLE_SYSROOT "")
set(CMAKE_CXX_SIMULATE_VERSION "")
set(CMAKE_CXX_COMPILER_ARCHITECTURE_ID "")

set(CMAKE_AR "/usr/bin/llvm-ar")
set(CMAKE_CXX_COMPILER_AR "")
set(CMAKE_RANLIB "/usr/bin/llvm-ar")
set(CMAKE_CXX_COMPILER_RANLIB "")
set(CMAKE_LINKER "/usr/bin/wasm-ld")
set(CMAKE_LINKER_LINK "")
set(CMAKE_LINKER_LLD "")
set(CMAKE_CXX_COMPILER_LINKER "")
set(CMAKE_CXX_COMPILER_LINKER_ID "")
set(CMAKE_CXX_COMPILER_LINKER_VERSION )
set(CMAKE_CXX_COMPILER_LINKER_FRONTEND_VARIANT )
set(CMAKE_MT "")
set(CMAKE_TAPI "")
set(CMAKE_COMPILER_IS_GNUCXX )
set(CMAKE_CXX_COMPILER_LOADED 1)
set(CMAKE_CXX_COMPILER_WORKS TRUE)
set(CMAKE_CXX_ABI_COMPILED TRUE)

set(CMAKE_CXX_COMPILER_ENV_VAR "CXX")

set(CMAKE_CXX_COMPILER_ID_RUN 1)
set(CMAKE_CXX_SOURCE_FILE_EXTENSIONS C;M;c++;cc;cpp;cxx;m;mm;mpp;CPP;ixx;cppm;ccm;cxxm;c++m)
set(CMAKE_CXX_IGNORE_EXTENSIONS inl;h;hpp;HPP;H;o;O;obj;OBJ;def;DEF;rc;RC)

set(CMAKE_CXX_LINKER_PREFERENCE 30)
set(CMAKE_CXX_LINKER_PREFERENCE_PROPAGATES 1)
set(CMAKE_CXX_LINKER_DEPFILE_SUPPORTED )
set(CMAKE_LINKER_PUSHPOP_STATE_SUPPORTED )
set(CMAKE_CXX_LINKER_PUSHPOP_STATE_SUPPORTED )

# Compiler ABI information.
set(CMAKE_CXX_SIZEOF_DATA_PTR "4")
set(CMAKE_CXX_COMPILER_ABI "")
set(CMAKE_CXX_BYTE_ORDER "LITTLE_ENDIAN")
set(CMAKE_CXX_LIBRARY_ARCHITECTURE "")

if(CMAKE_CXX_SIZEOF_DATA_PTR)
  set(CMAKE_SIZEOF_VOID_P "\${CMAKE_CXX_SIZEOF_DATA_PTR}")
endif()

set(CMAKE_CXX_CL_SHOWINCLUDES_PREFIX "")

set(CMAKE_CXX_IMPLICIT_INCLUDE_DIRECTORIES "")
set(CMAKE_CXX_IMPLICIT_LINK_LIBRARIES "")
set(CMAKE_CXX_IMPLICIT_LINK_DIRECTORIES "")
set(CMAKE_CXX_IMPLICIT_LINK_FRAMEWORK_DIRECTORIES "")
set(CMAKE_CXX_COMPILER_CLANG_RESOURCE_DIR "")

set(CMAKE_CXX_COMPILER_IMPORT_STD "")
set(CMAKE_CXX_COMPILER_IMPORT_STD_ERROR_MESSAGE  "")
set(CMAKE_CXX_STDLIB_MODULES_JSON "")
`;

const CMAKE_C_COMPILER_PRESEED = `\
set(CMAKE_C_COMPILER "/usr/bin/clang")
set(CMAKE_C_COMPILER_ARG1 "")
set(CMAKE_C_COMPILER_ID "Clang")
set(CMAKE_C_COMPILER_VERSION "20.0.0")
set(CMAKE_C_COMPILER_VERSION_INTERNAL "")
set(CMAKE_C_COMPILER_WRAPPER "")
set(CMAKE_C_STANDARD_COMPUTED_DEFAULT "17")
set(CMAKE_C_EXTENSIONS_COMPUTED_DEFAULT "ON")
set(CMAKE_C_STANDARD_LATEST "23")
set(CMAKE_C_COMPILE_FEATURES "c_std_90;c_std_99;c_std_11;c_std_17;c_std_23")
set(CMAKE_C90_COMPILE_FEATURES "c_std_90")
set(CMAKE_C99_COMPILE_FEATURES "c_std_99")
set(CMAKE_C11_COMPILE_FEATURES "c_std_11")
set(CMAKE_C17_COMPILE_FEATURES "c_std_17")
set(CMAKE_C23_COMPILE_FEATURES "c_std_23")

set(CMAKE_C_PLATFORM_ID "")
set(CMAKE_C_SIMULATE_ID "")
set(CMAKE_C_COMPILER_FRONTEND_VARIANT "GNU")
set(CMAKE_C_COMPILER_APPLE_SYSROOT "")
set(CMAKE_C_SIMULATE_VERSION "")
set(CMAKE_C_COMPILER_ARCHITECTURE_ID "")

set(CMAKE_AR "/usr/bin/llvm-ar")
set(CMAKE_C_COMPILER_AR "")
set(CMAKE_RANLIB "/usr/bin/llvm-ar")
set(CMAKE_C_COMPILER_RANLIB "")
set(CMAKE_LINKER "/usr/bin/wasm-ld")
set(CMAKE_LINKER_LINK "")
set(CMAKE_LINKER_LLD "")
set(CMAKE_C_COMPILER_LINKER "")
set(CMAKE_C_COMPILER_LINKER_ID "")
set(CMAKE_C_COMPILER_LINKER_VERSION )
set(CMAKE_C_COMPILER_LINKER_FRONTEND_VARIANT )
set(CMAKE_MT "")
set(CMAKE_TAPI "")
set(CMAKE_COMPILER_IS_GNUCC )
set(CMAKE_C_COMPILER_LOADED 1)
set(CMAKE_C_COMPILER_WORKS TRUE)
set(CMAKE_C_ABI_COMPILED TRUE)

set(CMAKE_C_COMPILER_ENV_VAR "CC")

set(CMAKE_C_COMPILER_ID_RUN 1)
set(CMAKE_C_SOURCE_FILE_EXTENSIONS c;m)
set(CMAKE_C_IGNORE_EXTENSIONS h;H;o;O;obj;OBJ;def;DEF;rc;RC)
set(CMAKE_C_LINKER_PREFERENCE 10)
set(CMAKE_C_LINKER_DEPFILE_SUPPORTED )
set(CMAKE_LINKER_PUSHPOP_STATE_SUPPORTED )
set(CMAKE_C_LINKER_PUSHPOP_STATE_SUPPORTED )

# Compiler ABI information.
set(CMAKE_C_SIZEOF_DATA_PTR "4")
set(CMAKE_C_COMPILER_ABI "")
set(CMAKE_C_BYTE_ORDER "LITTLE_ENDIAN")
set(CMAKE_C_LIBRARY_ARCHITECTURE "")

if(CMAKE_C_SIZEOF_DATA_PTR)
  set(CMAKE_SIZEOF_VOID_P "\${CMAKE_C_SIZEOF_DATA_PTR}")
endif()

set(CMAKE_C_CL_SHOWINCLUDES_PREFIX "")

set(CMAKE_C_IMPLICIT_INCLUDE_DIRECTORIES "")
set(CMAKE_C_IMPLICIT_LINK_LIBRARIES "")
set(CMAKE_C_IMPLICIT_LINK_DIRECTORIES "")
set(CMAKE_C_IMPLICIT_LINK_FRAMEWORK_DIRECTORIES "")
`;

type ModuleFactory = (config: Record<string, unknown>) => Promise<EmscriptenInstance>;

/** Minimal shape of an Emscripten-generated module instance */
interface EmscriptenInstance {
  FS: {
    writeFile(path: string, data: string | Uint8Array): void;
    readFile(path: string, opts?: { encoding?: string }): Uint8Array;
    readdir(path: string): string[];
    stat(path: string): { size: number; mode: number };
    mkdirTree(path: string): void;
    mkdir(path: string): void;
    chdir(path: string): void;
    unlink(path: string): void;
    isDir(mode: number): boolean;
    symlink(target: string, path: string): void;
  };
  callMain?(argv: string[]): number;
  EXITSTATUS?: number;
  [key: string]: unknown;
}

/* ------------------------------------------------------------------ */
/*  Kernel (ToolRunner)                                                */
/* ------------------------------------------------------------------ */

export interface ToolVersionConfig {
  pythonMajorMinor: string; // e.g. "3.13"
  pythonMajorMinorCompact: string; // e.g. "313"
}

export class ToolRunner {
  private vfs: VFSManager;
  private versions: ToolVersionConfig;

  constructor(vfs: VFSManager, versions: ToolVersionConfig = { pythonMajorMinor: '3.13', pythonMajorMinorCompact: '313' }) {
    this.vfs = vfs;
    this.versions = versions;
  }

  /** Resolve an explicit output artifact path from argv (-o <path>). */
  private getOutputPathFromArgv(argv: string[], cwd?: string): string | null {
    const outIdx = argv.lastIndexOf('-o');
    if (outIdx < 0 || outIdx + 1 >= argv.length) return null;
    const outPath = argv[outIdx + 1];
    if (!outPath) return null;
    if (outPath.startsWith('/')) return outPath;
    const base = cwd && cwd.startsWith('/') ? cwd : '/home/user';
    return `${base.replace(/\/$/, '')}/${outPath}`;
  }

  private escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  private tryReadProcessFile(fs: EmscriptenInstance['FS'], path: string, fileData?: Map<string, Uint8Array>): Uint8Array | null {
    const tryFsRead = (candidate: string): Uint8Array | null => {
      try {
        const data = fs.readFile(candidate);
        return data && data.length > 0 ? data : null;
      } catch {
        return null;
      }
    };

    const tryFileDataRead = (candidate: string): Uint8Array | null => {
      const data = fileData?.get(candidate) ?? null;
      return data && data.length > 0 ? data : null;
    };

    const exact = tryFsRead(path) ?? tryFileDataRead(path);
    if (exact) return exact;

    if (!fileData) return null;

    const lastSlash = path.lastIndexOf('/');
    const dir = lastSlash >= 0 ? (lastSlash === 0 ? '/' : path.slice(0, lastSlash)) : '/';
    const base = lastSlash >= 0 ? path.slice(lastSlash + 1) : path;
    const extIdx = base.lastIndexOf('.');
    const stem = extIdx > 0 ? base.slice(0, extIdx) : base;
    const ext = extIdx > 0 ? base.slice(extIdx) : '';
    const dirPrefix = dir === '/' ? '/' : `${dir}/`;
    const tmpNameRe = new RegExp(`^(?:${this.escapeRegExp(base)}\\.tmp|${this.escapeRegExp(stem)}-[0-9a-f]+${this.escapeRegExp(ext)}\\.tmp)$`, 'i');

    const candidatePaths = new Set<string>();
    candidatePaths.add(`${path}.tmp`);

    for (const key of fileData.keys()) {
      if (!key.startsWith(dirPrefix)) continue;
      const name = key.slice(dirPrefix.length);
      if (name.includes('/')) continue;
      if (tmpNameRe.test(name)) {
        candidatePaths.add(key);
      }
    }

    try {
      const entries = fs.readdir(dir).filter((name: string) => name !== '.' && name !== '..');
      for (const name of entries) {
        if (tmpNameRe.test(name)) {
          candidatePaths.add(`${dirPrefix}${name}`.replace(/^\/\//, '/'));
        }
      }
    } catch {
      // Ignore missing directories while probing fallback candidates.
    }

    let bestCandidate: Uint8Array | null = null;
    let bestPath: string | null = null;
    for (const candidatePath of candidatePaths) {
      const candidate = tryFsRead(candidatePath) ?? tryFileDataRead(candidatePath);
      if (!candidate) continue;
      if (!bestCandidate || candidate.length > bestCandidate.length) {
        bestCandidate = candidate;
        bestPath = candidatePath;
      }
    }

    if (bestCandidate && bestPath) {
      console.warn(`${LOG_PREFIX}   Falling back to temp output artifact: ${bestPath} → ${path} (${bestCandidate.length}B)`);
    }

    return bestCandidate;
  }

  /* ---------------------------------------------------------------- */
  /*  Public API                                                       */
  /* ---------------------------------------------------------------- */

  /**
   * Run a tool by name with the given argv. Each call spawns an isolated
   * WASM process and tears it down after completion.
   */
  async run(tool: string, argv: string[], options: RunOptions = {}): Promise<ToolResult> {
    const tTotal = performance.now();
    console.log(`${LOG_PREFIX} ===== RUN: ${tool} =====`);
    console.log(`${LOG_PREFIX}   argv: [${argv.map((a) => `"${a}"`).join(', ')}]`);

    // Extract basename from tool path (handle both 'node' and '/usr/bin/node')
    const toolBasename = tool.includes('/') ? tool.split('/').pop() || tool : tool;

    // Special case: 'node' runs compiled JavaScript output in the browser.
    if (toolBasename === 'node') {
      console.log(`${LOG_PREFIX}   Dispatching to runJavaScript (node emulation)`);
      return this.runJavaScript(argv, options);
    }

    // Special case: 'wasi-run' executes a compiled standalone WASM binary
    // using a minimal in-browser WASI runtime.
    if (toolBasename === 'wasi-run') {
      console.log(`${LOG_PREFIX}   Dispatching to runWasi (WASI runtime)`);
      return this.runWasi(argv, options);
    }

    // Special case: 'ninja' for actual builds (not --version or -t queries).
    // Ninja.wasm's deep C++ call stack (main→Builder→StartEdge→CommandRunner
    // →SubprocessSet::Add→Start→system) can't properly Asyncify-unwind when
    // dispatching subprocesses via system(). Instead of running ninja.wasm,
    // we parse build.ninja in JS and execute each build command directly.
    if (toolBasename === 'ninja') {
      const isInfoQuery = options.isInfoQuery ?? argv.some((a) => a === '--version' || a === '-v');
      const isToolQuery = argv.some((a) => a === '-t');
      if (isInfoQuery) {
        console.log(`${LOG_PREFIX}   [ninja] info query → fake version 1.12.1`);
        options.onStdout?.('1.12.1\n');
        return { exitCode: 0, stdout: '1.12.1\n', stderr: '' };
      }
      if (isToolQuery) {
        console.log(`${LOG_PREFIX}   [ninja] tool query (-t) → no-op`);
        return { exitCode: 0, stdout: '', stderr: '' };
      }
      console.log(`${LOG_PREFIX}   Dispatching to ninjaBuildBypass (JS-side build.ninja executor)`);
      return this.ninjaBuildBypass(argv, options);
    }

    // For emcc/em++: inject the actual emcc.py script path
    if (toolBasename === 'emcc' || toolBasename === 'em++') {
      const scriptPath = '/usr/lib/emscripten/emcc.py';
      if (argv.length > 0) {
        argv = [argv[0], scriptPath, ...argv.slice(1)];
      } else {
        argv = [tool, scriptPath];
      }
      console.log(`${LOG_PREFIX}   Injected Python script: ${scriptPath}`);
    }

    // Normalize linker library paths to canonical cache-lib locations.
    // Some code paths pass /usr/lib/emscripten/cache/sysroot/lib/... which can
    // contain stale or non-object payloads; the prebuilt immutable artifacts
    // live under /usr/lib/emscripten/cache-lib/...
    if (toolBasename === 'wasm-ld' || toolBasename === 'lld') {
      argv = argv.map((a) =>
        a
          .replace('/usr/lib/emscripten/cache/sysroot/lib', '/usr/lib/emscripten/cache-lib')
          .replace('/home/user/.emscripten_cache/sysroot/lib', '/usr/lib/emscripten/cache-lib'),
      );
      const hadCrt1 = argv.some((a) => a.endsWith('/crt1.o'));
      argv = argv.filter((a) => !a.endsWith('/crt1.o'));
      if (hadCrt1 && !argv.some((a) => a.startsWith('--entry=') || a === '--entry' || a === '--no-entry')) {
        argv.push('--entry=main');
      }
    }

    // cmake: inject Emception environment flags via -D cache variables.
    // cmake's compiler identification tries to compile CMakeCXXCompilerId.cpp,
    // which fails in WASM. We pre-set all compiler identity variables:
    //   *_COMPILER_ID_RUN=1  → skip identification (no test compilation)
    //   *_COMPILER_FORCED    → skip compiler testing
    //   *_COMPILER_WORKS     → mark compilers as working
    //   *_COMPILER_ID=Clang  → pre-set compiler identity
    // User-specified -D flags take precedence (never overwritten).
    if (toolBasename === 'cmake') {
      const cmakeDefaults: [string, string][] = [
        ['CMAKE_SYSTEM_NAME', 'Generic'],
        ['CMAKE_MAKE_PROGRAM', '/usr/bin/ninja'],
        ['CMAKE_C_COMPILER', '/usr/bin/clang'],
        ['CMAKE_CXX_COMPILER', '/usr/bin/clang++'],
        ['CMAKE_C_COMPILER_FORCED:BOOL', 'TRUE'],
        ['CMAKE_CXX_COMPILER_FORCED:BOOL', 'TRUE'],
        ['CMAKE_C_COMPILER_WORKS:BOOL', 'TRUE'],
        ['CMAKE_CXX_COMPILER_WORKS:BOOL', 'TRUE'],
        ['CMAKE_C_COMPILER_ID_RUN:BOOL', '1'],
        ['CMAKE_CXX_COMPILER_ID_RUN:BOOL', '1'],
        ['CMAKE_C_COMPILER_ID:STRING', 'Clang'],
        ['CMAKE_CXX_COMPILER_ID:STRING', 'Clang'],
        ['CMAKE_AR', '/usr/bin/llvm-ar'],
        ['CMAKE_RANLIB', '/usr/bin/llvm-ar'],
        // Tell clang to target WASM and use the emscripten sysroot for headers.
        // Without these, raw clang++ doesn't find <iostream> etc.
        // -isystem /usr/include/compat picks up xlocale.h and other compat shims.
        ['CMAKE_C_FLAGS', '--target=wasm32-unknown-emscripten --sysroot=/usr -isystem /usr/include/compat'],
        ['CMAKE_CXX_FLAGS', '--target=wasm32-unknown-emscripten --sysroot=/usr -isystem /usr/include/compat'],
        // Directly set the ENV_VAR variables cmake checks in EnableLanguage.
        // Without these, cmake relies on CMakeDetermine*.cmake scripts which
        // may not fire their early-return in the WASM environment.
        ['CMAKE_C_COMPILER_ENV_VAR:STRING', 'CC'],
        ['CMAKE_CXX_COMPILER_ENV_VAR:STRING', 'CXX'],
      ];
      const extraFlags: string[] = [];
      for (const [key, value] of cmakeDefaults) {
        const varName = key.replace(/:.*$/, '');
        const alreadySet = argv.some((a) => {
          const m = a.match(/^-D([^:=]+)/);
          return m?.[1] === varName;
        });
        if (!alreadySet) {
          extraFlags.push(`-D${key}=${value}`);
        }
      }
      if (extraFlags.length > 0) {
        argv = [...argv, ...extraFlags];
        console.log(`${LOG_PREFIX}   Injected ${extraFlags.length} cmake flags`);
      }
    }

    // Use module-level OPTIONAL_TOOLS set (defined near TOOL_REGISTRY)

    let descriptor: ReturnType<typeof this.resolveToolDescriptor>;
    try {
      descriptor = this.resolveToolDescriptor(tool);
    } catch {
      const msg = `${toolBasename}: command not found`;
      console.warn(`${LOG_PREFIX}   ${msg}`);
      options.onStderr?.(msg);
      return { exitCode: 127, stdout: '', stderr: msg };
    }
    console.log(`${LOG_PREFIX}   Descriptor: module=${descriptor.modulePath}`);

    // Check if the tool's WASM module exists in the VFS.
    {
      const wasmExists = await this.vfs.fetchFile(descriptor.modulePath);
      if (!wasmExists) {
        if (OPTIONAL_TOOLS.has(toolBasename)) {
          // Optional post-processing tools can be safely skipped
          console.log(`${LOG_PREFIX}   [SKIP] Optional tool "${toolBasename}" — WASM module not found, returning no-op (exit 0)`);
          console.log(`${LOG_PREFIX} ===== RUN COMPLETE: ${tool} — exitCode=0 (skipped), total=${elapsed(tTotal)} =====`);
          return { exitCode: 0, stdout: '', stderr: '' };
        }
        // Required tool missing — return a clear error instead of crashing on dynamic import
        const msg = `${toolBasename}: tool not available (WASM module not found at ${descriptor.modulePath}). Build it first with: npm run build:${toolBasename}`;
        console.warn(`${LOG_PREFIX}   ${msg}`);
        options.onStderr?.(msg);
        return { exitCode: 127, stdout: '', stderr: msg };
      }
    }

    // P3: For emcc/em++ invocations, proactively preload optional bundles when
    // the user requests debug or sanitizer builds.  Preloads run in parallel
    // with the early compiler setup work so bundle fetch latency is hidden.
    if (toolBasename === 'emcc' || toolBasename === 'em++') {
      const needsDebug = argv.some((a) => /^-g[1-9]?$/.test(a) || a === '-O0');
      const needsSanitizers = argv.some((a) => /^-fsanitize=/.test(a));
      const optionalPreloads: Promise<unknown>[] = [];
      if (needsDebug) optionalPreloads.push(this.vfs.preloadBundle('cache-debug').catch(() => { }));
      if (needsSanitizers) optionalPreloads.push(this.vfs.preloadBundle('cache-sanitizers').catch(() => { }));
      if (optionalPreloads.length > 0) {
        console.log(`${LOG_PREFIX}   P3 lazy-preload: debug=${needsDebug} sanitizers=${needsSanitizers}`);
        // Fire-and-forget: continue spawning while bundles download
        void Promise.all(optionalPreloads);
      }
    }

    // Spawn an isolated WASM process
    const result = await this.spawnProcess(descriptor, argv, options, toolBasename);

    console.log(`${LOG_PREFIX} ===== RUN COMPLETE: ${tool} — exitCode=${result.exitCode}, total=${elapsed(tTotal)} =====`);
    return result;
  }

  /* ---------------------------------------------------------------- */
  /*  Ninja build bypass (JS-side build.ninja parser + executor)       */
  /* ---------------------------------------------------------------- */

  /**
   * Execute a ninja build by parsing build.ninja in JS and running each
   * build command directly through the tool runner. This avoids the
   * Asyncify crash that happens when ninja.wasm tries to dispatch
   * subprocesses via system("__dispatch_subprocess").
   */
  private async ninjaBuildBypass(argv: string[], options: RunOptions): Promise<ToolResult> {
    const tTotal = performance.now();

    // Extract build directory from -C flag
    let buildDir = '.';
    const cIdx = argv.indexOf('-C');
    if (cIdx >= 0 && cIdx + 1 < argv.length) {
      buildDir = argv[cIdx + 1];
    }
    const base = options.cwd && options.cwd.startsWith('/') ? options.cwd : '/home/user';
    const absBuildDir = buildDir.startsWith('/')
      ? buildDir
      : `${base.replace(/\/$/, '')}/${buildDir}`;

    // Read build.ninja
    const buildNinjaPath = `${absBuildDir}/build.ninja`;
    console.log(`${LOG_PREFIX}   [ninja-bypass] Reading ${buildNinjaPath}...`);
    const buildNinjaData = await this.vfs.fetchFile(buildNinjaPath);
    if (!buildNinjaData) {
      const msg = `ninja: error: loading '${buildNinjaPath}'.`;
      console.error(`${LOG_PREFIX}   [ninja-bypass] ${msg}`);
      options.onStderr?.(msg);
      return { exitCode: 1, stdout: '', stderr: msg };
    }

    const buildContent = new TextDecoder().decode(buildNinjaData);

    // Resolve include/subninja directives by reading referenced files
    const resolvedContent = await this.resolveNinjaIncludes(buildContent, absBuildDir);
    const commands = this.parseBuildNinja(resolvedContent);
    console.log(`${LOG_PREFIX}   [ninja-bypass] Parsed ${commands.length} build command(s)`);

    if (commands.length === 0) {
      const msg = 'ninja: no work to do.';
      console.log(`${LOG_PREFIX}   [ninja-bypass] ${msg}`);
      options.onStdout?.(msg);
      return { exitCode: 0, stdout: msg, stderr: '' };
    }

    // Preload bundles that compilation tools will need
    const tPreload = performance.now();

    // P3: detect debug/sanitizer flags across all commands for lazy preloads
    const allCommandsText = commands.join(' ');
    const ninjaHasDebug = /\s-g[1-9]?\s|\s-O0\s/.test(allCommandsText);
    const ninjaHasSanitizers = /-fsanitize=/.test(allCommandsText);

    try {
      await Promise.all([
        this.vfs.preloadBundle('clang'),
        this.vfs.preloadBundle('lld'),
        this.vfs.preloadBundle('usr-include'),
        this.vfs.preloadBundle('sdl3'),
        this.vfs.preloadBundle('cache-core'),
        this.vfs.preloadBundle('python-runtime'),
        this.vfs.preloadBundle('emscripten-core'),
        // P3: optional bundles — preloaded only when commands require them
        ...(ninjaHasDebug ? [this.vfs.preloadBundle('cache-debug')] : []),
        ...(ninjaHasSanitizers ? [this.vfs.preloadBundle('cache-sanitizers')] : []),
      ]);
      console.log(
        `${LOG_PREFIX}   [ninja-bypass] Preloaded compilation bundles in ${elapsed(tPreload)} (debug=${ninjaHasDebug} sanitizers=${ninjaHasSanitizers})`,
      );
    } catch (e) {
      console.warn(`${LOG_PREFIX}   [ninja-bypass] ⚠️ Bundle preload warning:`, e);
    }

    // Execute each command in order
    const stdoutChunks: string[] = [];
    const stderrChunks: string[] = [];
    for (let i = 0; i < commands.length; i++) {
      const cmd = commands[i];
      const progress = `[${i + 1}/${commands.length}]`;

      // Skip no-op commands (cmake uses `: && cmd && :` pattern)
      const effectiveCmd = cmd
        .replace(/^:\s*&&\s*/, '')
        .replace(/\s*&&\s*:$/, '')
        .trim();
      if (!effectiveCmd || effectiveCmd === ':') continue;

      const shortCmd = effectiveCmd.length > 120 ? effectiveCmd.slice(0, 117) + '...' : effectiveCmd;
      console.log(`${LOG_PREFIX}   [ninja-bypass] ${progress} ${shortCmd}`);
      options.onStdout?.(`${progress} ${shortCmd}`);

      let parts = this.parseCommand(effectiveCmd);
      if (parts.length === 0) continue;

      // Transform link commands: clang++ as linker driver can't posix_spawn
      // wasm-ld in the WASM environment, so call wasm-ld directly.
      const toolBase = parts[0].split('/').pop() ?? '';
      const isClangLink = (toolBase === 'clang' || toolBase === 'clang++') && !parts.includes('-c');
      if (isClangLink) {
        parts = this.transformLinkToWasmLd(parts);
        const shortLd = parts.join(' ');
        console.log(`${LOG_PREFIX}   [ninja-bypass] ${progress} (link rewritten) ${shortLd.length > 120 ? shortLd.slice(0, 117) + '...' : shortLd}`);
      }

      // Pre-create output directories (ninja normally does this before running
      // each build command). Without this, clang fails with "No such file or
      // directory" when trying to write object/dependency files to
      // CMakeFiles/<target>.dir/ which doesn't exist yet.
      this.ensureOutputDirectories(parts, absBuildDir);

      const result = await this.run(parts[0], parts, {
        cwd: absBuildDir,
        onStdout: (t) => {
          stdoutChunks.push(t);
          options.onStdout?.(t);
        },
        onStderr: (t) => {
          stderrChunks.push(t);
          options.onStderr?.(t);
        },
      });

      if (result.exitCode !== 0) {
        console.error(`${LOG_PREFIX}   [ninja-bypass] ${progress} FAILED (exit ${result.exitCode})`);
        return {
          exitCode: result.exitCode,
          stdout: stdoutChunks.join('\n'),
          stderr: stderrChunks.join('\n'),
        };
      }
    }

    const duration = ((performance.now() - tTotal) / 1000).toFixed(2);
    console.log(`${LOG_PREFIX}   [ninja-bypass] Build complete in ${duration}s`);
    return { exitCode: 0, stdout: stdoutChunks.join('\n'), stderr: stderrChunks.join('\n') };
  }

  /**
   * Transform a clang/clang++ link command into a direct wasm-ld invocation.
   * clang++ can't fork wasm-ld via posix_spawn in the WASM environment.
   */
  private transformLinkToWasmLd(parts: string[]): string[] {
    const objFiles: string[] = [];
    let outputFile = 'a.out';
    const extraLinkFlags: string[] = [];

    for (let i = 1; i < parts.length; i++) {
      const p = parts[i];
      if (p === '-o' && i + 1 < parts.length) {
        outputFile = parts[i + 1];
        i++; // skip output filename
        continue;
      }
      // Skip compiler-driver flags that wasm-ld doesn't understand
      if (p.startsWith('--target=') || p.startsWith('--sysroot=') || p.startsWith('-std=')) continue;
      if (p === '--target' || p === '--sysroot' || p === '-isystem') {
        i++; // skip the next argument too
        continue;
      }
      if (p.startsWith('-isystem')) continue;
      // Keep object and archive files
      if (p.endsWith('.obj') || p.endsWith('.o') || p.endsWith('.a')) {
        objFiles.push(p);
        continue;
      }
      // Keep linker-relevant flags (-L, -l, -Wl, etc.)
      if (p.startsWith('-L') || p.startsWith('-l') || p.startsWith('-Wl,')) {
        extraLinkFlags.push(p);
      }
    }

    return [
      '/usr/bin/wasm-ld',
      ...objFiles,
      '-o',
      outputFile,
      '-L/usr/lib/emscripten/cache-lib/wasm32-emscripten',
      '-lc++-noexcept',
      '-lc++abi-noexcept',
      '-lc',
      '-ldlmalloc',
      '-lcompiler_rt',
      '--entry=main',
      '--export=__wasm_call_ctors',
      '--allow-undefined',
      ...extraLinkFlags,
    ];
  }

  /**
   * Pre-create output directories for a build command.
   *
   * In a real ninja build, ninja creates output directories before running
   * each build command. Our JS-side bypass doesn't do this, so clang fails
   * with "No such file or directory" when trying to write object/dependency
   * files to CMakeFiles/<target>.dir/ which doesn't exist yet.
   *
   * This method scans the command's argv for -o (output) and -MF (depfile)
   * flags, extracts their parent directories, and creates them in the VFS
   * overlay (which VFSFS will see via write-through).
   */
  private ensureOutputDirectories(parts: string[], absBuildDir: string): void {
    const dirsToCreate = new Set<string>();

    for (let i = 0; i < parts.length; i++) {
      // -o <output> and -MF <depfile>
      if ((parts[i] === '-o' || parts[i] === '-MF') && i + 1 < parts.length) {
        const outPath = parts[i + 1];
        // Skip absolute paths that are outside the build dir (e.g. /usr/...)
        if (outPath.startsWith('/')) {
          // Only create dirs for paths within the build directory
          if (outPath.startsWith(absBuildDir + '/') || outPath === absBuildDir) {
            const dir = outPath.substring(0, outPath.lastIndexOf('/'));
            if (dir && dir !== absBuildDir) dirsToCreate.add(dir);
          }
          continue;
        }
        // Relative path — resolve against absBuildDir
        const resolved = `${absBuildDir.replace(/\/$/, '')}/${outPath}`;
        const dir = resolved.substring(0, resolved.lastIndexOf('/'));
        if (dir && dir !== absBuildDir) dirsToCreate.add(dir);
      }
    }

    for (const dir of dirsToCreate) {
      try {
        this.vfs.mkdirSync(dir);
        console.log(`${LOG_PREFIX}   [ninja-bypass] Created output directory: ${dir}`);
      } catch {
        // Directory may already exist — non-fatal
      }
    }
  }

  /**
   * Resolve include/subninja directives in a ninja build file by inlining
   * the referenced files. CMake generates rules in a separate rules.ninja.
   */
  private async resolveNinjaIncludes(content: string, buildDir: string): Promise<string> {
    const lines = content.split('\n');
    const resolved: string[] = [];
    for (const line of lines) {
      const trimmed = line.trim();
      if (trimmed.startsWith('include ') || trimmed.startsWith('subninja ')) {
        const keyword = trimmed.startsWith('include') ? 'include' : 'subninja';
        const relPath = trimmed.slice(keyword.length + 1).trim();
        const absPath = relPath.startsWith('/') ? relPath : `${buildDir}/${relPath}`;
        const fileData = await this.vfs.fetchFile(absPath);
        if (fileData) {
          const included = new TextDecoder().decode(fileData);
          console.log(`${LOG_PREFIX}   [ninja-bypass] Resolved ${keyword} ${relPath} (${fileData.length}B)`);
          // Recursively resolve nested includes
          const nested = await this.resolveNinjaIncludes(included, buildDir);
          resolved.push(nested);
        } else {
          console.warn(`${LOG_PREFIX}   [ninja-bypass] ⚠️ ${keyword} ${relPath}: file not found`);
        }
      } else {
        resolved.push(line);
      }
    }
    return resolved.join('\n');
  }

  /**
   * Parse a build.ninja file and extract the ordered list of build commands.
   * Handles CMake-generated ninja files: rules with command templates,
   * build edges with variable substitution ($in, $out, edge variables).
   */
  private parseBuildNinja(content: string): string[] {
    const rules = new Map<string, string>(); // ruleName → command template
    const globalVars = new Map<string, string>();

    interface BuildEdge {
      outputs: string[];
      rule: string;
      inputs: string[];
      orderOnly: string[];
      vars: Map<string, string>;
    }

    const edges: BuildEdge[] = [];
    const lines = content.split('\n');
    let i = 0;

    // Current context for indented lines
    let currentRule: string | null = null;
    let currentRuleVars = new Map<string, string>();
    let currentEdge: BuildEdge | null = null;

    while (i < lines.length) {
      const raw = lines[i];
      const trimmed = raw.trimEnd();

      // Handle line continuations ($\n)
      let line = trimmed;
      while (line.endsWith('$') && i + 1 < lines.length) {
        i++;
        line = line.slice(0, -1) + lines[i].trimEnd();
      }

      // Skip comments and empty lines
      if (line.trim() === '' || line.trim().startsWith('#')) {
        i++;
        continue;
      }

      // Indented line — belongs to current rule or build edge
      if (raw.startsWith('  ') || raw.startsWith('\t')) {
        const kv = line.trim();
        const eqIdx = kv.indexOf(' = ');
        if (eqIdx > 0) {
          const key = kv.slice(0, eqIdx).trim();
          const value = kv.slice(eqIdx + 3);
          if (currentRule) {
            currentRuleVars.set(key, value);
          } else if (currentEdge) {
            currentEdge.vars.set(key, value);
          }
        } else if (kv.startsWith('command = ') && currentRule) {
          currentRuleVars.set('command', kv.slice('command = '.length));
        }
        i++;
        continue;
      }

      // Flush previous rule
      if (currentRule) {
        const cmd = currentRuleVars.get('command');
        if (cmd) rules.set(currentRule, cmd);
        currentRule = null;
        currentRuleVars = new Map();
      }
      // Flush previous edge
      if (currentEdge) {
        edges.push(currentEdge);
        currentEdge = null;
      }

      // Parse non-indented lines
      if (line.startsWith('rule ')) {
        currentRule = line.slice(5).trim();
        currentRuleVars = new Map();
      } else if (line.startsWith('build ')) {
        // build output1 output2: ruleName input1 input2 | orderOnly1
        const afterBuild = line.slice(6);
        const colonIdx = afterBuild.indexOf(': ');
        if (colonIdx < 0) {
          i++;
          continue;
        }

        const outputsPart = afterBuild.slice(0, colonIdx).trim();
        const rest = afterBuild.slice(colonIdx + 2).trim();

        // Split rest into rule + inputs + order-only deps
        const restParts = rest.split(/\s+/);
        const ruleName = restParts[0] || '';
        const inputs: string[] = [];
        const orderOnly: string[] = [];
        let isOrderOnly = false;
        for (let j = 1; j < restParts.length; j++) {
          if (restParts[j] === '|' || restParts[j] === '||') {
            isOrderOnly = true;
            continue;
          }
          (isOrderOnly ? orderOnly : inputs).push(restParts[j]);
        }

        // Skip phony rules and cmake internal rules
        if (ruleName === 'phony' || ruleName === 'RERUN_CMAKE' || ruleName === 'CLEAN' || ruleName === 'HELP' || ruleName === 'CUSTOM_COMMAND') {
          i++;
          continue;
        }

        currentEdge = {
          outputs: outputsPart.split(/\s+/).filter(Boolean),
          rule: ruleName,
          inputs,
          orderOnly,
          vars: new Map(),
        };
      } else if (line.includes(' = ') && !line.startsWith(' ')) {
        // Top-level variable
        const eqIdx = line.indexOf(' = ');
        const key = line.slice(0, eqIdx).trim();
        const value = line.slice(eqIdx + 3);
        globalVars.set(key, value);
      } else if (line.startsWith('default ') || line.startsWith('pool ') || line.startsWith('include ') || line.startsWith('subninja ')) {
        // Skip directives we don't need
      }

      i++;
    }

    // Flush last rule/edge
    if (currentRule) {
      const cmd = currentRuleVars.get('command');
      if (cmd) rules.set(currentRule, cmd);
    }
    if (currentEdge) {
      edges.push(currentEdge);
    }

    // Topological sort: edges whose inputs are outputs of other edges come later
    const outputMap = new Map<string, number>(); // output → edge index
    for (let ei = 0; ei < edges.length; ei++) {
      for (const out of edges[ei].outputs) {
        outputMap.set(out, ei);
      }
    }
    const visited = new Set<number>();
    const order: number[] = [];
    const visit = (idx: number) => {
      if (visited.has(idx)) return;
      visited.add(idx);
      const edge = edges[idx];
      for (const inp of edge.inputs) {
        const depIdx = outputMap.get(inp);
        if (depIdx !== undefined) visit(depIdx);
      }
      order.push(idx);
    };
    for (let ei = 0; ei < edges.length; ei++) visit(ei);

    // Expand commands with variable substitution
    const commands: string[] = [];
    for (const idx of order) {
      const edge = edges[idx];
      const cmdTemplate = rules.get(edge.rule);
      if (!cmdTemplate) {
        console.warn(`${LOG_PREFIX}   [ninja-bypass] No rule found for "${edge.rule}" — skipping`);
        continue;
      }

      // Build substitution context: edge vars > global vars > built-ins
      const vars = new Map<string, string>(globalVars);
      for (const [k, v] of edge.vars) vars.set(k, v);
      vars.set('in', edge.inputs.join(' '));
      vars.set('in_newline', edge.inputs.join('\n'));
      vars.set('out', edge.outputs.join(' '));

      // Substitute $VAR and ${VAR}
      let cmd = cmdTemplate;
      cmd = cmd.replace(/\$\{([^}]+)\}|\$([a-zA-Z_][a-zA-Z0-9_]*)/g, (_match, braced, bare) => {
        const name = braced || bare;
        return vars.get(name) ?? '';
      });
      // Remove escaped newlines
      cmd = cmd.replace(/\$\n\s*/g, '');

      commands.push(cmd);
    }

    return commands;
  }

  /**
   * Run a shell command string (for system() / popen() emulation).
   */
  async system(command: string, cwd?: string): Promise<number> {
    const parts = this.parseCommand(command);
    if (parts.length === 0) return 0;
    const result = await this.run(parts[0], parts, { cwd });
    return result.exitCode;
  }

  /**
   * Read a file from the kernel VFS.
   */
  async getFile(path: string): Promise<Uint8Array | null> {
    return this.vfs.fetchFile(path);
  }

  /**
   * List directory contents from the kernel VFS overlay.
   */
  async listDir(path: string): Promise<string[]> {
    try {
      return await this.vfs.overlay.readdir(path);
    } catch {
      return [];
    }
  }

  /* ---------------------------------------------------------------- */
  /*  Process spawning (micro-kernel core)                             */
  /* ---------------------------------------------------------------- */

  /**
   * Spawn an isolated WASM process for a tool invocation:
   *
   *   1. Load the module factory (cached Emscripten JS glue)
   *   2. Configure process: ENV, argv, stdout/stderr hooks, FS callbacks
   *   3. Instantiate the WASM module (creates fresh linear memory)
   *   4. Pre-populate the process FS with files from the kernel VFS
   *   5. Call main(argc, argv)
   *   6. Harvest output files back to the kernel VFS
   *   7. Discard the process (GC reclaims the linear memory)
   */
  private async spawnProcess(descriptor: ToolDescriptor, argv: string[], options: RunOptions, toolBasename: string): Promise<ToolResult> {
    const tSpawn = performance.now();
    const stdoutChunks: string[] = [];
    const stderrChunks: string[] = [];

    // Step 1: Load module factory (may be cached from a previous invocation)
    const tFactory = performance.now();
    console.log(`${LOG_PREFIX}   Step 1/4: Loading module factory for ${descriptor.modulePath}...`);
    const factory = await this.loadModuleFactory(descriptor.modulePath);
    console.log(`${LOG_PREFIX}   Step 1/4 done: factory loaded in ${elapsed(tFactory)}`);

    // Is this a Python-based tool (emcc/em++)?
    const isPythonTool = descriptor.modulePath.includes('python');

    // Is stdin provided? (interactive script mode vs build-tool mode)
    const isInteractive = !!options.stdin;

    // Build environment
    const envVars: Record<string, string> = {
      PYTHONHOME: '/usr',
      PYTHONPATH: `/usr/lib/python${this.versions.pythonMajorMinor}:/usr/lib/emscripten`,
      PATH: '/usr/bin',
      HOME: '/home/user',
      TMPDIR: '/tmp',
      EM_CONFIG: '/etc/emscripten.config',
      PYTHONDONTWRITEBYTECODE: '1',
      // Force unbuffered stdout/stderr so Python tracebacks are visible
      // before the WASM module exits (CPython's Py_FinalizeEx can fail
      // before flushing C stdio buffers).
      PYTHONUNBUFFERED: '1',
      ...(isPythonTool ? { EMCC_SKIP_SANITY_CHECK: '1' } : {}),
      // Tell sitecustomize.py not to replace sys.stdout/sys.stderr
      // when running interactively — we need Emscripten's TTY I/O.
      ...(isInteractive ? { _EMCEPTION_INTERACTIVE: '1' } : {}),
      ...(descriptor.env || {}),
      ...(options.env || {}),
    };

    // Capture a reference to the VFS for locateFile
    const vfs = this.vfs;

    // Mutable flush hook — set after instance is created so the stdin
    // wrapper can drain the Emscripten TTY stdout buffer before blocking.

    let flushStdoutTTY: (() => void) | undefined;

    // Step 2: Configure and instantiate the WASM module
    const tInst = performance.now();
    console.log(`${LOG_PREFIX}   Step 2/4: Instantiating isolated WASM process...`);

    // Use an absolute path for argv[0] so tools that self-locate by inspecting
    // their executable path (e.g. cmake's FindCMakeResources) can resolve their
    // install prefix.  /usr/bin/<tool> entries exist in the manifest as symlinks.
    const program = argv[0] || 'tool';
    const thisProgram = program.startsWith('/') ? program : `/usr/bin/${program}`;

    const moduleConfig: Record<string, unknown> = {
      // Skip callMain during init — we call it manually after FS population
      noInitialRun: true,
      // Allow the process to exit normally
      noExitRuntime: false,
      // Pass absolute argv[0] as thisProgram
      thisProgram,
      // ENV injection strategy:
      // python.mjs (Emscripten glue at /usr/lib/python.mjs) is patched to
      // merge moduleArg.ENV into local ENV variable:
      //   var ENV={};if(moduleArg&&moduleArg["ENV"]){for(var _k in moduleArg["ENV"]){ENV[_k]=moduleArg["ENV"][_k]}}
      // This ensures PYTHONHOME, PYTHONPATH, etc. are available before
      // getEnvStrings() is called by Python's environ_get syscall.
      ENV: envVars,
      // Capture abort info
      onAbort: (what: unknown) => {
        console.error(`${LOG_PREFIX} ⚠️ onAbort called with:`, what);
      },
      // TTY hooks
      print: (text: string) => {
        stdoutChunks.push(text);
        if (isPythonTool) console.log(`${LOG_PREFIX}   [print] ${text}`);
        options.onStdout?.(text);
      },
      printErr: (text: string) => {
        stderrChunks.push(text);
        console.error(`${LOG_PREFIX}   [printErr] ${text}`);
        options.onStderr?.(text);
      },
      // For interactive scripts, wrap stdin to flush the Emscripten TTY
      // stdout buffer before blocking on Atomics.wait(). Without this,
      // input() prompts (no trailing newline) stay buffered in the TTY
      // device and never reach the print callback, causing a deadlock:
      // the user can't see the prompt, so they never type, so stdin
      // never returns.  flushStdoutTTY is set after instance creation.
      //
      // CRITICAL: After returning a newline byte (10), the next call
      // must return null to signal "end of available data" to
      // Emscripten's FS.createDevice read handler. Without this, the
      // read handler loops calling input() for `length` bytes (often
      // 8192), blocking on Atomics.wait after the line is consumed.
      // The read() syscall never returns the already-read bytes, and
      // Python's input() deadlocks. Returning null mimics the behavior
      // of Emscripten's default FS_stdin_getChar which returns null
      // when its internal buffer is exhausted.
      stdin: options.stdin
        ? (() => {
          let afterNewline = false;
          return () => {
            flushStdoutTTY?.();
            if (afterNewline) {
              afterNewline = false;
              return null;
            }
            const byte = options.stdin!();
            if (byte === 10) afterNewline = true;
            return byte;
          };
        })()
        : () => null,
      // Resolve .wasm URL from the CDN manifest
      locateFile: (path: string) => {
        if (path.endsWith('.wasm')) {
          const url = vfs.getUrl(descriptor.modulePath);
          if (url) {
            // Blob URLs are ready to use; CDN URLs need compression suffix stripped
            return url.startsWith('blob:') ? url : url.replace(/\.(br|gz)$/, '');
          }
        }
        return path;
      },
      // Override arguments for WASM main (used by Emscripten's callMain)
      arguments: argv.slice(1),
    };

    // Provide systemCallbackSync + systemCallback for system() interception.
    // systemCallbackSync is a synchronous fast-path for commands that can be
    // answered without spawning a WASM subprocess (e.g. version probes).
    // This avoids reliance on Asyncify stack unwinding, which may not work
    // if the WASM binary wasn't compiled with the system() call path
    // properly instrumented for Asyncify.
    // systemCallback is the async fallback that uses Asyncify.handleAsync.
    let instanceRef: EmscriptenInstance | null = null;
    {
      // ── Synchronous fast-path ──────────────────────────────────
      moduleConfig['systemCallbackSync'] = (cmdStr: string): number | undefined => {
        if (cmdStr === '__dispatch_subprocess' && instanceRef) {
          try {
            const requestData = String(instanceRef.FS.readFile('/tmp/.subprocess_request', { encoding: 'utf8' }));
            const request = JSON.parse(requestData) as { cmd: string; cwd: string };
            const parts = this.parseCommand(request.cmd);
            if (parts.length === 0) {
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
              return (0 << 8) | 0;
            }
            const subBasename = parts[0].split('/').pop() ?? '';
            const isVersionCheck = parts.includes('--version') || parts.includes('-v');

            // Fast-path: ninja version probe — cmake's Ninja generator requires this.
            // Respond synchronously so cmake doesn't need Asyncify to suspend.
            if (subBasename === 'ninja' && isVersionCheck) {
              console.log(`${LOG_PREFIX}   [subprocess] Sync fast-path: ninja --version → 1.12.1`);
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '1.12.1\n');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
              return (0 << 8) | 0;
            }

            // Fast-path: ninja restat — cmake's post-configure cleanup step.
            // Refreshes file timestamps; non-critical and safe to skip.
            // cmake calls this via system() which requires Asyncify unwind,
            // but cmake.wasm can't unwind — so handle synchronously.
            if (subBasename === 'ninja' && parts.includes('-t') && parts.includes('restat')) {
              console.log(`${LOG_PREFIX}   [subprocess] Sync fast-path: ninja -t restat → skip (non-critical)`);
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
              return (0 << 8) | 0;
            }

            // Fast-path: any OPTIONAL_TOOLS version check
            if (OPTIONAL_TOOLS.has(subBasename) && isVersionCheck) {
              console.log(`${LOG_PREFIX}   [subprocess] Sync fast-path: ${subBasename} --version → skip (optional tool)`);
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
              return (0 << 8) | 0;
            }
          } catch {
            // Fall through to async path
          }
        }
        return undefined; // Not handled — use async path
      };

      // ── Async path (requires Asyncify) ─────────────────────────
      moduleConfig['systemCallback'] = async (cmdStr: string): Promise<number> => {
        if (cmdStr === '__dispatch_subprocess' && instanceRef) {
          try {
            // Read the subprocess request from the process FS
            const requestData = String(instanceRef.FS.readFile('/tmp/.subprocess_request', { encoding: 'utf8' }));
            const request = JSON.parse(requestData) as { cmd: string; cwd: string };
            console.log(`${LOG_PREFIX}   [subprocess] Dispatching: ${request.cmd.slice(0, 120)}...`);

            // Parse the command and run through the tool runner
            const parts = this.parseCommand(request.cmd);
            if (parts.length === 0) {
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
              return 0;
            }

            const subBasename = parts[0].split('/').pop() ?? parts[0];
            const isVersionCheck = parts.includes('--version') || parts.includes('-v');

            // With VFSFS write-through, writes from the parent process
            // go to VFS immediately.  The child process's VFSFS mount
            // will lazily read those files via Asyncify hooks.

            const subStdout: string[] = [];
            const subStderr: string[] = [];
            const subResult = await this.run(parts[0], parts, {
              cwd: request.cwd || options.cwd,
              onStdout: (t) => subStdout.push(t),
              onStderr: (t) => subStderr.push(t),
              isInfoQuery: isVersionCheck,
            });

            // cmake's Ninja generator probes `${CMAKE_MAKE_PROGRAM} --version`
            // and expects a non-empty semantic version string.
            // In some WASM/browser runs, ninja executes but emits empty stdout,
            // which cmake parses as "version ()" and hard-fails generation.
            // Normalize this probe to a stable version output.
            const isNinjaVersionProbe = subBasename === 'ninja' && isVersionCheck;
            let effectiveExitCode = subResult.exitCode;
            if (isNinjaVersionProbe) {
              const versionText = subStdout.join('\n').trim();
              if (!versionText || effectiveExitCode !== 0) {
                subStdout.length = 0;
                subStdout.push('1.12.1\n');
                effectiveExitCode = 0;
                const fallbackMsg = `${LOG_PREFIX}   [subprocess] ninja --version probe was unusable (exit=${subResult.exitCode}, stdout="${versionText}"); using fallback version 1.12.1`;
                console.warn(fallbackMsg);
                options.onStderr?.(fallbackMsg);
              }
            }

            // With VFSFS write-through, the child's output files are
            // already in VFS.  The parent's VFSFS will lazily load them
            // on next access via Asyncify hooks.  No re-population needed.

            // Write results back to the process FS
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', subStdout.join('\n'));
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', subStderr.join('\n'));

            console.log(`${LOG_PREFIX}   [subprocess] Done: exitCode=${subResult.exitCode}`);
            if (subStderr.length > 0) {
              for (const line of subStderr) {
                console.error(`${LOG_PREFIX}   [subprocess] stderr: ${line}`);
              }
            }

            // Optional tools may crash on actual processing passes even though
            // --version works fine. Treat their failures as non-fatal.
            if (effectiveExitCode !== 0 && OPTIONAL_TOOLS.has(subBasename)) {
              console.log(`${LOG_PREFIX}   [subprocess] Optional tool "${subBasename}" failed (exit ${effectiveExitCode}) — treating as non-fatal`);
              effectiveExitCode = 0;
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
            }
            // Return in _W_EXITCODE format: (exitCode << 8) | signal
            const waitStatus = (effectiveExitCode << 8) | 0;
            console.log(`${LOG_PREFIX}   [subprocess] Returning wait-status ${waitStatus} to parent WASM via Asyncify`);
            return waitStatus;
          } catch (e) {
            const msg = e instanceof Error ? e.message : String(e);
            console.error(`${LOG_PREFIX}   [subprocess] Error: ${msg}`);
            if (instanceRef) {
              instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
              instanceRef.FS.writeFile('/tmp/.subprocess_stderr', msg);
            }
            return (1 << 8) | 0;
          }
        }

        // --- __dispatch_curl: libcurl-lite HTTP bridge ---
        if (cmdStr === '__dispatch_curl' && instanceRef) {
          try {
            const reqText = String(instanceRef.FS.readFile('/tmp/.curl_request', { encoding: 'utf8' }));
            const lines = reqText.split('\n').filter((l) => l.length > 0);
            if (lines.length === 0) return (1 << 8) | 0;

            // Line 0: "METHOD URL"
            const spaceIdx = lines[0].indexOf(' ');
            const method = spaceIdx > 0 ? lines[0].slice(0, spaceIdx) : 'GET';
            const url = spaceIdx > 0 ? lines[0].slice(spaceIdx + 1) : lines[0];

            // Remaining lines: headers (real + pseudo X-Curl-* directives)
            const headers = new Headers();
            let followRedirects = false;
            let timeoutMs = 0;
            for (let i = 1; i < lines.length; i++) {
              const colonIdx = lines[i].indexOf(':');
              if (colonIdx <= 0) continue;
              const name = lines[i].slice(0, colonIdx).trim();
              const value = lines[i].slice(colonIdx + 1).trim();
              if (name === 'X-Curl-Follow') {
                followRedirects = value === '1';
                continue;
              }
              if (name === 'X-Curl-Timeout') {
                timeoutMs = parseInt(value, 10) * 1000;
                continue;
              }
              headers.set(name, value);
            }

            // Read body if present
            let body: Uint8Array | undefined;
            try {
              body = instanceRef.FS.readFile('/tmp/.curl_request_body');
              if (body.length === 0) body = undefined;
            } catch {
              body = undefined;
            }

            console.log(`${LOG_PREFIX}   [curl] ${method} ${url.slice(0, 120)}...`);

            const fetchInit: RequestInit = {
              method,
              headers,
              redirect: followRedirects ? 'follow' : 'manual',
            };
            if (body && method !== 'GET' && method !== 'HEAD') {
              fetchInit.body = body as unknown as BodyInit;
            }

            let response: Response;
            if (timeoutMs > 0) {
              const controller = new AbortController();
              const timer = setTimeout(() => controller.abort(), timeoutMs);
              fetchInit.signal = controller.signal;
              try {
                response = await fetch(url, fetchInit);
              } finally {
                clearTimeout(timer);
              }
            } else {
              response = await fetch(url, fetchInit);
            }

            // Write response metadata: line1 = status, then headers
            const respLines: string[] = [String(response.status)];
            response.headers.forEach((v, k) => {
              respLines.push(`${k}: ${v}`);
            });
            instanceRef.FS.writeFile('/tmp/.curl_response', respLines.join('\n') + '\n');

            // Write response body
            const respBody = new Uint8Array(await response.arrayBuffer());
            instanceRef.FS.writeFile('/tmp/.curl_response_body', respBody);

            console.log(`${LOG_PREFIX}   [curl] Done: status=${response.status} body=${respBody.length}B`);
            return 0;
          } catch (e) {
            const msg = e instanceof Error ? e.message : String(e);
            console.error(`${LOG_PREFIX}   [curl] Error: ${msg}`);
            if (instanceRef) {
              instanceRef.FS.writeFile('/tmp/.curl_response', '0\n');
              instanceRef.FS.writeFile('/tmp/.curl_response_body', new Uint8Array(0));
            }
            return (1 << 8) | 0;
          }
        }

        // Unknown system() command — return ENOSYS
        console.warn(`${LOG_PREFIX}   [systemCallback] Unknown command: "${cmdStr.slice(0, 120)}" — returning ENOSYS (-52)`);
        return -52;
      };
    }

    let instance: EmscriptenInstance;
    try {
      instance = await (factory as ModuleFactory)(moduleConfig);
      instanceRef = instance;
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      console.error(`${LOG_PREFIX} ❌ Failed to instantiate WASM module ${descriptor.modulePath}:`, msg);
      return {
        exitCode: 1,
        stdout: stdoutChunks.join('\n'),
        stderr: `Failed to instantiate ${descriptor.modulePath}: ${msg}`,
      };
    }

    console.log(`${LOG_PREFIX}   Step 2/4 done: process instantiated in ${elapsed(tInst)}`);

    if ((toolBasename === 'clang' || toolBasename === 'wasm-ld' || toolBasename === 'lld') && typeof instance.callMain === 'function') {
      const callMainSource = String(instance.callMain);
      console.log(
        `${LOG_PREFIX}   callMain patch probe: hasWhenDone=${callMainSource.includes('Asyncify.whenDone')} hasCurrData=${callMainSource.includes('Asyncify.currData')} head=${JSON.stringify(callMainSource.slice(0, 220))}`,
      );
    }

    // Guard: if the factory returned an object without FS (stub .mjs that
    // has no real WASM module behind it), treat the tool as a no-op.
    // Tools like llvm-objcopy, llvm-ar, llvm-nm, llc may not have compiled
    // WASM modules — they are optional post-processing tools whose absence
    // does not prevent a working output binary.
    if (!instance.FS) {
      console.warn(`${LOG_PREFIX}   ⚠️ No FS on instance — stub module, returning no-op (exit 0)`);
      console.log(`${LOG_PREFIX}   Process complete (no-op stub, total spawn: ${elapsed(tSpawn)})`);
      return {
        exitCode: 0,
        stdout: stdoutChunks.join('\n'),
        stderr: stderrChunks.join('\n'),
      };
    }

    // Step 3: Mount VFSFS + install Asyncify hooks for on-demand file loading
    const tFS = performance.now();
    console.log(`${LOG_PREFIX}   Step 3/4: Mounting VFSFS + Asyncify hooks...`);
    moduleConfig['__modulePath'] = descriptor.modulePath;
    const { fileData, protectedPaths } = this.setupProcessFS(instance, moduleConfig, options);

    // For interactive scripts, replace the Emscripten TTY output ops with
    // character-at-a-time forwarding. The default TTY buffers until \n and
    // only then calls the `print` callback — this means input() prompts
    // (no trailing \n) never appear and the process deadlocks.  By
    // overriding put_char we forward every byte (including \n) to
    // onStdout/onStderr immediately, giving the user a live terminal.
    if (isInteractive) {
      try {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const streams = (instance.FS as any).streams;
        if (streams) {
          for (const fd of [1, 2]) {
            const stream = streams[fd];
            if (!stream?.tty) continue;
            const isFd1 = fd === 1;
            // Clear any pre-existing buffer
            stream.tty.output = [];
            // Override put_char: forward every character immediately
            const origOps = stream.stream_ops;
            stream.stream_ops = {
              ...origOps,
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              write: (s: any, buffer: Uint8Array, offset: number, length: number) => {
                if (!length) return 0;
                const text = new TextDecoder().decode(buffer.subarray(offset, offset + length));
                console.log(`${LOG_PREFIX}   [TTY-fd${fd}] write ${length}B: ${JSON.stringify(text.slice(0, 120))}`);
                if (isFd1) {
                  stdoutChunks.push(text);
                  options.onStdout?.(text);
                } else {
                  stderrChunks.push(text);
                  options.onStderr?.(text);
                }
                return length;
              },
            };
          }
          console.log(`${LOG_PREFIX}   Installed unbuffered TTY output for interactive mode`);
        }
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to install unbuffered TTY:`, e);
      }
      // flushStdoutTTY is no longer needed — output is unbuffered.
      // Keep it as a no-op so the stdin wrapper doesn't break.
      flushStdoutTTY = () => { };
    }

    // For Python-based tools, inject the subprocess shim to replace stdlib subprocess
    if (isPythonTool) {
      // Inject shim into the VFSFS fileData map (not MEMFS writeFile)
      try {
        const shimBytes = typeof SUBPROCESS_SHIM === 'string' ? new TextEncoder().encode(SUBPROCESS_SHIM) : SUBPROCESS_SHIM;
        const pyLibDir = `/usr/lib/python${this.versions.pythonMajorMinor}`;
        fileData.set(`${pyLibDir}/subprocess.py`, shimBytes as Uint8Array);
        console.log(`${LOG_PREFIX}   Injected subprocess shim`);

        // Poison __pycache__/*.pyc entries for shimmed modules.
        // Even though pre-warming filters skip these .pyc files, VFSFS can
        // still lazily load them from IDB on demand during Python's import.
        // By placing an invalid (empty) entry in fileData, VFSFS's lookup()
        // returns the empty content synchronously, which Python recognizes as
        // an invalid .pyc and falls back to the .py source (our shim).
        const pyVer = this.versions.pythonMajorMinor.replace('.', '');
        const poisonModules = ['subprocess', 'sitecustomize'];
        for (const modName of poisonModules) {
          const pycPath = `${pyLibDir}/__pycache__/${modName}.cpython-${pyVer}.pyc`;
          fileData.set(pycPath, new Uint8Array(0));
        }
        console.log(`${LOG_PREFIX}   Poisoned ${poisonModules.length} __pycache__/*.pyc entries to prevent import bypass`);
      } catch {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to inject subprocess shim`);
      }

      // Inject a sitecustomize.py that:
      // 1. Replaces sys.stderr with a safe file-backed writer (fd 2 is
      //    broken in WASM — WASI errno EBADF=8 on write) for build tools.
      // 2. Installs a custom excepthook that writes unhandled exceptions
      //    to /tmp/python_error.txt so the tool-runner can read them after
      //    the process exits (Python's normal stderr is broken in WASM).
      // 3. In interactive mode (_EMCEPTION_INTERACTIVE=1), skips the
      //    stdout/stderr replacement so Emscripten's TTY I/O works
      //    (print/printErr callbacks fire for every write).
      try {
        const SITE_CUSTOMIZE = `
import sys, io, os, traceback as _tb

_interactive = os.environ.get('_EMCEPTION_INTERACTIVE') == '1'

# Write a marker file so we know sitecustomize.py ran
try:
    with open('/tmp/site_init.ok', 'w') as _f:
        _f.write('sitecustomize loaded\\n')
        _f.write(f'sys.path = {sys.path}\\n')
        _f.write(f'sys.argv = {sys.argv}\\n')
        _f.write(f'interactive = {_interactive}\\n')
        _f.write(f'sys.stdout = {sys.stdout!r}\\n')
        _f.write(f'sys.stderr = {sys.stderr!r}\\n')
        _f.write(f'sys.stdin  = {sys.stdin!r}\\n')
except: pass

if not _interactive:
    class _SafeStderr(io.TextIOBase):
        """File-backed stderr replacement since WASM fd 2 yields EBADF."""
        def write(self, s):
            try:
                with open('/tmp/stderr.log', 'a') as f:
                    f.write(str(s))
            except: pass
            return len(str(s))
        def flush(self): pass
        def writable(self): return True
        @property
        def encoding(self): return 'utf-8'
        @property
        def errors(self): return 'backslashreplace'

    class _SafeStdout(io.TextIOBase):
        """File-backed stdout replacement since WASM fd 1 may be None/invalid."""
        def write(self, s):
            try:
                with open('/tmp/stdout.log', 'a') as f:
                    f.write(str(s))
            except: pass
            return len(str(s))
        def flush(self): pass
        def writable(self): return True
        @property
        def encoding(self): return 'utf-8'
        @property
        def errors(self): return 'backslashreplace'

    if sys.stdout is None:
        sys.stdout = _SafeStdout()
        sys.__stdout__ = sys.stdout

    sys.stderr = _SafeStderr()
    sys.__stderr__ = sys.stderr
else:
    # Interactive mode: Emscripten's TTY is wired to forward output to
    # the browser terminal, but CPython may set sys.stdout/sys.stderr to
    # None when it cannot detect a valid terminal at startup.  Always
    # re-create stdio wrappers around fd 0/1/2 so print()/input() work.
    try:
        sys.stdout = io.TextIOWrapper(
            io.BufferedWriter(io.FileIO(1, 'w', closefd=False)),
            line_buffering=True, write_through=True)
        sys.__stdout__ = sys.stdout
    except Exception as _e:
        with open('/tmp/site_init.ok', 'a') as _f:
            _f.write(f'stdout reconstruction failed: {_e}\\n')
    try:
        sys.stderr = io.TextIOWrapper(
            io.BufferedWriter(io.FileIO(2, 'w', closefd=False)),
            line_buffering=True, write_through=True)
        sys.__stderr__ = sys.stderr
    except Exception as _e:
        with open('/tmp/site_init.ok', 'a') as _f:
            _f.write(f'stderr reconstruction failed: {_e}\\n')
    try:
        sys.stdin = io.TextIOWrapper(
            io.BufferedReader(io.FileIO(0, 'r', closefd=False)))
        sys.__stdin__ = sys.stdin
    except Exception as _e:
        with open('/tmp/site_init.ok', 'a') as _f:
            _f.write(f'stdin reconstruction failed: {_e}\\n')

_orig = sys.excepthook
def _hook(t, v, tb):
    try:
        with open('/tmp/python_error.txt', 'w') as f:
            _tb.print_exception(t, v, tb, file=f)
    except: pass
    try: _orig(t, v, tb)
    except: pass
sys.excepthook = _hook
`;
        fileData.set(`/usr/lib/python${this.versions.pythonMajorMinor}/sitecustomize.py`, new TextEncoder().encode(SITE_CUSTOMIZE));
        console.log(`${LOG_PREFIX}   Injected sitecustomize.py (safe stderr + exception capture)`);
      } catch {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to inject sitecustomize.py`);
      }

      // Patch ports/__init__.py:
      // Skip fetch_port_artifact entirely when ports are pre-bundled.
      //    The marker-check flow (get_dir → os.makedirs → lookup alias → CDN fetch)
      //    is fragile in the WASM sandbox: VFSFS alias resolution can be shadowed
      //    by MEMFS directory nodes created during os.makedirs. Since all ports are
      //    pre-built and cached, we short-circuit the entire download check.
      try {
        const portsInitPath = '/usr/lib/emscripten/tools/ports/__init__.py';
        // This will be checked by fileData.has() during pre-warming,
        // preventing the bundle's old version from overwriting our patch.
        const existingBytes = await this.vfs.fetchFile?.(portsInitPath).catch(() => null);
        console.log(`${LOG_PREFIX}   ports/__init__.py fetchFile returned ${existingBytes ? existingBytes.byteLength + ' bytes' : 'null'}`);
        if (existingBytes) {
          let src = new TextDecoder().decode(existingBytes);
          let patched = false;
          // Patch: skip fetch_port_artifact unconditionally.
          // All ports are pre-built in CDN bundles; the library existence
          // check in cache.get() handles the rest via path aliases.
          // We cannot rely on FROZEN_CACHE (must be False to avoid unreachable
          // traps) nor on up_to_date() (VFSFS async alias resolution is
          // fragile during os.makedirs + marker read).
          const fetchSig =
            '    """This function only fetches the port and returns True when the port is up to date, False otherwise"""\n    # To compute the sha512 hash';
          if (src.includes(fetchSig)) {
            src = src.replace(
              fetchSig,
              '    """This function only fetches the port and returns True when the port is up to date, False otherwise"""\n    return True  # Emception: all ports pre-built in CDN bundles\n    # To compute the sha512 hash',
            );
            patched = true;
          }
          if (patched) {
            fileData.set(portsInitPath, new TextEncoder().encode(src));
            // Poison the compiled .pyc so Python falls back to our patched .py
            const pyVer = this.versions.pythonMajorMinor.replace('.', '');
            const pycPath = `/usr/lib/emscripten/tools/ports/__pycache__/__init__.cpython-${pyVer}.pyc`;
            fileData.set(pycPath, new Uint8Array(0));
            console.log(`${LOG_PREFIX}   Patched ports/__init__.py + poisoned ${pycPath}`);
          }
        }
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ ports/__init__.py patch error: ${e}`);
      }

      // Inject system/bin scripts required by system_libs.py install_system_headers().
      // The sysroot bundle may not include these, and safe_copytree crashes on missing dirs.
      const sdlConfigContent = `#!/bin/sh\necho "emscripten sdl-config called with $*" >&2\nfor arg in "$@"; do\n  case "$arg" in\n    --cflags|--libs)\n      echo "-sUSE_SDL"\n      ;;\n    --version)\n      echo "1.3.0"\n      ;;\n  esac\ndone\n`;
      const sdl2ConfigContent = `#!/bin/sh\necho "emscripten sdl2-config called with $*" >&2\nfor arg in "$@"; do\n  case "$arg" in\n    --cflags|--libs)\n      echo "-sUSE_SDL=2"\n      ;;\n    --version)\n      echo "2.0.10"\n      ;;\n  esac\ndone\n`;
      fileData.set('/usr/lib/emscripten/system/bin/sdl-config', new TextEncoder().encode(sdlConfigContent));
      fileData.set('/usr/lib/emscripten/system/bin/sdl2-config', new TextEncoder().encode(sdl2ConfigContent));
      console.log(`${LOG_PREFIX}   Injected system/bin/sdl-config + sdl2-config`);

      this.installToolStubs(fileData);
    }

    // cmake also needs tool stubs so compiler detection can find clang++/clang
    // via os.path.exists() and PATH lookup before subprocess dispatch runs them.
    if (descriptor.modulePath === '/usr/lib/cmake.wasm') {
      this.installToolStubs(fileData);
    }

    // cmake: pre-seed the build directory with generated compiler info files.
    // cmake's C++ code (cmGlobalGenerator::EnableLanguage) DELETES these files
    // before running determination scripts, then expects configure_file() to
    // recreate them.  In WASM, configure_file skips recreation when the
    // early-return fires.  To survive the deletion, pre-seeded files are
    // marked as protectedPaths in the VFSFS mount — the unlink handler keeps
    // the data in RAM (fileData) so lookup() can recover them on re-access.
    // All file data flows through VFS (→ IndexedDB), never MEMFS.
    if (descriptor.modulePath === '/usr/lib/cmake.wasm') {
      this.preSeedCmakeBuildDir(argv, fileData, protectedPaths, instance, options.onStderr);

      // ── Runtime diagnostics: print to stderr so user sees in terminal ──
      const diag: string[] = [];
      diag.push(`[cmake-diag] fileData entries (${fileData.size}):`);
      for (const k of fileData.keys()) {
        if (k.includes('CMakeFiles') || k.includes('cmake')) {
          diag.push(`  ${k} (${fileData.get(k)!.length}B)`);
        }
      }
      diag.push(`[cmake-diag] protectedPaths (${protectedPaths.size}): ${[...protectedPaths].join(', ')}`);
      const dFlags = argv.filter((a) => a.startsWith('-D'));
      diag.push(`[cmake-diag] -D flags (${dFlags.length}): ${dFlags.join(' ')}`);
      diag.push(`[cmake-diag] argv: ${argv.join(' ')}`);
      const diagMsg = diag.join('\n');
      console.log(diagMsg);
      options.onStderr?.(diagMsg);
    }
    console.log(`${LOG_PREFIX}   Step 3/4 done: FS set up in ${elapsed(tFS)}`);

    // Preload bundles required by specific tools so that their files
    // are in IDB before callMain.  This is critical because cmake
    // checks CMAKE_ROOT existence very early during startup, and the Asyncify
    // hooks may not have fetched the data in time.
    if (descriptor.modulePath === '/usr/lib/cmake.wasm') {
      const tPreload = performance.now();
      try {
        await Promise.all([this.vfs.preloadBundle('usr-share'), this.vfs.preloadBundle('usr-bin')]);
        console.log(`${LOG_PREFIX}   Preloaded usr-share + usr-bin bundles for cmake in ${elapsed(tPreload)}`);
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to preload bundles:`, e);
      }

      // ── Post-preload diagnostic: verify sysroot early-return patch ──
      const diag2: string[] = [];
      const cxxDetPath = '/usr/share/cmake-4.3/Modules/CMakeDetermineCXXCompiler.cmake';
      try {
        const cxxDetData = await this.vfs.fetchFile(cxxDetPath);
        if (cxxDetData) {
          const cxxDet = new TextDecoder().decode(cxxDetData);
          const hasEarlyReturn = cxxDet.includes('CMAKE_CXX_COMPILER AND CMAKE_CXX_COMPILER_ID AND CMAKE_CXX_COMPILER_FORCED');
          diag2.push(`[cmake-diag] sysroot ${cxxDetPath}: loaded (${cxxDetData.length}B), early-return=${hasEarlyReturn}`);
          if (!hasEarlyReturn) {
            diag2.push(`[cmake-diag] ⚠️ SYSROOT MISSING EARLY-RETURN PATCH! CDN bundle is stale.`);
          }
        } else {
          diag2.push(`[cmake-diag] ⚠️ sysroot ${cxxDetPath}: NOT FOUND in VFS`);
        }
      } catch (e) {
        diag2.push(`[cmake-diag] ⚠️ sysroot check failed: ${e}`);
      }
      if (diag2.length > 0) {
        const msg2 = diag2.join('\n');
        console.log(msg2);
        options.onStderr?.(msg2);
      }

      // ── Pre-callMain FS verification + self-healing ──────────────────
      // Verify that Emscripten's FS can actually see the pre-seeded cmake
      // files.  If FS.stat fails (mkdirTree/writeFile issues during preSeed),
      // retry the directory creation and file write now that the sysroot
      // bundle has been loaded into IDB (which may help VFSFS resolve paths).
      {
        const buildDir = this.extractCmakeBuildDir(argv);
        const infoDir = `${buildDir}/CMakeFiles/${CMAKE_BUILD_VERSION}`;
        const preSeedFiles: [string, string][] = [
          [`${infoDir}/CMakeSystem.cmake`, CMAKE_SYSTEM_PRESEED],
          [`${infoDir}/CMakeCXXCompiler.cmake`, CMAKE_CXX_COMPILER_PRESEED],
          [`${infoDir}/CMakeCCompiler.cmake`, CMAKE_C_COMPILER_PRESEED],
        ];
        const enc = new TextEncoder();
        const fsVerify: string[] = ['[cmake-diag] FS verification (pre-callMain):'];

        for (const [path, content] of preSeedFiles) {
          try {
            const s = instance.FS.stat(path);
            fsVerify.push(`  FS.stat(${path}): OK size=${(s as { size: number }).size}`);
          } catch (e) {
            const msg = e instanceof Error ? e.message : String(e);
            fsVerify.push(`  FS.stat(${path}): FAIL — ${msg}`);
            // Self-heal: retry creating the file now
            try {
              instance.FS.mkdirTree(infoDir);
            } catch {
              /* exists */
            }
            try {
              instance.FS.writeFile(path, enc.encode(content));
              fsVerify.push(`    → retry writeFile: OK`);
            } catch (e2) {
              const msg2 = e2 instanceof Error ? e2.message : String(e2);
              fsVerify.push(`    → retry writeFile: FAIL — ${msg2}`);
            }
          }
        }

        // Check parent dir listing
        try {
          const entries = instance.FS.readdir(infoDir) as string[];
          fsVerify.push(`  readdir(${infoDir}): [${entries.join(', ')}]`);
        } catch (e) {
          const msg = e instanceof Error ? e.message : String(e);
          fsVerify.push(`  readdir(${infoDir}): FAIL — ${msg}`);
        }

        const fsMsg = fsVerify.join('\n');
        console.log(fsMsg);
        options.onStderr?.(fsMsg);
      }

      // ── Pre-warm cmake module files into VFSFS in-memory cache ────────
      // The usr-share bundle is in IDB, but VFSFS reads from IDB are async
      // (require Asyncify). cmake's WASM binary doesn't properly unwind
      // through Asyncify, so any async FS read during callMain crashes with
      // "unreachable". Fix: fetch all cmake module files from IDB (async,
      // in JS before callMain) and write them to the Emscripten FS (sync,
      // populates VFSFS fileData Map). Then isCachedSync returns true and
      // cmake never needs Asyncify for file I/O.
      {
        const tWarm = performance.now();
        // Get file list directly from manifest bundle metadata (reliable,
        // avoids recursive readdirSync which may miss entries).
        const bundlesToWarm = ['usr-share', 'usr-bin'];
        const filePaths: string[] = [];
        for (const bundleName of bundlesToWarm) {
          const bundleFiles = this.vfs.getBundleFilePaths(bundleName);
          for (const fp of bundleFiles) {
            filePaths.push(fp);
          }
        }

        // Batch-fetch from IDB and write to Emscripten FS
        const BATCH = 50;
        let warmed = 0;
        for (let i = 0; i < filePaths.length; i += BATCH) {
          const batch = filePaths.slice(i, i + BATCH);
          const results = await Promise.all(batch.map((p) => this.vfs.fetchFile(p).catch(() => null)));
          for (let j = 0; j < batch.length; j++) {
            if (results[j]) {
              try {
                instance.FS.writeFile(batch[j], results[j]!);
                warmed++;
              } catch {
                // Non-fatal: directory creation or write failed
              }
            }
          }
        }
        console.log(`${LOG_PREFIX}   Pre-warmed ${warmed}/${filePaths.length} cmake files into VFSFS in ${elapsed(tWarm)}`);
      }
    }

    // clang: preload and pre-warm header files so #include <iostream> etc. work.
    // clang.wasm reads headers from /usr/include/c++/v1/ etc. via VFSFS.
    // Without pre-warming, these reads trigger Asyncify async hooks which may
    // fail if the usr-include bundle hasn't been loaded yet.
    if (descriptor.modulePath === '/usr/lib/clang.wasm') {
      const tPreload = performance.now();
      // Always preload sdl3 for clang. The bundle is only ~2.4MB and pre-warming
      // it into the FS before callMain is the only reliable way to avoid the
      // asyncify deadlock that happens when SDL3 headers are loaded lazily via
      // the open() syscall hook. Hint-based scoping proved too fragile across
      // the worker RPC chain and Vite's worker bundle caching.
      const needsImgui = (options.hints?.bundlesNeeded ?? []).includes('sdl3');
      try {
        await Promise.all([
          this.vfs.preloadBundle('clang-headers'),
          this.vfs.preloadBundle('usr-include'),
          this.vfs.preloadBundle('sdl3'),
          this.vfs.preloadBundle('cache-core'),
          ...(needsImgui ? [this.vfs.preloadBundle('imgui').catch(() => {})] : []),
        ]);
        console.log(
          `${LOG_PREFIX}   Preloaded clang-headers + usr-include + sdl3 + cache-core${needsImgui ? ' + imgui' : ''} bundles for clang in ${elapsed(tPreload)}`,
        );
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to preload clang bundles:`, e);
      }

      // Pre-warm header files into clang's Emscripten FS
      const tWarm = performance.now();
      const bundlesToWarm = needsImgui ? ['clang-headers', 'usr-include', 'sdl3', 'imgui'] : ['clang-headers', 'usr-include', 'sdl3'];
      const headerPaths: string[] = [];
      for (const bundleName of bundlesToWarm) {
        for (const fp of this.vfs.getBundleFilePaths(bundleName)) {
          headerPaths.push(fp);
        }
      }
      const BATCH = 100;
      let warmed = 0;
      for (let hi = 0; hi < headerPaths.length; hi += BATCH) {
        const batch = headerPaths.slice(hi, hi + BATCH);
        const results = await Promise.all(batch.map((p) => this.vfs.fetchFile(p).catch(() => null)));
        for (let j = 0; j < batch.length; j++) {
          if (results[j]) {
            try {
              instance.FS.writeFile(batch[j], results[j]!);
              warmed++;
            } catch {
              /* ignore */
            }
          }
        }
      }
      console.log(`${LOG_PREFIX}   Pre-warmed ${warmed}/${headerPaths.length} header files for clang in ${elapsed(tWarm)}`);
    }

    // lld/wasm-ld: preload and pre-warm library archives so linking works.
    // Without pre-warming .a files, wasm-ld sees "unknown file type" errors.
    if (descriptor.modulePath === '/usr/lib/lld.wasm') {
      const tPreload = performance.now();
      // Only load graphics bundles the caller explicitly requested.
      const bundlesNeeded = options.hints?.bundlesNeeded ?? [];
      const graphicsBundles = (['sdl3', 'raylib', 'allegro'] as const).filter((b) => bundlesNeeded.includes(b));
      const coreLibBundles = ['cache-core', 'cache-crt', 'cache-libc-variants', 'cache-libcxx-variants'];
      try {
        await Promise.all([
          ...coreLibBundles.map((b) => this.vfs.preloadBundle(b)),
          ...graphicsBundles.map((b) => this.vfs.preloadBundle(b)),
        ]);
        const bundleList = [...coreLibBundles, ...graphicsBundles].join(' + ');
        console.log(`${LOG_PREFIX}   Preloaded ${bundleList} bundles for lld in ${elapsed(tPreload)}`);
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to preload lld bundles:`, e);
      }

      // Pre-warm library files into lld's Emscripten FS
      const tWarm = performance.now();
      const libPaths: string[] = [];
      for (const bundleName of [...coreLibBundles, ...graphicsBundles]) {
        for (const fp of this.vfs.getBundleFilePaths(bundleName)) {
          libPaths.push(fp);
        }
      }
      let warmed = 0;
      const results = await Promise.all(libPaths.map((p) => this.vfs.fetchFile(p).catch(() => null)));
      for (let j = 0; j < libPaths.length; j++) {
        if (results[j]) {
          try {
            instance.FS.writeFile(libPaths[j], results[j]!);
            warmed++;
          } catch {
            /* ignore */
          }
        }
      }
      const warmBundles = [...coreLibBundles, ...graphicsBundles].join('+');
      console.log(`${LOG_PREFIX}   Pre-warmed ${warmed}/${libPaths.length} library files (${warmBundles}) for lld in ${elapsed(tWarm)}`);

      // Pre-warm manifest-symlink .a paths referenced in the link argv but not
      // covered by any bundle (e.g. /usr/lib/libSDL2.a is deduped by the
      // generate-bundles P0 pass to a symlink → cache-core's copy; it therefore
      // belongs to no bundle but fetchFile() resolves symlinks automatically).
      const bundleWarmedSet = new Set(libPaths);
      const symlinkArgPaths = argv.filter((a) => a.endsWith('.a') && !bundleWarmedSet.has(a));
      if (symlinkArgPaths.length > 0) {
        const tSym = performance.now();
        let symWarmed = 0;
        const symResults = await Promise.all(symlinkArgPaths.map((p) => this.vfs.fetchFile(p).catch(() => null)));
        for (let j = 0; j < symlinkArgPaths.length; j++) {
          if (symResults[j]) {
            try {
              instance.FS.writeFile(symlinkArgPaths[j], symResults[j]!);
              symWarmed++;
            } catch {
              /* ignore */
            }
          }
        }
        console.log(`${LOG_PREFIX}   Pre-warmed ${symWarmed}/${symlinkArgPaths.length} symlink .a paths for lld in ${elapsed(tSym)}`);
      }
    }

    // python (emcc/em++): preload and pre-warm Python stdlib + emscripten scripts.
    // Python's Py_Initialize() needs the `encodings` module synchronously during
    // startup. Without pre-warming, the python-runtime files are only in IDB
    // (async access via VFSFS) and Python fails with:
    //   "Fatal Python error: init_fs_encoding: failed to get the Python codec
    //    of the filesystem encoding"
    if (isPythonTool) {
      const tPreload = performance.now();
      const needsSdl3ForPython = options.hints?.bundlesNeeded?.includes('sdl3') ?? false;
      try {
        await Promise.all([
          this.vfs.preloadBundle('python-runtime'),
          this.vfs.preloadBundle('emscripten-core'),
          ...(needsSdl3ForPython ? [this.vfs.preloadBundle('sdl3')] : []),
        ]);
        console.log(`${LOG_PREFIX}   Preloaded python-runtime + emscripten-core${needsSdl3ForPython ? ' + sdl3' : ''} bundles in ${elapsed(tPreload)}`);
      } catch (e) {
        console.warn(`${LOG_PREFIX}   ⚠️ Failed to preload python bundles:`, e);
      }

      // Pre-warm Python stdlib and emscripten files into Emscripten FS.
      // Skip files already in fileData (e.g. subprocess.py shim, sitecustomize.py)
      // to avoid overwriting our injected shims with the real stdlib versions.
      // Also skip the corresponding __pycache__/*.pyc files — Python's import
      // system prefers .pyc over .py, so pre-warming the bundled .pyc would
      // bypass our shims entirely.
      const tWarm = performance.now();
      const shimmedModules = new Set(['subprocess', 'sitecustomize']);
      const bundlesToWarm = ['python-runtime', 'emscripten-core', 'sdl3'];
      const pyPaths: string[] = [];
      for (const bundleName of bundlesToWarm) {
        for (const fp of this.vfs.getBundleFilePaths(bundleName)) {
          // Skip .py files already shimmed in fileData
          if (fileData.has(fp)) continue;
          // Skip __pycache__/*.pyc files for shimmed modules
          if (fp.includes('__pycache__/')) {
            const basename = fp.split('/').pop() ?? '';
            const moduleName = basename.split('.')[0];
            if (shimmedModules.has(moduleName)) continue;
          }
          pyPaths.push(fp);
        }
      }
      const BATCH = 100;
      let warmed = 0;
      for (let i = 0; i < pyPaths.length; i += BATCH) {
        const batch = pyPaths.slice(i, i + BATCH);
        const results = await Promise.all(batch.map((p) => this.vfs.fetchFile(p).catch(() => null)));
        for (let j = 0; j < batch.length; j++) {
          if (results[j]) {
            try {
              instance.FS.writeFile(batch[j], results[j]!);
              warmed++;
            } catch {
              /* ignore — dir creation or write failed */
            }
          }
        }
      }
      console.log(`${LOG_PREFIX}   Pre-warmed ${warmed}/${pyPaths.length} python files in ${elapsed(tWarm)}`);

      // ── Subprocess dispatch via Asyncify ___syscall_openat hook ──────
      // Python's os.system() uses __emscripten_system which crashes with
      // 'unreachable' WASM traps due to Asyncify stack-unwind failures in
      // CPython's indirect call dispatch. Instead, the subprocess shim
      // opens /tmp/__dispatch_subprocess__ for reading. The patched
      // python.mjs glue intercepts this path in ___syscall_openat and
      // calls Module["subprocessDispatch"]() via Asyncify.handleAsync.
      // After it resolves, the glue opens the file normally (FS.open).
      {
        // eslint-disable-next-line @typescript-eslint/no-this-alias
        const toolRunner = this;

        moduleConfig['subprocessDispatch'] = async (): Promise<void> => {
          if (!instanceRef) {
            console.error(`${LOG_PREFIX}   [dispatch] No instance ref — cannot dispatch subprocess`);
            return;
          }

          let request: { cmd: string; cwd: string };
          try {
            const requestData = String(instanceRef.FS.readFile('/tmp/.subprocess_request', { encoding: 'utf8' }));
            request = JSON.parse(requestData) as { cmd: string; cwd: string };
          } catch (e) {
            console.error(`${LOG_PREFIX}   [dispatch] Failed to read subprocess request: ${e}`);
            instanceRef.FS.writeFile('/tmp/__dispatch_subprocess__', '1');
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', 'Failed to read subprocess request');
            return;
          }

          console.log(`${LOG_PREFIX}   [subprocess] Dispatching: ${request.cmd.slice(0, 120)}...`);

          const parts = toolRunner.parseCommand(request.cmd);
          if (parts.length === 0) {
            instanceRef.FS.writeFile('/tmp/__dispatch_subprocess__', '0');
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
            return;
          }

          const subBasename = parts[0].split('/').pop() ?? parts[0];
          const isVersionCheck = parts.includes('--version') || parts.includes('-v');

          // Ninja version probe fast-path
          if (subBasename === 'ninja' && isVersionCheck) {
            console.log(`${LOG_PREFIX}   [subprocess] Fast-path: ninja --version → 1.12.1`);
            instanceRef.FS.writeFile('/tmp/__dispatch_subprocess__', '0');
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '1.12.1\n');
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', '');
            return;
          }

          // Run the subprocess through the tool runner
          const subStdout: string[] = [];
          const subStderr: string[] = [];
          try {
            const subResult = await toolRunner.run(parts[0], parts, {
              cwd: request.cwd || options.cwd,
              onStdout: (t: string) => subStdout.push(t),
              onStderr: (t: string) => subStderr.push(t),
              isInfoQuery: isVersionCheck,
            });

            let effectiveExitCode = subResult.exitCode;

            // Ninja version probe normalization
            if (subBasename === 'ninja' && isVersionCheck) {
              const versionText = subStdout.join('\n').trim();
              if (!versionText || effectiveExitCode !== 0) {
                subStdout.length = 0;
                subStdout.push('1.12.1\n');
                effectiveExitCode = 0;
              }
            }

            // Optional tools: treat failures as non-fatal
            if (effectiveExitCode !== 0 && OPTIONAL_TOOLS.has(subBasename)) {
              console.log(`${LOG_PREFIX}   [subprocess] Optional tool "${subBasename}" failed (exit ${effectiveExitCode}) — treating as non-fatal`);
              effectiveExitCode = 0;
            }

            instanceRef.FS.writeFile('/tmp/__dispatch_subprocess__', String(effectiveExitCode));
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', subStdout.join('\n'));
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', subStderr.join('\n'));

            console.log(`${LOG_PREFIX}   [subprocess] Done: exitCode=${effectiveExitCode}`);
            if (subStderr.length > 0) {
              for (const line of subStderr) {
                console.error(`${LOG_PREFIX}   [subprocess] stderr: ${line}`);
              }
            }
          } catch (e) {
            const msg = e instanceof Error ? e.message : String(e);
            console.error(`${LOG_PREFIX}   [subprocess] Error: ${msg}`);
            instanceRef.FS.writeFile('/tmp/__dispatch_subprocess__', '1');
            instanceRef.FS.writeFile('/tmp/.subprocess_stdout', '');
            instanceRef.FS.writeFile('/tmp/.subprocess_stderr', msg);
          }
        };

        // CRITICAL: also set on the live instance Module — moduleConfig was
        // copied during factory() init, so post-init mutations don't reach
        // the Module object captured by the glue's closure.
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (instance as any)['subprocessDispatch'] = moduleConfig['subprocessDispatch'];

        console.log(
          `${LOG_PREFIX}   Installed subprocess dispatch via subprocessDispatch hook (Module["subprocessDispatch"] = ${typeof (instance as unknown as Record<string, unknown>)['subprocessDispatch']})`,
        );
      }
    }

    const isOptionalTool = OPTIONAL_TOOLS.has(toolBasename);
    const outputPath = this.getOutputPathFromArgv(argv, options.cwd);
    let preRunOutputSnapshot: Uint8Array | null = null;
    if (isOptionalTool && outputPath) {
      try {
        const existing = await this.vfs.fetchFile(outputPath);
        if (existing) {
          preRunOutputSnapshot = new Uint8Array(existing);
        }
      } catch {
        // Non-fatal: snapshot is best-effort.
      }
    }

    // Step 4: Call main(argc, argv) — the tool runs to completion
    const tRun = performance.now();
    const mainArgv = argv.slice(1); // Emscripten's callMain expects argv without argv[0]
    console.log(`${LOG_PREFIX}   Step 4/4: callMain([${mainArgv.map((a) => `"${a}"`).join(', ')}])...`);

    let exitCode: number;
    try {
      if (typeof instance.callMain === 'function') {
        // With Asyncify, callMain may return a Promise when async imports
        // (FS hooks, systemCallback) trigger stack unwinding/rewinding.
        const result: unknown = instance.callMain(mainArgv);
        if (result && typeof (result as Promise<number>).then === 'function') {
          console.log(`${LOG_PREFIX}   callMain returned a Promise — awaiting Asyncify completion...`);
          // Add a watchdog timer to detect hangs
          let resolved = false;
          const watchdog = setInterval(() => {
            if (!resolved) {
              console.warn(`${LOG_PREFIX}   ⏳ callMain still pending after ${elapsed(tRun)} — WASM may be stuck`);
            }
          }, 10_000);
          try {
            exitCode = (await (result as Promise<number>)) ?? 0;
          } finally {
            resolved = true;
            clearInterval(watchdog);
          }
        } else {
          exitCode = (result as number) ?? 0;
        }
      } else {
        // Fallback: Some modules may not export callMain
        console.warn(`${LOG_PREFIX}   ⚠️ callMain not found — module may have run during init`);
        exitCode = instance.EXITSTATUS ?? 0;
      }
      console.log(`${LOG_PREFIX}   Step 4/4 done: exitCode=${exitCode} in ${elapsed(tRun)}`);
    } catch (e: unknown) {
      // Emscripten throws to unwind the stack on process exit.
      // If the error has a 'status' field, that's the exit code.
      if (e && typeof e === 'object' && 'status' in e) {
        exitCode = (e as { status: number }).status;
        console.log(`${LOG_PREFIX}   Step 4/4 done: process exited with status=${exitCode} in ${elapsed(tRun)}`);
      } else {
        exitCode = instance.EXITSTATUS ?? 1;
        const msg = e instanceof Error ? e.message : String(e);
        // Don't treat "Program terminated with exit(0)" as an error
        if (msg.includes('exit(0)') || exitCode === 0) {
          exitCode = 0;
          console.log(`${LOG_PREFIX}   Step 4/4 done: clean exit via throw in ${elapsed(tRun)}`);
        } else {
          console.error(`${LOG_PREFIX} ❌ callMain threw: ${msg}`);
          if (e instanceof Error && e.stack) {
            console.error(`${LOG_PREFIX}   Stack trace: ${e.stack}`);
          }
          // Log WASM memory info for debugging
          try {
            const mem = (instance as Record<string, unknown>)['wasmMemory'] as WebAssembly.Memory | undefined;
            if (mem) {
              console.error(`${LOG_PREFIX}   WASM memory: ${(mem.buffer.byteLength / 1024 / 1024).toFixed(1)}MB`);
            }
          } catch {
            /* ignore */
          }
          // Log any stderr that was captured before the abort
          if (stderrChunks.length > 0) {
            console.error(`${LOG_PREFIX}   stderr before abort: ${stderrChunks.join('; ').slice(0, 500)}`);
          }
          stderrChunks.push(msg);
          options.onStderr?.(msg);
        }
      }
    }

    // Drain any remaining TTY output after the process exits (e.g. final
    // print() without trailing newline in interactive mode).
    flushStdoutTTY?.();

    // CPython-WASM finalization fix: Py_FinalizeEx() often fails in WASM
    // environments, causing Py_RunMain() to return exitcode=120 even though
    // the Python script (emcc.py) completed successfully. Exit code 120 is
    // specifically set in CPython's Modules/main.c when Py_FinalizeEx() < 0.
    // This is a known WASM limitation — finalization cleanup (flushing stdio,
    // running atexit handlers, freeing modules) doesn't fully work in MEMFS.
    if (isPythonTool && exitCode === 120) {
      console.log(`${LOG_PREFIX}   Treating exit code 120 as success (CPython Py_FinalizeEx failure in WASM)`);
      exitCode = 0;
    }

    // Optional post-processing tools can fail non-fatally in parent subprocess
    // dispatch. Because VFSFS is write-through, a failed in-place `-o` rewrite
    // may leave a corrupted artifact in shared VFS (e.g. /home/user/main.wasm)
    // and break the same run's subsequent steps. Restore the pre-run snapshot
    // on optional-tool failure.
    if (isOptionalTool && exitCode !== 0 && outputPath && preRunOutputSnapshot) {
      this.vfs.writeFileSync(outputPath, preRunOutputSnapshot);
      console.log(`${LOG_PREFIX}   Restored output artifact after optional tool failure: ${outputPath} (${preRunOutputSnapshot.length}B)`);
    }

    // Persist explicit output artifacts (e.g. -o /home/user/main.wasm) into
    // shared VFS so subsequent steps (wasi-run) can read them.
    // Keep this strict: only copy the exact -o path when it exists.
    if (exitCode === 0 && instance.FS) {
      if (outputPath) {
        const outputData = this.tryReadProcessFile(instance.FS, outputPath, fileData);
        if (outputData) {
          this.vfs.writeFileSync(outputPath, outputData);
          console.log(`${LOG_PREFIX}   Persisted output artifact: ${outputPath} (${outputData.length}B)`);
        } else {
          // Diagnostic: file not found at expected path. Dump dir listing and
          // siblings to help diagnose silent linker / cwd-relative writes.
          console.warn(`${LOG_PREFIX}   ⚠ Output artifact MISSING at ${outputPath}`);
          try {
            const dir = outputPath.substring(0, outputPath.lastIndexOf('/')) || '/';
            const entries = instance.FS.readdir(dir).filter((n: string) => n !== '.' && n !== '..');
            console.warn(`${LOG_PREFIX}     readdir(${dir}) = [${entries.join(', ')}]`);
            for (const name of entries) {
              try {
                const full = (dir === '/' ? '' : dir) + '/' + name;
                const st = instance.FS.stat(full);
                console.warn(`${LOG_PREFIX}     ${full} size=${st.size} mode=0o${(st.mode & 0o7777).toString(8)}`);
              } catch (e) {
                console.warn(`${LOG_PREFIX}     stat(${name}) failed: ${(e as Error).message}`);
              }
            }
          } catch (e) {
            console.warn(`${LOG_PREFIX}     readdir failed: ${(e as Error).message}`);
          }
          // Also try /tmp and cwd
          for (const probe of ['/tmp', '/home/user']) {
            if (probe === outputPath.substring(0, outputPath.lastIndexOf('/'))) continue;
            try {
              const entries = instance.FS.readdir(probe).filter((n: string) => n !== '.' && n !== '..');
              console.warn(`${LOG_PREFIX}     readdir(${probe}) = [${entries.join(', ')}]`);
            } catch {
              /* ignore */
            }
          }
        }
      }
    }

    // Read Python exception/stderr/stdout capture files and forward to terminal
    if (isPythonTool && instance.FS) {
      try {
        // Log sitecustomize diagnostics
        try {
          const siteInit = new TextDecoder().decode(instance.FS.readFile('/tmp/site_init.ok'));
          console.log(`${LOG_PREFIX}   site_init.ok:\n${siteInit}`);
        } catch {
          /* not found */
        }

        // Read Python exception capture file if it exists
        try {
          const errContent = new TextDecoder().decode(instance.FS.readFile('/tmp/python_error.txt'));
          if (errContent.length > 0) {
            console.log(`${LOG_PREFIX}   python_error.txt:\n${errContent}`);
          }
          for (const line of errContent.split('\n')) {
            if (line.length > 0) {
              stderrChunks.push(line);
              options.onStderr?.(line);
            }
          }
        } catch {
          /* file doesn't exist — no unhandled exception */
        }

        // Read Python stderr log (redirected from broken fd 2)
        try {
          const stderrLog = new TextDecoder().decode(instance.FS.readFile('/tmp/stderr.log'));
          if (stderrLog.length > 0) {
            console.log(`${LOG_PREFIX}   stderr.log:\n${stderrLog}`);
            for (const line of stderrLog.split('\n')) {
              if (line.length > 0) {
                stderrChunks.push(line);
                options.onStderr?.(line);
              }
            }
          }
        } catch {
          /* file doesn't exist — no stderr output */
        }

        // Read Python stdout log (when _SafeStdout was active, non-interactive mode)
        try {
          const stdoutLog = new TextDecoder().decode(instance.FS.readFile('/tmp/stdout.log'));
          if (stdoutLog.length > 0) {
            for (const line of stdoutLog.split('\n')) {
              if (line.length > 0) {
                stdoutChunks.push(line);
                options.onStdout?.(line);
              }
            }
          }
        } catch {
          /* file doesn't exist — no redirected stdout */
        }
      } catch {
        // FS dump failed — non-critical
      }
    }

    // With VFSFS write-through, output files are already in VFS.
    // No explicit harvest step needed.

    // Process is done — let GC reclaim the WASM linear memory
    console.log(`${LOG_PREFIX}   Process complete, releasing WASM instance (total spawn: ${elapsed(tSpawn)})`);

    return {
      exitCode,
      stdout: stdoutChunks.join('\n'),
      stderr: stderrChunks.join('\n'),
    };
  }

  /* ---------------------------------------------------------------- */
  /*  Process FS bridging (VFSFS mount + Asyncify hooks)                */
  /* ---------------------------------------------------------------- */

  /**
   * Set up the process FS for a tool invocation using VFSFS mounts.
   *
   * Instead of copying files or patching lookupPath, this method:
   *   1. Mounts VFSFS at /usr, /etc (backed by kernel VFS + Asyncify on-demand fetch)
   *   2. Registers path aliases for sysroot cache mapping
   *   3. Creates essential synthetic files (shim, config, stubs)
   *   4. Sets CWD
   *
   * File loading is entirely on-demand via Asyncify: the FS syscall
   * JS imports (listed in ASYNCIFY_IMPORTS) call Module["onPreOpen"]/
   * ["onPreStat"] hooks before each syscall, which fetch from CDN →
   * IDB.  Emscripten's Asyncify runtime automatically
  /**
   * Create sentinel stub files for LLVM/Binaryen/system tools in /usr/bin/.
   * cmake and Emscripten Python tools call os.path.exists() on these paths
   * for compiler detection. Actual execution routes through subprocess dispatch.
   */
  private installToolStubs(fileData: Map<string, Uint8Array>): void {
    const STUB = new TextEncoder().encode('stub\n');
    const TOOL_STUBS = [
      '/usr/bin/clang',
      '/usr/bin/clang++',
      '/usr/bin/wasm-ld',
      '/usr/bin/lld',
      '/usr/bin/llvm-ar',
      '/usr/bin/llvm-nm',
      '/usr/bin/llvm-objcopy',
      '/usr/bin/llc',
      '/usr/bin/wasm-opt',
      '/usr/bin/wasm-as',
      '/usr/bin/wasm-ctor-eval',
      '/usr/bin/wasm-emscripten-finalize',
      '/usr/bin/wasm-metadce',
      '/usr/bin/node',
      '/usr/bin/python3',
      '/usr/bin/em++',
      '/usr/bin/emcc',
      '/usr/bin/ninja',
      '/usr/bin/cmake',
    ];
    for (const stubPath of TOOL_STUBS) {
      fileData.set(stubPath, STUB);
    }
    console.log(`${LOG_PREFIX}   Created ${TOOL_STUBS.length} tool stubs in fileData`);
  }

  /**
   * Pre-seed the cmake build directory with generated compiler/system info
   * files.  cmake's C++ code (cmGlobalGenerator::EnableLanguage) DELETES
   * these files before running Determine scripts, then expects
   * configure_file() to recreate them.  The early-return in our patched
   * Determine scripts fires but skips configure_file(), so the files
   * vanish permanently.
   *
   * Fix: write files to VFSFS fileData + VFS overlay (IndexedDB), AND
   * register each path in protectedPaths.  The VFSFS unlink handler
   * skips fileData.delete() for protected paths, so cmake's delete is
   * effectively a no-op on the RAM copy.  When cmake re-reads the file,
   * VFSFS lookup() finds it in fileData and serves it.
   */
  private preSeedCmakeBuildDir(
    argv: string[],
    fileData: Map<string, Uint8Array>,
    protectedPaths: Set<string>,
    instance: EmscriptenInstance,
    onStderr?: (msg: string) => void,
  ): void {
    const buildDir = this.extractCmakeBuildDir(argv);
    const infoDir = `${buildDir}/CMakeFiles/${CMAKE_BUILD_VERSION}`;
    const enc = new TextEncoder();
    const log: string[] = [];

    // Pre-create directory tree in VFSFS
    try {
      instance.FS.mkdirTree(infoDir);
      log.push(`[cmake-preSeed] mkdirTree(${infoDir}) OK`);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      log.push(`[cmake-preSeed] ⚠️ mkdirTree(${infoDir}) FAILED: ${msg}`);
    }

    const files: [string, string][] = [
      [`${infoDir}/CMakeSystem.cmake`, CMAKE_SYSTEM_PRESEED],
      [`${infoDir}/CMakeCXXCompiler.cmake`, CMAKE_CXX_COMPILER_PRESEED],
      [`${infoDir}/CMakeCCompiler.cmake`, CMAKE_C_COMPILER_PRESEED],
    ];

    for (const [path, content] of files) {
      const data = enc.encode(content);

      // Strategy 1: Inject into VFSFS fileData map (RAM — serves via lookup)
      fileData.set(path, data);

      // Strategy 2: Write via Emscripten FS.writeFile (creates inode in hash table)
      try {
        instance.FS.writeFile(path, data);
        log.push(`[cmake-preSeed] FS.writeFile(${path}) OK (${data.length}B)`);
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        log.push(`[cmake-preSeed] FS.writeFile(${path}) FAILED: ${msg}`);
      }

      // Strategy 3: Write to VFS overlay (IndexedDB) for persistence
      try {
        this.vfs.writeFileSync(path, data);
      } catch {
        // Non-fatal: overlay may not support sync write for this path
      }

      // Mark as protected so VFSFS unlink keeps the fileData entry
      protectedPaths.add(path);
    }

    log.push(`[cmake-preSeed] Done: ${infoDir} (${files.length} files, ${protectedPaths.size} protected)`);
    const logMsg = log.join('\n');
    console.log(logMsg);
    onStderr?.(logMsg);
  }

  /**
   * Extract the build directory from cmake's argv (the -B flag).
   */
  private extractCmakeBuildDir(argv: string[]): string {
    for (let i = 0; i < argv.length; i++) {
      if (argv[i] === '-B' && i + 1 < argv.length) return argv[i + 1];
      if (argv[i].startsWith('-B') && argv[i].length > 2) return argv[i].slice(2);
    }
    return '/home/user/build'; // default
  }

  /**
   * Set up the per-process filesystem for a WASM tool invocation.
   *
   * unwinds/rewinds the WASM stack while the fetch Promise resolves.
   *
   * When `options.isInfoQuery` is true, only basic dir creation + CWD is done.
   */
  private setupProcessFS(
    instance: EmscriptenInstance,
    moduleConfig: Record<string, unknown>,
    options: RunOptions,
  ): { fileData: Map<string, Uint8Array>; protectedPaths: Set<string> } {
    const FS = instance.FS;
    const isInfoQuery = options.isInfoQuery === true;
    const isPythonDescriptor = ((moduleConfig['__modulePath'] as string) || '').includes('python');

    // For info queries (--version), just set CWD
    if (isInfoQuery) {
      console.log(`${LOG_PREFIX}     [FAST] Info query — skipping FS setup`);
      const cwd = options.cwd || '/home/user';
      try {
        FS.mkdirTree(cwd);
      } catch {
        /* exists */
      }
      try {
        FS.chdir(cwd);
      } catch {
        /* ignore */
      }
      return { fileData: new Map(), protectedPaths: new Set() };
    }

    // Path aliases for sysroot cache mapping — ALL tools need these,
    // not just Python.  When emcc spawns clang as a child process, clang
    // receives --sysroot=<CACHE>/sysroot and looks for headers under that
    // prefix.  The CDN stores them under /usr/include and /usr/lib/emscripten/cache-lib.
    //
    // We maintain aliases for BOTH possible CACHE prefixes:
    //   1. /usr/lib/emscripten/cache  — the injected config value; used when
    //      our fileData.set('/etc/emscripten.config') takes effect.
    //   2. /home/user/.emscripten_cache — the legacy sysroot config; used if
    //      the injected config is ever bypassed.
    const pathAliases = new Map<string, string>();

    // Primary: CACHE = /usr/lib/emscripten/cache (matches injected config)
    pathAliases.set('/usr/lib/emscripten/cache/sysroot/lib', '/usr/lib/emscripten/cache-lib');
    pathAliases.set('/usr/lib/emscripten/cache/sysroot/include', '/usr/include');
    pathAliases.set('/usr/lib/emscripten/cache/ports', '/usr/lib/emscripten_ports');

    // Legacy: CACHE = /home/user/.emscripten_cache
    pathAliases.set('/home/user/.emscripten_cache/sysroot/lib', '/usr/lib/emscripten/cache-lib');
    pathAliases.set('/home/user/.emscripten_cache/sysroot/include', '/usr/include');
    pathAliases.set('/home/user/.emscripten_cache/ports', '/usr/lib/emscripten_ports');

    // Mount VFSFS at system paths — all file access goes through VFS + Asyncify
    // /home is included so user files (e.g. main.cpp) written by the IDE are
    // visible to child WASM processes via the kernel VFS overlay.
    const { fileData, protectedPaths } = mountVFSFS(FS, moduleConfig, this.vfs, {
      mountPoints: ['/usr', '/etc', '/home', '/tmp'],
      pathAliases,
      runtime: instance as unknown as VFSFSRuntime,
    });

    // Sysroot scaffold (Python tools only)
    if (isPythonDescriptor) {
      // Create dirs at both CACHE prefixes for compatibility.
      for (const prefix of ['/usr/lib/emscripten/cache', '/home/user/.emscripten_cache']) {
        try {
          FS.mkdirTree(`${prefix}/sysroot/lib`);
        } catch {
          /* exists */
        }
        try {
          FS.mkdirTree(`${prefix}/sysroot/include`);
        } catch {
          /* exists */
        }
        // Write sysroot_install.stamp to fileData so VFSFS can find it
        // (FS.writeFile writes to MEMFS which VFSFS doesn't see).
        fileData.set(`${prefix}/sysroot_install.stamp`, new TextEncoder().encode('prebuilt'));
      }

      // Seed emscripten config in fileData to guarantee correct CACHE path.
      // The CDN bundle may be stale in IDB; this ensures the config is
      // consistent.  We use /usr/lib/emscripten/cache as CACHE because
      // the path aliases map sysroot/lib→cache-lib and sysroot/include→/usr/include.
      // FROZEN_CACHE must be False — setting it True causes `unreachable`
      // traps because emscripten raises on ANY missing cache file during link.
      const emscriptenConfig = `import os\nEMSCRIPTEN_ROOT = '/usr/lib/emscripten'\nLLVM_ROOT = '/usr/bin'\nBINARYEN_ROOT = '/usr'\nNODE_JS = '/usr/bin/node'\nPYTHON = '/usr/bin/python3'\nCACHE = '/usr/lib/emscripten/cache'\nFROZEN_CACHE = False\nCOMPILER_OPTS = []\n`;
      fileData.set('/etc/emscripten.config', new TextEncoder().encode(emscriptenConfig));

      console.log(`${LOG_PREFIX}     Sysroot dirs created with ${pathAliases.size} path aliases`);
    }

    // Set CWD
    const cwd = options.cwd || '/home/user';
    try {
      FS.mkdirTree(cwd);
    } catch {
      /* exists */
    }
    try {
      FS.chdir(cwd);
    } catch {
      /* ignore */
    }

    return { fileData, protectedPaths };
  }

  /* ---------------------------------------------------------------- */
  /*  WASI runtime (standalone WASM execution)                         */
  /* ---------------------------------------------------------------- */

  /**
   * Run a standalone WASM binary compiled by emcc with a minimal WASI runtime.
   *
   * When emcc compiles to a .wasm output, it produces a standalone binary that
   * uses WASI (WebAssembly System Interface) imports for I/O.  This method
   * provides fd_write (stdout/stderr), proc_exit, and stubs for the remaining
   * WASI snapshot_preview1 imports so that simple programs (printf, return 0)
   * can execute directly in the browser without Node.js.
   */
  private async runWasi(argv: string[], options: RunOptions = {}): Promise<ToolResult> {
    const tTotal = performance.now();
    // argv: ['wasi-run', '/home/user/main.wasm', ...extra args]
    const rawWasmPath = argv.length > 1 ? argv[1] : '/home/user/main.wasm';
    const wasmPath = rawWasmPath.startsWith('/')
      ? rawWasmPath
      : `${(options.cwd && options.cwd.startsWith('/') ? options.cwd : '/home/user').replace(/\/$/, '')}/${rawWasmPath}`;
    console.log(`${LOG_PREFIX} ===== WASI RUN: ${wasmPath} =====`);

    // 1. Read the WASM binary from the VFS
    const wasmBytes = await this.vfs.fetchFile(wasmPath);
    if (!wasmBytes) {
      const msg = `WASI run: compiled WASM not found: ${wasmPath}`;
      console.error(`${LOG_PREFIX} ❌ ${msg}`);
      options.onStderr?.(msg);
      return { exitCode: 1, stdout: '', stderr: msg };
    }
    console.log(`${LOG_PREFIX}   WASM binary: ${fmtSize(wasmBytes.length)}`);

    const stdoutChunks: string[] = [];
    const stderrChunks: string[] = [];
    let exitCode = 0;

    // 2. Build minimal WASI imports
    const decoder = new TextDecoder();

    // WASI fd_write: write iovs to a file descriptor and return bytes written.
    // fd 1 = stdout, fd 2 = stderr
    const fd_write = (fd: number, iovsPtr: number, iovsLen: number, nwrittenPtr: number): number => {
      if (!memory) {
        console.error(`${LOG_PREFIX}   [WASI] fd_write called but memory is not set!`);
        return 8; // EBADF
      }
      const mem = new DataView(memory.buffer);
      let totalWritten = 0;
      for (let i = 0; i < iovsLen; i++) {
        const base = iovsPtr + i * 8;
        const ptr = mem.getUint32(base, true);
        const len = mem.getUint32(base + 4, true);
        const bytes = new Uint8Array(memory.buffer, ptr, len);
        const text = decoder.decode(bytes, { stream: true });
        totalWritten += len;
        if (fd === 1) {
          stdoutChunks.push(text);
          options.onStdout?.(text);
        } else if (fd === 2) {
          stderrChunks.push(text);
          options.onStderr?.(text);
        }
      }
      mem.setUint32(nwrittenPtr, totalWritten, true);
      return 0; // ESUCCESS
    };

    // environ_sizes_get: return 0 env vars
    const environ_sizes_get = (countPtr: number, sizePtr: number): number => {
      const mem = new DataView(memory.buffer);
      mem.setUint32(countPtr, 0, true);
      mem.setUint32(sizePtr, 0, true);
      return 0;
    };

    // environ_get: no-op (zero env vars)
    const environ_get = (): number => 0;

    // args: return program name + any extra args
    const programArgs = argv.slice(1); // ['main.wasm', ...extra]
    const encodedArgs = programArgs.map((a) => new TextEncoder().encode(a + '\0'));
    const totalArgSize = encodedArgs.reduce((sum, a) => sum + a.length, 0);

    const args_sizes_get = (countPtr: number, sizePtr: number): number => {
      const mem = new DataView(memory.buffer);
      mem.setUint32(countPtr, programArgs.length, true);
      mem.setUint32(sizePtr, totalArgSize, true);
      return 0;
    };

    const args_get = (argvPtr: number, argvBufPtr: number): number => {
      const mem = new DataView(memory.buffer);
      const buf = new Uint8Array(memory.buffer);
      let offset = argvBufPtr;
      for (let i = 0; i < encodedArgs.length; i++) {
        mem.setUint32(argvPtr + i * 4, offset, true);
        buf.set(encodedArgs[i], offset);
        offset += encodedArgs[i].length;
      }
      return 0;
    };

    const proc_exit = (code: number): void => {
      exitCode = code;
      console.log(`${LOG_PREFIX}   WASI proc_exit(${code})`);
      throw new WasiExit(code);
    };

    const fd_close = (): number => 0;
    const fd_seek = (): number => 8; // EBADF — not seekable
    const stdinProvider = options.stdin ?? null;
    console.log(`${LOG_PREFIX}   [WASI-STDIN] stdinProvider=${stdinProvider ? 'SET' : 'NULL'}`);

    // Synchronous fd_read for standalone WASI binaries.
    // The worker provides stdin through a SharedArrayBuffer-backed callback,
    // so a call here can block until a byte is available without falling
    // through to EOF or leaking the keystrokes back to the shell.
    const fd_read = (fd: number, iovsPtr: number, iovsLen: number, nreadPtr: number): number => {
      const mem = new DataView(memory.buffer);
      console.log(`${LOG_PREFIX}   [WASI-STDIN] fd_read called: fd=${fd}, iovsLen=${iovsLen}`);

      if (fd !== 0) {
        mem.setUint32(nreadPtr, 0, true);
        return 8; // EBADF
      }

      if (!stdinProvider) {
        mem.setUint32(nreadPtr, 0, true);
        return 0;
      }

      for (let i = 0; i < iovsLen; i++) {
        const base = iovsPtr + i * 8;
        const ptr = mem.getUint32(base, true);
        const len = mem.getUint32(base + 4, true);
        if (len === 0) continue;
        const out = new Uint8Array(memory.buffer, ptr, len);

        const raw = stdinProvider();
        if (raw !== null && typeof raw === 'object' && 'then' in raw) {
          throw new Error('WASI stdin provider must be synchronous');
        }

        const byte = raw as number | null;
        if (byte === null || byte === -1) {
          mem.setUint32(nreadPtr, 0, true);
          return 0;
        }

        out[0] = byte === 13 ? 10 : byte;
        mem.setUint32(nreadPtr, 1, true);
        return 0;
      }

      mem.setUint32(nreadPtr, 0, true);
      return 0;
    };

    const fd_fdstat_get = (fd: number, statPtr: number): number => {
      const mem = new DataView(memory.buffer);
      // fs_filetype: REGULAR_FILE=4, CHARACTER_DEVICE=2
      mem.setUint8(statPtr, fd <= 2 ? 2 : 4); // filetype
      mem.setUint16(statPtr + 2, 0, true); // fs_flags
      // rights_base: FD_READ=0x2 for stdin, FD_WRITE=0x40 for stdout/stderr
      let rights = BigInt(0);
      if (fd === 0)
        rights = BigInt(0x2); // FD_READ
      else if (fd === 1 || fd === 2) rights = BigInt(0x40); // FD_WRITE
      mem.setBigUint64(statPtr + 8, rights, true);
      mem.setBigUint64(statPtr + 16, BigInt(0), true); // rights_inheriting
      return 0;
    };
    const fd_prestat_get = (): number => 8; // EBADF — no preopened dirs
    const fd_prestat_dir_name = (): number => 8;
    const clock_time_get = (_id: number, _precision: bigint, timePtr: number): number => {
      const mem = new DataView(memory.buffer);
      const now = BigInt(Math.round(performance.now() * 1_000_000)); // nanoseconds
      mem.setBigUint64(timePtr, now, true);
      return 0;
    };
    const random_get = (bufPtr: number, bufLen: number): number => {
      const buf = new Uint8Array(memory.buffer, bufPtr, bufLen);
      crypto.getRandomValues(buf);
      return 0;
    };

    type WasiImportFn = (...args: never[]) => unknown;

    const wasiImports: Record<string, WasiImportFn> = {
      fd_write,
      fd_read,
      fd_close,
      fd_seek,
      fd_fdstat_get,
      fd_prestat_get,
      fd_prestat_dir_name,
      proc_exit,
      environ_sizes_get,
      environ_get,
      args_sizes_get,
      args_get,
      clock_time_get,
      random_get,
      // Stubs for less common WASI calls
      path_open: () => 44, // ENOENT
      path_filestat_get: () => 44,
      path_create_directory: () => 63, // ENOSYS
      path_remove_directory: () => 63,
      path_unlink_file: () => 63,
      path_rename: () => 63,
      path_readlink: () => 63,
      path_symlink: () => 63,
      fd_advise: () => 0,
      fd_allocate: () => 63,
      fd_datasync: () => 0,
      fd_sync: () => 0,
      fd_tell: () => 63,
      fd_readdir: () => 63,
      fd_renumber: () => 63,
      fd_pwrite: () => 63,
      fd_pread: () => 63,
      // Minimal poll_oneoff implementation for stdin-driven interactive apps.
      // We report subscriptions as ready and let fd_read_async actually block
      // on input when needed.
      poll_oneoff: (inPtr: number, outPtr: number, nsubscriptions: number, neventsPtr: number): number => {
        const mem = new DataView(memory.buffer);
        const SUB_SIZE = 48;
        const EVT_SIZE = 32;
        let nevents = 0;
        for (let i = 0; i < nsubscriptions; i++) {
          const subPtr = inPtr + i * SUB_SIZE;
          const evPtr = outPtr + nevents * EVT_SIZE;
          const userdata = mem.getBigUint64(subPtr, true);
          const eventType = mem.getUint8(subPtr + 8);

          // __wasi_event_t
          mem.setBigUint64(evPtr, userdata, true); // userdata
          mem.setUint16(evPtr + 8, 0, true); // error = ESUCCESS
          mem.setUint8(evPtr + 10, eventType); // type
          // __wasi_event_fd_readwrite_t (union payload)
          mem.setBigUint64(evPtr + 16, BigInt(1), true); // nbytes (non-zero readiness)
          mem.setUint16(evPtr + 24, 0, true); // flags

          nevents++;
        }
        mem.setUint32(neventsPtr, nevents, true);
        return 0;
      },
      sched_yield: () => 0,
      sock_accept: () => 63,
      sock_recv: () => 63,
      sock_send: () => 63,
      sock_shutdown: () => 63,
    };

    // 3. Compile and instantiate

    let memory: WebAssembly.Memory = undefined as unknown as WebAssembly.Memory;

    try {
      // Cleanly trim any trailing non-WASM padding/garbage bytes beyond the last valid section
      let validEnd = wasmBytes.byteLength;
      if (
        wasmBytes.byteLength >= 8 &&
        wasmBytes[0] === 0x00 &&
        wasmBytes[1] === 0x61 &&
        wasmBytes[2] === 0x73 &&
        wasmBytes[3] === 0x6d
      ) {
        let offset = 8;
        let lastSectionEnd = 8;
        while (offset < wasmBytes.byteLength) {
          const secId = wasmBytes[offset];
          if (secId > 13) break; // Valid WASM section IDs are 0..13
          let shift = 0, size = 0, bytesRead = 0;
          while (offset + 1 + bytesRead < wasmBytes.byteLength) {
            const b = wasmBytes[offset + 1 + bytesRead];
            bytesRead++;
            size |= (b & 0x7f) << shift;
            if ((b & 0x80) === 0) break;
            shift += 7;
          }
          const contentStart = offset + 1 + bytesRead;
          if (contentStart + size > wasmBytes.byteLength) break;
          offset = contentStart + size;
          lastSectionEnd = offset;
        }
        if (lastSectionEnd < wasmBytes.byteLength) {
          console.warn(
            `${LOG_PREFIX}   Trimming ${wasmBytes.byteLength - lastSectionEnd} trailing bytes after WASM offset ${lastSectionEnd}`,
          );
          validEnd = lastSectionEnd;
        }
      }

      console.log(`${LOG_PREFIX}   Compiling WASM module (total size: ${validEnd}B)...`);
      const tCompile = performance.now();
      const wasmBuffer = new ArrayBuffer(validEnd);
      new Uint8Array(wasmBuffer).set(wasmBytes.subarray(0, validEnd));
      const wasmModule = await WebAssembly.compile(wasmBuffer);

      console.log(`${LOG_PREFIX}   WASM compiled in ${elapsed(tCompile)}`);

      // Inspect required imports to build the import object dynamically
      const importDescs = WebAssembly.Module.imports(wasmModule);
      console.log(`${LOG_PREFIX}   WASM imports: [${importDescs.map((i) => `${i.module}.${i.name}(${i.kind})`).join(', ')}]`);
      const importObject: Record<string, Record<string, WebAssembly.ImportValue>> = {};

      for (const imp of importDescs) {
        if (!importObject[imp.module]) {
          importObject[imp.module] = {};
        }
        if (imp.module === 'wasi_snapshot_preview1' || imp.module === 'wasi_unstable') {
          const fn: WasiImportFn =
            wasiImports[imp.name] ??
            (() => {
              console.warn(`${LOG_PREFIX}   WASI stub called: ${imp.module}.${imp.name}`);
              return 0;
            });
          importObject[imp.module][imp.name] = fn;
        } else if (imp.kind === 'memory') {
          const mem = new WebAssembly.Memory({ initial: 256, maximum: 16384 });
          importObject[imp.module][imp.name] = mem;
          memory = mem;
        } else if (imp.kind === 'table') {
          importObject[imp.module][imp.name] = new WebAssembly.Table({
            initial: 0,
            element: 'anyfunc',
          });
        } else {
          // Stub unknown import (Emscripten env functions, etc.)
          importObject[imp.module][imp.name] = () => {
            // Only warn once per import to avoid log spam
            console.warn(`${LOG_PREFIX}   Unknown import stub: ${imp.module}.${imp.name}`);
            return 0;
          };
        }
      }

      console.log(`${LOG_PREFIX}   Instantiating WASM...`);
      const tInst = performance.now();
      const instance = await WebAssembly.instantiate(wasmModule, importObject);
      console.log(`${LOG_PREFIX}   WASM instantiated in ${elapsed(tInst)}`);

      // Get the memory export (most WASM modules export their memory)
      if (!memory && instance.exports.memory) {
        memory = instance.exports.memory as WebAssembly.Memory;
      }

      // Log exports and imports for debugging
      const exportNames = Object.keys(instance.exports);
      console.log(`${LOG_PREFIX}   WASM exports: [${exportNames.join(', ')}]`);

      // 4. Call _start (WASI entry point) or main
      const startFn = instance.exports._start as (() => unknown) | undefined;
      const mainFn = instance.exports.main as ((argc: number, argv: number) => number) | undefined;
      const initFn = instance.exports.__wasm_call_ctors as (() => void) | undefined;

      // Call global constructors if present
      if (initFn) {
        try {
          initFn();
        } catch {
          /* ok */
        }
      }

      // The entry point may return a Promise when Asyncify is used
      // (e.g. fd_read blocking on stdin)
      if (startFn) {
        console.log(`${LOG_PREFIX}   Calling _start()...`);
        try {
          const result = startFn();
          if (result && typeof (result as Promise<unknown>).then === 'function') {
            await (result as Promise<unknown>);
          }
        } catch (e) {
          if (e instanceof WasiExit) {
            exitCode = e.code;
          } else {
            throw e;
          }
        }
      } else if (mainFn) {
        console.log(`${LOG_PREFIX}   Calling main()...`);
        try {
          const result = mainFn(0, 0) as unknown;
          if (result && typeof (result as Promise<unknown>).then === 'function') {
            await (result as Promise<unknown>);
            exitCode = 0;
          } else {
            exitCode = Number(result ?? 0);
          }
        } catch (e) {
          if (e instanceof WasiExit) {
            exitCode = e.code;
          } else {
            throw e;
          }
        }
      } else {
        console.warn(`${LOG_PREFIX}   No _start or main export found`);
        exitCode = 0;
      }

      console.log(`${LOG_PREFIX} ===== WASI COMPLETE: exitCode=${exitCode}, total=${elapsed(tTotal)} =====`);
    } catch (e) {
      if (e instanceof WasiExit) {
        exitCode = e.code;
        console.log(`${LOG_PREFIX} ===== WASI COMPLETE (proc_exit): exitCode=${exitCode}, total=${elapsed(tTotal)} =====`);
      } else {
        const msg = e instanceof Error ? e.message : String(e);
        console.error(`${LOG_PREFIX} ❌ WASI run error after ${elapsed(tTotal)}:`, msg);
        if (e instanceof Error && e.stack) {
          console.error(`${LOG_PREFIX}   Stack: ${e.stack}`);
        }
        stderrChunks.push(msg);
        options.onStderr?.(msg);
        return {
          exitCode: 1,
          stdout: stdoutChunks.join(''),
          stderr: stderrChunks.join(''),
        };
      }
    }

    return {
      exitCode,
      stdout: stdoutChunks.join(''),
      stderr: stderrChunks.join(''),
    };
  }

  /* ---------------------------------------------------------------- */
  /*  JavaScript evaluation (node emulation)                           */
  /* ---------------------------------------------------------------- */

  /**
   * Run an Emscripten-compiled JavaScript file in the browser.
   *
   * After emcc produces main.js + main.wasm, this evaluator:
   *   1. Reads main.js from the kernel VFS.
   *   2. Reads the companion .wasm file.
   *   3. Sets up a Module object with wasmBinary + print/printErr hooks.
   *   4. Evaluates the JS via `new Function()`.
   */
  private async runJavaScript(argv: string[], options: RunOptions = {}): Promise<ToolResult> {
    const tTotal = performance.now();
    const scriptPath = argv.length > 1 ? argv[1] : argv[0];
    console.log(`${LOG_PREFIX} ===== NODE (JS eval): ${scriptPath} =====`);

    // Special case: compiler.mjs is a heavy Node.js script that's used for
    // post-processing after clang compilation. The actual compilation is done
    // by clang, so we can safely skip the node execution and return minimal output.
    if (scriptPath.includes('compiler.mjs')) {
      console.log(`${LOG_PREFIX}   [SKIPPED] compiler.mjs (post-processing, already done by clang)`);
      // Return minimal valid JSON matching the format that compiler.mjs --symbols-only
      // produces (see src/jsifier.mjs runJSify symbolsOnly branch).
      // Python's get_js_sym_info() -> json.loads(output) expects:
      //   deps: dict[str, list[str]]  — JS symbol → native dependencies
      //   asyncFuncs: list[str]       — async JS library functions
      //   extraLibraryFuncs: list[str] — extra library function names
      const output = JSON.stringify({
        deps: {},
        asyncFuncs: [],
        extraLibraryFuncs: [],
      });
      options.onStdout?.(output);
      return { exitCode: 0, stdout: output, stderr: '' };
    }

    // Read the JS file from the kernel VFS
    console.log(`${LOG_PREFIX}   Reading JS file from VFS: ${scriptPath}`);
    const jsBytes = await this.vfs.fetchFile(scriptPath);
    if (!jsBytes) {
      const msg = `File not found: ${scriptPath}`;
      console.error(`${LOG_PREFIX} ❌ JS file not found: ${scriptPath}`);
      options.onStderr?.(msg);
      return { exitCode: 1, stdout: '', stderr: msg };
    }
    console.log(`${LOG_PREFIX}   JS file read: ${fmtSize(jsBytes.length)}`);

    const jsCode = new TextDecoder().decode(jsBytes);

    // Read companion .wasm (e.g., main.wasm next to main.js)
    const wasmPath = scriptPath.replace(/\.js$/, '.wasm');
    const wasmBytes = await this.vfs.fetchFile(wasmPath);
    console.log(`${LOG_PREFIX}   Companion WASM: ${wasmPath} — ${wasmBytes ? fmtSize(wasmBytes.length) : 'NOT FOUND'}`);

    const stdoutChunks: string[] = [];
    const stderrChunks: string[] = [];

    const printFn = (text: string) => {
      stdoutChunks.push(text);
      options.onStdout?.(text);
    };
    const printErrFn = (text: string) => {
      stderrChunks.push(text);
      options.onStderr?.(text);
    };

    try {
      const moduleConfig: Record<string, unknown> = {
        print: printFn,
        printErr: printErrFn,
        noExitRuntime: false,
        arguments: argv.slice(2),
      };

      if (wasmBytes) {
        moduleConfig['wasmBinary'] = wasmBytes.buffer.slice(wasmBytes.byteOffset, wasmBytes.byteOffset + wasmBytes.byteLength);
      }

      // Create a simple loader for node: modules
      // This allows Node.js scripts to run in a browser-like environment
      const createNodeModuleLoader = () => {
        // Return stub objects for common Node APIs
        return {
          assert: { ok: () => { } },
          util: {
            parseArgs: () => ({ values: {}, positionals: [] }),
            debuglog: () => () => { },
          },
          fs: {
            readFileSync: () => '',
            writeFileSync: () => { },
            promises: { readFile: () => '' },
          },
          path: { resolve: (...args: string[]) => args[args.length - 1] ?? '' },
          process: { argv: [], exit: () => { }, cwd: () => '/' },
          module: { exports: {} },
          require: (id: string) => createNodeModuleLoader()[id as keyof ReturnType<typeof createNodeModuleLoader>] || {},
        };
      };

      // Wrap the code to handle both import and require
      // Convert import statements to require-like calls
      let processedCode = jsCode;

      // This regex matches import statements - handle single and multiline
      processedCode = processedCode.replace(/import\s+(?:(?:\{[^}]*\})|(?:[a-zA-Z_$][a-zA-Z0-9_$]*))\s+from\s+['"]node:([^'"]+)['"]/g, (match) => {
        // Just comment out the import - we'll provide globals
        return '/* ' + match + ' */';
      });

      // Handle multiline imports by converting them to no-ops
      processedCode = processedCode.replace(/import\s+\{[\s\S]*?\}\s+from\s+['"]node:([^'"]+)['"]/g, '/* multiline import removed */');

      const nodeModules = createNodeModuleLoader();
      const globalStubs = Object.entries(nodeModules)
        .map(([name, obj]) => `const ${name} = ${JSON.stringify(obj, (_, v) => (typeof v === 'function' ? '[Function]' : v))};`)
        .join('\n');

      const wrappedCode = `
        return (async function(Module) {
          ${globalStubs}
          ${processedCode}
          return Module;
        })(Module);
      `;
      console.log(`${LOG_PREFIX}   Evaluating Emscripten JS module...`);
      const tEval = performance.now();
      const fn = new Function('Module', wrappedCode);
      await fn(moduleConfig);
      console.log(`${LOG_PREFIX}   JS module evaluation completed in ${elapsed(tEval)}`);

      const exitCode = typeof moduleConfig['EXITSTATUS'] === 'number' ? (moduleConfig['EXITSTATUS'] as number) : 0;

      console.log(`${LOG_PREFIX} ===== NODE COMPLETE: exitCode=${exitCode}, total=${elapsed(tTotal)} =====`);

      return {
        exitCode,
        stdout: stdoutChunks.join('\n'),
        stderr: stderrChunks.join('\n'),
      };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      console.error(`${LOG_PREFIX} ❌ JS eval error after ${elapsed(tTotal)}:`, msg);
      stderrChunks.push(msg);
      options.onStderr?.(msg);
      return {
        exitCode: 1,
        stdout: stdoutChunks.join('\n'),
        stderr: stderrChunks.join('\n'),
      };
    }
  }

  /* ---------------------------------------------------------------- */
  /*  Internal helpers                                                 */
  /* ---------------------------------------------------------------- */

  private resolveToolDescriptor(name: string): ToolDescriptor {
    // First, try direct registry lookup
    const descriptor = TOOL_REGISTRY[name];
    if (descriptor) {
      console.log(`${LOG_PREFIX}   resolveToolDescriptor: "${name}" → registry match`);
      return descriptor;
    }

    // If name is a path (e.g., /usr/bin/clang), extract the basename
    if (name.includes('/')) {
      const basename = name.split('/').pop() || name;
      const baseDescriptor = TOOL_REGISTRY[basename];
      if (baseDescriptor) {
        console.log(`${LOG_PREFIX}   resolveToolDescriptor: "${name}" → basename "${basename}" registry match`);
        return baseDescriptor;
      }
      // Check for full path patterns
      if (basename === 'python3' || basename === 'python' || basename === `python${this.versions.pythonMajorMinor}`) {
        return { modulePath: '/usr/lib/python.wasm' };
      }
      if (basename === 'emcc' || basename === 'em++') {
        // Fall through to the emcc/em++ handler below
      }
    }

    if (name === 'python3' || name === 'python') {
      return { modulePath: '/usr/lib/python.wasm' };
    }

    // Normalize: use basename for emcc/em++ matching (handle /usr/lib/emscripten/emcc paths)
    const toolBasename = name.includes('/') ? name.split('/').pop() || name : name;
    if (toolBasename === 'emcc' || toolBasename === 'em++') {
      return { modulePath: '/usr/lib/python.wasm' };
    }

    throw new Error(`Unknown tool: ${name}`);
  }

  private async loadModuleFactory(wasmPath: string): Promise<ModuleFactory> {
    console.log(`${LOG_PREFIX}   loadModuleFactory: ${wasmPath}`);

    // Preload the bundle containing this WASM so getUrl returns blob URLs
    const bundleName = this.vfs.getBundleForFile(wasmPath);
    if (bundleName) {
      console.log(`${LOG_PREFIX}   loadModuleFactory: preloading bundle "${bundleName}" for ${wasmPath}`);
      await this.vfs.preloadBundle(bundleName);
    }

    const isPython = wasmPath.includes('python');

    return loadModuleFactory(wasmPath, {
      getGlueUrl: (path) => {
        // For bundled files: get the .mjs blob URL directly
        const mjsPath = path.replace(/\.wasm$/, '.mjs');
        const mjsUrl = this.vfs.getUrl(mjsPath);
        if (mjsUrl && mjsUrl.startsWith('blob:')) return mjsUrl;
        // For non-bundled files: derive .mjs URL from .wasm URL
        const baseUrl = this.vfs.getUrl(path);
        if (baseUrl) {
          return baseUrl.replace(/\.(br|gz)$/, '').replace(/\.wasm$/, '.mjs');
        }
        // Fallback: add /cdn prefix for files not in manifest
        return `/cdn${path.replace('.wasm', '.mjs')}`;
      },
      // For python.mjs: patch ___syscall_openat at runtime to intercept
      // the /tmp/__dispatch_subprocess__ magic path for subprocess dispatch.
      ...(isPython
        ? {
          patchGlueContent: (source: string): string => {
            if (source.includes('__dispatch_subprocess__')) {
              console.log(`${LOG_PREFIX}   patchGlueContent: python.mjs already patched`);
              return source;
            }
            // Emscripten openat body (modern):
            //   path=SYSCALLS.getStr(path);path=SYSCALLS.calculateAt(dirfd,path);
            //   var mode=varargs?syscallGetVarargI():0;
            //   if(flags&64){mode&=~SYSCALLS.currentUmask}
            //   return FS.open(path,flags,mode).fd
            // Inject subprocess dispatch right after path resolution.
            const needle = 'path=SYSCALLS.getStr(path);path=SYSCALLS.calculateAt(dirfd,path);' + 'var mode=varargs?syscallGetVarargI():0;';
            const replacement =
              needle +
              'if(path==="/tmp/__dispatch_subprocess__"&&Module["subprocessDispatch"]){' +
              'return Asyncify.handleAsync(function(){' +
              'return Module["subprocessDispatch"]().then(function(){' +
              'return FS.open(path,flags,mode).fd})})' +
              '}';
            if (!source.includes(needle)) {
              console.warn(`${LOG_PREFIX}   patchGlueContent: needle not found in python.mjs, skipping patch`);
              return source;
            }
            const patched = source.replace(needle, replacement);
            console.log(`${LOG_PREFIX}   patchGlueContent: patched ___syscall_openat for subprocess dispatch`);
            return patched;
          },
        }
        : {}),
    }) as Promise<ModuleFactory>;
  }

  private parseCommand(cmd: string): string[] {
    const tokens: string[] = [];
    let current = '';
    let inQuote: string | null = null;
    for (const ch of cmd) {
      if (inQuote) {
        if (ch === inQuote) {
          inQuote = null;
        } else {
          current += ch;
        }
      } else if (ch === '"' || ch === "'") {
        inQuote = ch;
      } else if (ch === ' ' || ch === '\t') {
        if (current) {
          tokens.push(current);
          current = '';
        }
      } else {
        current += ch;
      }
    }
    if (current) tokens.push(current);
    return tokens;
  }
}
