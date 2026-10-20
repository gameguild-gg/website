// Headless build presets for the browser API.
//
// These encapsulate the argv arrays that the IDE has historically inlined
// for direct (driver-less) clang -cc1 + wasm-ld + wasi-run pipelines.
// Lifting them into the `@emception/browser` package lets minimal demos
// (and any future consumer) drive a compile+run cycle without copying
// the dozens of cc1 flags by hand.
//
// This is the *low-level* preset surface: it returns argv ready to feed
// to `EmceptionAPI.run`. The richer `compileAndRun` orchestration in
// `emception` (events, workspace seeding, cancellation) is a
// separate, higher-level layer that lands later in the roadmap.

import { ToolchainPreset, type EmceptionAPI } from 'emception';
import type { ToolResult } from './tool-runner.js';
import type { WorkerClient } from './worker-client.js';

export interface CompilePaths {
    /** Source file path inside the VFS (also appears in error messages). */
    sourcePath: string;
    /** Object file path produced by the compile step. */
    objectPath: string;
    /** Final wasm artifact path produced by the link step. */
    wasmPath: string;
}

const DEFAULT_PATHS: CompilePaths = {
    sourcePath: 'main.cpp',
    objectPath: 'main.o',
    wasmPath: 'main.wasm',
};

/**
 * Common clang -cc1 frontend flags (no isystem entries — those are
 * appended per-language so the C++ stdlib search path can be inserted
 * BEFORE /usr/include, which is required by libc++'s `<cctype>`,
 * `<cwchar>`, `<cwctype>` etc.
 */
const CC1_FRONTEND: readonly string[] = [
    '-cc1',
    '-triple',
    'wasm32-unknown-emscripten',
    '-emit-obj',
    '-O1',
    '-disable-free',
    '-clear-ast-before-backend',
    '-disable-llvm-verifier',
    '-discard-value-names',
    '-mrelocation-model',
    'static',
    '-mframe-pointer=none',
    '-ffp-contract=on',
    '-fno-rounding-math',
    '-mconstructor-aliases',
    '-target-cpu',
    'generic',
    '-fvisibility=hidden',
];

/** Trailing cc1 flags shared by all presets. */
const CC1_TAIL: readonly string[] = ['-fdeprecated-macro', '-ferror-limit', '19', '-fgnuc-version=4.2.1'];

/** C-language include search path. */
const CC1_C_INCLUDES: readonly string[] = [
    '-resource-dir',
    '/usr/lib/clang/23',
    '-internal-isystem',
    '/usr/lib/clang/23/include',
    '-internal-isystem',
    '/usr/include',
];

/**
 * C++ include search path — libc++ headers must come BEFORE /usr/include
 * so that `<cctype>` resolves to libc++'s shim that re-exports `<ctype.h>`
 * via `__cxx_libc++` rather than directly grabbing the C `<ctype.h>`.
 */
const CC1_CPP_INCLUDES: readonly string[] = [
    '-internal-isystem',
    '/usr/include/c++/v1',
    '-internal-isystem',
    '/usr/include/compat',
    '-internal-isystem',
    '/usr/lib/clang/23/include',
    '-resource-dir',
    '/usr/lib/clang/23',
    '-internal-isystem',
    '/usr/include',
];

const CC1_CPP_EXC: readonly string[] = ['-fcxx-exceptions', '-fexceptions'];

const CC1_STD_C: readonly string[] = ['-std=c2y'];
const CC1_STD_CPP: readonly string[] = ['-std=c++2c'];

/** Standard wasm-ld link line for emscripten-style libc. */
const WASM_LD_BASE: readonly string[] = [
    '-L/usr/lib/emscripten/cache-lib/wasm32-emscripten',
    '--entry=main',
    '--import-undefined',
    '--allow-undefined',
    '--export-table',
    '--table-base=1',
    '--export=__wasm_call_ctors',
];

const WASM_LD_CPP_LIBS: readonly string[] = [
    '-lc++-noexcept',
    '-lc++abi-noexcept',
    '-lc',
    '-ldlmalloc',
    '-lcompiler_rt',
    '-lsockets',
];

const WASM_LD_C_LIBS: readonly string[] = ['-lc', '-ldlmalloc', '-lcompiler_rt', '-lsockets'];

/**
 * Shared wasm-ld base flags for all canvas presets (SDL3, raylib, Allegro).
 * Uses the emscripten sysroot (not the WASI cache-lib) and includes the CRT
 * startup object. Each canvas preset appends its own lib-specific flags.
 */
const WASM_LD_CANVAS_BASE: readonly string[] = [
    '-L/usr/lib/emscripten/cache/sysroot/lib/wasm32-emscripten',
    '-L/usr/lib/emscripten/src/lib',
    '/usr/lib/emscripten/cache/sysroot/lib/wasm32-emscripten/crt1.o',
    '--no-entry',
    '--import-undefined',
    '--allow-undefined',
    '--export-table',
    '--table-base=1',
    '-lGL-getprocaddr',
    '-lal',
    '-lstubs',
    '-lc',
    '-ldlmalloc',
    '-lcompiler_rt',
    '-lc++-noexcept',
    '-lc++abi-noexcept',
    '-lsockets',
];

/**
 * SDL3 link line. Adds libSDL3.a, SDL_App* callback exports, html5 (needed
 * for emscripten_set_main_loop JS glue), and a small stack (SDL3 manages its
 * own memory growth via ALLOW_MEMORY_GROWTH).
 */
const WASM_LD_SDL_FLAGS: readonly string[] = [
    ...WASM_LD_CANVAS_BASE,
    '/usr/lib/libimgui.a',
    '/usr/lib/emscripten/cache/sysroot/lib/wasm32-emscripten/libSDL3.a',
    '--export-if-defined=SDL_AppInit',
    '--export-if-defined=SDL_AppIterate',
    '--export-if-defined=SDL_AppEvent',
    '--export-if-defined=SDL_AppQuit',
    '-z',
    'stack-size=65536',
    '-lhtml5',
];

/** Extra cc1 -internal-isystem entries needed to find SDL3 and Dear ImGui headers. */
const CC1_SDL_EXTRA: readonly string[] = [
    '-internal-isystem',
    '/usr/include/fakesdl',
    '-internal-isystem',
    '/usr/include/SDL3',
    '-internal-isystem',
    '/usr/include/imgui',
];

/** Extra cc1 -internal-isystem entries needed to find raylib headers. */
const CC1_RAYLIB_EXTRA: readonly string[] = ['-internal-isystem', '/usr/include/raylib'];

/** Extra cc1 -internal-isystem entries needed to find Allegro 5 headers. */
const CC1_ALLEGRO_EXTRA: readonly string[] = ['-internal-isystem', '/usr/include/allegro5'];

/**
 * Raylib link line. Uses prebuilt libraylib.a with raylib-runtime.mjs.
 *
 * Key: do NOT link -lhtml5. libhtml5.a provides a WASM emscripten_set_main_loop
 * that bypasses raylib-runtime.mjs's MainLoop.func setup, leaving MainLoop.func=null
 * so the RAF callback crashes. By omitting -lhtml5, emscripten_set_main_loop and
 * other HTML5 API functions become WASM imports resolved by raylib-runtime.mjs which
 * correctly calls setMainLoop() → MainLoop.func = iterFunc → RAF works.
 *
 * Exports `main` so the runtime calls it via callMain(). emscripten_set_main_loop
 * (with simulate_infinite_loop=1) throws 'unwind' which callMain() catches, leaving
 * the RAF-based draw loop active.
 */
const WASM_LD_RAYLIB_FLAGS: readonly string[] = [
    ...WASM_LD_CANVAS_BASE,
    '/usr/lib/libraylib.a',
    '--export=main',
    '--export=malloc',
    '--export=free',
    '--export=__wasm_call_ctors',
    '-z',
    'stack-size=2097152',
    // -lhtml5 intentionally omitted — see comment above.
];

/**
 * Allegro 5 link line. Mirrors the raylib pattern (clang+wasm-ld two-step,
 * runtime mjs supplies emscripten_set_main_loop + WebGL). Unlike raylib, we
 * DO link `-lhtml5` here because Allegro's SDL2 backend pulls in helper C
 * functions like `emscripten_compute_dom_pk_code` that live in `libhtml5.a`
 * (not in the JS glue). `emscripten_set_main_loop` is a JS-library function
 * and is NOT defined in `libhtml5.a`, so RAF is still routed through the
 * allegro-runtime.mjs interception path.
 *
 * Backend: Allegro 5 upstream removed the native HTML5 backend; current
 * releases require `-DALLEGRO_SDL=on`. We link the emsdk SDL2 port
 * (libSDL2.a, copied into the sysroot by build-allegro.ts) so Allegro's
 * SDL platform layer can resolve SDL_* symbols.
 *
 * Link order: liballegro_main first (so user int main() works portably),
 * then addons (which depend on core), then the core library, then libSDL2,
 * then the emscripten libc/libgl runtime libs.
 */
const WASM_LD_ALLEGRO_FLAGS: readonly string[] = [
    ...WASM_LD_CANVAS_BASE,
    '/usr/lib/liballegro_main.a',
    '/usr/lib/liballegro_image.a',
    '/usr/lib/liballegro_primitives.a',
    '/usr/lib/liballegro_font.a',
    '/usr/lib/liballegro_audio.a',
    '/usr/lib/liballegro_acodec.a',
    '/usr/lib/liballegro_color.a',
    '/usr/lib/liballegro.a',
    '/usr/lib/libSDL2.a',
    '--export=main',
    '--export=malloc',
    '--export=free',
    '--export=__wasm_call_ctors',
    '-z',
    'stack-size=2097152',
    '-lhtml5',
];

/** Full preset for a native (clang + wasm-ld) target. */
export interface NativePreset {
    readonly toolchain: Exclude<ToolchainPreset, ToolchainPreset.CMake | ToolchainPreset.Python>;
    readonly bundlesToPreload: string[];
    readonly defaultTools: string[];
    readonly compiler?: 'clang' | 'clang++' | 'emcc' | 'em++';
    readonly flags?: string[];
    readonly ldflags?: string[];
    readonly defines?: Record<string, string | true>;
    readonly includePaths?: string[];
    readonly libPaths?: string[];
    readonly libs?: string[];
    readonly sources?: string[];
    readonly output?: string;
    readonly env?: Record<string, string>;
    readonly compileTool: string;
    readonly linkTool: string;
    compileArgv(paths: Pick<CompilePaths, 'sourcePath' | 'objectPath'>): string[];
    linkArgv(paths: Pick<CompilePaths, 'objectPath' | 'wasmPath'>): string[];
}

/** Full preset for a Python script target (no compile/link step). */
export interface PythonPreset {
    readonly toolchain: ToolchainPreset.Python;
    readonly bundlesToPreload: string[];
    readonly defaultTools: string[];
    readonly env?: Record<string, string>;
}

/** Full preset for a CMake project (no direct clang invocation). */
export interface CMakePreset {
    readonly toolchain: ToolchainPreset.CMake;
    readonly bundlesToPreload: string[];
    readonly defaultTools: string[];
    readonly env?: Record<string, string>;
}

/** Unified preset — discriminated by `toolchain`. */
export type Preset = NativePreset | PythonPreset | CMakePreset;

export const TOOLCHAIN_PRESETS: Record<ToolchainPreset, Preset> = {
    [ToolchainPreset.C]: {
        toolchain: ToolchainPreset.C,
        bundlesToPreload: ['llvm', 'libcurl-lite'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        flags: ['-O1'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_C_INCLUDES,
            ...CC1_STD_C,
            ...CC1_TAIL,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_BASE, ...WASM_LD_C_LIBS],
    },
    [ToolchainPreset.CPP]: {
        toolchain: ToolchainPreset.CPP,
        bundlesToPreload: ['llvm', 'libcurl-lite'],
        defaultTools: ['clang++', 'wasm-ld'],
        compiler: 'clang++',
        flags: ['-O1'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_CPP_INCLUDES,
            ...CC1_STD_CPP,
            ...CC1_TAIL,
            ...CC1_CPP_EXC,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c++',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_BASE, ...WASM_LD_CPP_LIBS],
    },
    [ToolchainPreset.SDL_CPP]: {
        toolchain: ToolchainPreset.SDL_CPP,
        bundlesToPreload: ['llvm', 'sdl3', 'imgui'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: ['SDL3'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_CPP_INCLUDES,
            ...CC1_SDL_EXTRA,
            ...CC1_STD_CPP,
            ...CC1_TAIL,
            ...CC1_CPP_EXC,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c++',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_SDL_FLAGS],
    },
    [ToolchainPreset.SDL_C]: {
        toolchain: ToolchainPreset.SDL_C,
        bundlesToPreload: ['llvm', 'sdl3', 'imgui'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: ['SDL3'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_C_INCLUDES,
            ...CC1_SDL_EXTRA,
            ...CC1_STD_C,
            ...CC1_TAIL,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_SDL_FLAGS],
    },
    [ToolchainPreset.Raylib_CPP]: {
        toolchain: ToolchainPreset.Raylib_CPP,
        bundlesToPreload: ['llvm', 'raylib'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: ['raylib', 'raygui', 'physac', 'rlights'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_CPP_INCLUDES,
            ...CC1_RAYLIB_EXTRA,
            ...CC1_STD_CPP,
            ...CC1_TAIL,
            ...CC1_CPP_EXC,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c++',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_RAYLIB_FLAGS],
    },
    [ToolchainPreset.Raylib_C]: {
        toolchain: ToolchainPreset.Raylib_C,
        bundlesToPreload: ['llvm', 'raylib'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: ['raylib', 'raygui', 'physac', 'rlights'],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_C_INCLUDES,
            ...CC1_RAYLIB_EXTRA,
            ...CC1_STD_C,
            ...CC1_TAIL,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_RAYLIB_FLAGS],
    },
    [ToolchainPreset.Allegro_CPP]: {
        toolchain: ToolchainPreset.Allegro_CPP,
        bundlesToPreload: ['llvm', 'allegro'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: [
            'allegro',
            'allegro_image',
            'allegro_primitives',
            'allegro_font',
            'allegro_ttf',
            'allegro_audio',
            'allegro_acodec',
            'allegro_color',
            'allegro_main',
        ],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_CPP_INCLUDES,
            ...CC1_ALLEGRO_EXTRA,
            ...CC1_STD_CPP,
            ...CC1_TAIL,
            ...CC1_CPP_EXC,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c++',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_ALLEGRO_FLAGS],
    },
    [ToolchainPreset.Allegro_C]: {
        toolchain: ToolchainPreset.Allegro_C,
        bundlesToPreload: ['llvm', 'allegro'],
        defaultTools: ['clang', 'wasm-ld'],
        compiler: 'clang',
        libs: [
            'allegro',
            'allegro_image',
            'allegro_primitives',
            'allegro_font',
            'allegro_ttf',
            'allegro_audio',
            'allegro_acodec',
            'allegro_color',
            'allegro_main',
        ],
        compileTool: 'clang',
        linkTool: 'wasm-ld',
        compileArgv: ({ sourcePath, objectPath }) => [
            'clang',
            ...CC1_FRONTEND,
            ...CC1_C_INCLUDES,
            ...CC1_ALLEGRO_EXTRA,
            ...CC1_STD_C,
            ...CC1_TAIL,
            '-main-file-name',
            basename(sourcePath),
            '-o',
            objectPath,
            '-x',
            'c',
            sourcePath,
        ],
        linkArgv: ({ objectPath, wasmPath }) => ['wasm-ld', objectPath, '-o', wasmPath, ...WASM_LD_ALLEGRO_FLAGS],
    },
    [ToolchainPreset.Python]: {
        toolchain: ToolchainPreset.Python,
        bundlesToPreload: ['cpython'],
        defaultTools: ['python3'],
    },
    [ToolchainPreset.CMake]: {
        toolchain: ToolchainPreset.CMake,
      bundlesToPreload: ['llvm', 'cmake'],
      defaultTools: ['cmake', 'ninja'],
    },
};

function basename(path: string): string {
    const i = path.lastIndexOf('/');
    return i < 0 ? path : path.slice(i + 1);
}

export type CompilePhase = 'write' | 'compile' | 'link' | 'run';

export interface CompileAndRunOptions {
    toolchain: ToolchainPreset;
    /** Source code to compile. Written to `paths.sourcePath` before compiling. */
    source: string;
    /** Optional working dir for each tool invocation. Defaults to dirname(sourcePath). */
    cwd?: string;
    /** Override the default `/home/user/main.{cpp,o,wasm}` paths. */
    paths?: Partial<CompilePaths>;
    /** Stdin payload for the run step. Sent byte-by-byte, EOF after exhaustion. */
    stdin?: string;
    /** Forwarded to each `api.run` call. */
    onStdout?: (text: string) => void;
    onStderr?: (text: string) => void;
    /** Lifecycle callback fired before each phase begins. */
    onPhase?: (phase: CompilePhase) => void;
}

export interface CompileAndRunResult {
    /** Phase that produced the final exit code. */
    finalPhase: CompilePhase;
    /** Exit code of the last tool that ran. Non-zero if compile or link failed. */
    exitCode: number;
    /** Per-phase results, populated as each phase completes. */
    compile?: ToolResult;
    link?: ToolResult;
    run?: ToolResult;
}

/**
 * End-to-end "edit → compile → link → run" cycle on top of the headless
 * `WorkerClient`. Stops early on compile or link failure.
 */
export async function compileAndRun(client: WorkerClient | EmceptionAPI, opts: CompileAndRunOptions): Promise<CompileAndRunResult> {
    const preset = TOOLCHAIN_PRESETS[opts.toolchain] as NativePreset | undefined;
    if (!preset) {
        throw new Error(`compileAndRun: unknown toolchain '${opts.toolchain}'`);
    }

    const paths: CompilePaths = { ...DEFAULT_PATHS, ...opts.paths };
    const cwd = opts.cwd ?? dirname(paths.sourcePath);
    const runTool = async (tool: string, argv: string[], stdin?: string): Promise<ToolResult> => {
        if ('workspace' in client) {
            const stdoutDecoder = new TextDecoder();
            const stderrDecoder = new TextDecoder();
            try {
                return await client.run(tool, argv, {
                    cwd,
                    stdin,
                    stdout: opts.onStdout ? (chunk) => {
                        const text = stdoutDecoder.decode(chunk, { stream: true });
                        if (text) opts.onStdout?.(text);
                    } : undefined,
                    stderr: opts.onStderr ? (chunk) => {
                        const text = stderrDecoder.decode(chunk, { stream: true });
                        if (text) opts.onStderr?.(text);
                    } : undefined,
                });
            } finally {
                const stdoutTail = stdoutDecoder.decode();
                const stderrTail = stderrDecoder.decode();
                if (stdoutTail) opts.onStdout?.(stdoutTail);
                if (stderrTail) opts.onStderr?.(stderrTail);
            }
        }
        return client.run(tool, argv, {
            cwd,
            onStdout: opts.onStdout,
            onStderr: opts.onStderr,
            stdin: makeStdinFeeder(stdin),
        });
    };

    opts.onPhase?.('write');
    if ('workspace' in client) {
        await client.workspace.writeFile(paths.sourcePath, opts.source);
    } else {
        await client.writeFile(paths.sourcePath, new TextEncoder().encode(opts.source));
    }

    opts.onPhase?.('compile');
    const compile = await runTool(preset.compileTool, preset.compileArgv(paths));
    if (compile.exitCode !== 0) {
        return { finalPhase: 'compile', exitCode: compile.exitCode, compile };
    }

    opts.onPhase?.('link');
    const link = await runTool(preset.linkTool, preset.linkArgv(paths));
    if (link.exitCode !== 0) {
        return { finalPhase: 'link', exitCode: link.exitCode, compile, link };
    }

    opts.onPhase?.('run');
    const run = await runTool('wasi-run', ['wasi-run', paths.wasmPath], opts.stdin);
    return { finalPhase: 'run', exitCode: run.exitCode, compile, link, run };
}

function dirname(path: string): string {
    const i = path.lastIndexOf('/');
    return i <= 0 ? '.' : path.slice(0, i);
}

function makeStdinFeeder(stdin: string | undefined): (() => number | null) | undefined {
    if (stdin === undefined) return undefined;
    const enc = new TextEncoder();
    const bytes = enc.encode(stdin.endsWith('\n') ? stdin : stdin + '\n');
    let i = 0;
    return () => (i >= bytes.length ? null : bytes[i++]!);
}
