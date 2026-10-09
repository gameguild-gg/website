# Code Runners

Browser code execution using WebAssembly and progressive compilation feedback. Isolation and resource limits depend on the language runner.

## Architecture

The runner system is built on a modular architecture where each language has its own dedicated runner implementing the `CodeRunner` interface. All runners share common infrastructure for WASM loading, caching, and execution.

### Runner Selection System

For languages with multiple runner implementations, you can switch between them by modifying the `RUNNER_SELECTION` configuration in `runners/index.ts`:

```typescript
const RUNNER_SELECTION = {
  PYTHON_RUNNER: 1, // 1=Pyodide, 2=WASI
  // Add more languages here as needed
} as const
```

This allows testing and comparing different runtime implementations for the same language without changing application code.

### Core Components

**`UnifiedCodeRunner`** - Central orchestrator that manages runner instances and routes execution requests to the appropriate language runner.

**`CodeRunner` Interface** - Standard contract that all language runners must implement:
```typescript
interface CodeRunner {
  execute(code: string, stdin?: string): Promise<RunnerResult>
  executeWithFiles?(entryPoint: string, files: FileMap, stdin?: string): Promise<RunnerResult>
  interrupt(): Promise<void>
  dispose(): void
}
```

**`RunnerOptions`** - Configuration passed to all runners:
```typescript
interface RunnerOptions {
  timeout?: number              // Execution timeout in ms (default: 30000)
  memoryLimit?: number          // Memory limit in bytes (default: 64MB)
  onRequestInput?: (prompt?: string, currentOutput?: string) => Promise<string>
  onProgress?: (message: string) => void  // Progressive feedback for compilation stages
}
```

**WASM Loader** - Shared infrastructure for loading, decompressing, and caching WASM binaries:
- `loadCompressedWasm()` - Loads and decompresses `.wasm.gz` files
- `loadTarGz()` - Extracts `.tar.gz` filesystems for compilers
- IndexedDB caching for fast subsequent loads
- Automatic cache versioning and invalidation

### Execution Flow

1. **Initialization**: Runner instances are created lazily on first use and cached
2. **Progress Callbacks**: Compilation-based runners report progress via `onProgress`
3. **WASM Loading**: Binaries are loaded from cache or fetched and decompressed
4. **Execution**: Code runs in isolated WASI environment with Web Workers
5. **Result Collection**: stdout/stderr streams are captured and returned

## Implemented Languages

### ✅ JavaScript
- **Engine**: QuickJS (asyncify-enabled WASM)
- **Sandbox**: A separate QuickJS guest context; page DOM, storage, network clients and host globals are not exposed.
- **Features**: Full ES2020 support, async/await
- **Timeout**: 30s (configurable), enforced inside the interpreter and while awaiting guest promises.
- **Limits**: 64MB guest memory by default, 1MB stack and 1MB console/result output. Stop cancels pending input and the VM is disposed after failure.
- **Size**: ~368KB (gzip compressed)
- **Source**: `/langs/quickjs-asyncify.wasm.gz`, prepared from the installed package by `sync:javascript-runtime`. The IndexedDB cache key includes its package version.

The legacy source-code editor also uses this runner for programs and terminal expressions. Imports and terminal variables remain inside the guest. Prompt, alert and confirm use explicit callbacks that exchange primitive values with the UI. No page `Window` object or browser credential store is passed to student code.

### ✅ TypeScript
- **Transpiler**: esbuild (WASM)
- **Engine**: QuickJS (WASM)
- **Sandbox**: Complete isolation
- **Features**: Full TypeScript support, JSX/TSX
- **Timeout**: 30s (configurable)
- **Size**: ~3.5MB (esbuild) + ~368KB (quickjs) (gzip compressed)
- **Source**: `/wasm/esbuild.wasm.gz` + `/wasm/quickjs-asyncify.wasm.gz`

The legacy source-code editor transpiles each visible TypeScript file with the installed TypeScript compiler and reports syntax diagnostics before execution. Its imports and compiled JavaScript run through a separate instance of the isolated JavaScript executor. It does not use host `AsyncFunction` or pass the page `Window` to student code. This transpilation step does not perform full semantic type checking.

### ✅ Python
- **Engine**: Pyodide (CPython 3.12 compiled to WASM)
- **Sandbox**: Complete isolation
- **Features**: Full standard library, scientific packages (numpy, pandas via micropip)
- **Timeout**: 30s (configurable)
- **Size**: ~2.7MB WASM (compressed) + ~6MB runtime
- **Source**: `/wasm/pyodide.asm.wasm.gz` + `/pyodide/` (local CDN mirror)
- **Version**: 0.26.4

#### Alternative: Python (WASI)
- **Engine**: CPython 3.11.3 (WASI runtime)
- **Execution**: WASI runtime in Web Worker
- **Features**: Basic standard library only
- **Size**: ~9.50MB WASM + ~3.88MB stdlib (gzip compressed)
- **Source**: `/langs/python-3.11.3.wasm.gz`, `/langs/python-3.11.3.tar.gz`
- **Progress Stages**:
  1. Loading Python interpreter...
  2. Running Python code...
- **Notes**: 
  - Faster startup than Pyodide
  - No external package support
  - Full file system support
  - Switch via `RUNNER_SELECTION.PYTHON_RUNNER = 2`

### ✅ Lua
- **Engine**: Wasmoon (Lua 5.4 WASM)
- **Sandbox**: Complete isolation
- **Features**: Full Lua 5.4 standard library
- **Timeout**: 30s (configurable)
- **Size**: ~400KB (gzip compressed)
- **Source**: Via `wasmoon` npm package

### ✅ C
- **Compiler**: Clang 8.0.1 (WASM)
- **Linker**: wasm-ld (LLVM linker)
- **Execution**: WASI runtime in Web Worker
- **Features**: Full C11 standard library
- **Compilation**: 3-stage process (compile → link → execute)
- **Size**: ~10MB clang + ~1.7MB stdlib (gzip compressed)
- **Source**: `/langs/clang.wasm.gz`, `/langs/wasm-ld.wasm.gz`, `/langs/clang-fs.tar.gz`
- **Progress Stages**:
  1. Loading compiler...
  2. Compiling C code...
  3. Linking WebAssembly...
  4. Running program...

### ✅ C++
- **Compiler**: Clang 8.0.1 (WASM)
- **Linker**: wasm-ld (LLVM linker)
- **Execution**: WASI runtime in Web Worker
- **Features**: Full C++17 standard library (libc++, libc++abi)
- **Compilation**: 3-stage process (compile → link → execute)
- **Size**: ~10MB clang + ~1.7MB stdlib (gzip compressed)
- **Source**: `/langs/clang.wasm.gz`, `/langs/wasm-ld.wasm.gz`, `/langs/clang-fs.tar.gz`
- **Progress Stages**:
  1. Loading compiler...
  2. Compiling C++ code...
  3. Linking WebAssembly...
  4. Running program...

### ✅ PHP
- **Interpreter**: PHP-CGI 8.2.0 (WASM)
- **Execution**: WASI runtime in Web Worker
- **Features**: Core PHP functionality, file system support
- **Size**: ~3.95MB (gzip compressed)
- **Source**: `/langs/php-cgi.wasm.gz`
- **Progress Stages**:
  1. Loading PHP interpreter...
  2. Running PHP code...
- **Notes**: 
  - Runs as php-cgi with CGI environment
  - HTTP headers are automatically stripped from output
  - Supports multi-file projects with `require/include`

### ✅ SQL
- **Engine**: SQLite 3.x (WASM)
- **Execution**: WASI runtime in Web Worker
- **Features**: Full SQLite SQL dialect, in-memory database
- **Size**: ~1.23MB (gzip compressed)
- **Source**: `/langs/sqlite.wasm.gz`
- **Progress Stages**:
  1. Loading SQLite...
  2. Running SQL queries...
- **Notes**: 
  - Uses `.read` command to execute SQL scripts
  - In-memory database (resets between executions)
  - Supports CREATE TABLE, INSERT, SELECT, UPDATE, DELETE, etc.
  - Full SQLite3 SQL syntax support

### ✅ Ruby
- **Interpreter**: Ruby 3.2.0 (WASM)
- **Execution**: WASI runtime in Web Worker
- **Features**: Core Ruby functionality, file system support
- **Size**: ~7.3MB (gzip compressed)
- **Source**: `/langs/ruby.wasm.gz`
- **Progress Stages**:
  1. Loading Ruby interpreter...
  2. Running Ruby code...
- **Notes**: 
  - Full Ruby 3.2.0 interpreter
  - Supports multi-file projects with `require/load`
  - Standard library available
  - No external gems (pure Ruby only)

### ✅ WebAssembly (WAT)
- **Compiler**: wabt (WebAssembly Binary Toolkit)
- **Execution**: WASI runtime in Web Worker
- **Features**: Full WAT to WASM compilation and execution
- **Size**: ~400KB (wabt npm package)
- **Source**: `wabt` npm package
- **Progress Stages**:
  1. Compiling WAT to WASM...
  2. Running WebAssembly...
- **Notes**: 
  - Compiles WebAssembly Text Format (.wat) to binary WASM
  - Supports all WAT features (SIMD, threads, bulk memory, etc.)
  - Executes compiled WASM with full WASI support
  - No additional WASM files needed (wabt is pure JS/WASM)

- **Compilation**: 3-stage process (compile → link → execute)
- **Size**: ~10MB clang + ~1.7MB stdlib (gzip compressed)
- **Source**: `/langs/clang.wasm.gz`, `/langs/wasm-ld.wasm.gz`, `/langs/clang-fs.tar.gz`
- **Supported Extensions**: `.cpp`, `.cxx`, `.cc`, `.hpp`
- **Progress Stages**:
  1. Loading compiler...
  2. Compiling C++ code...
  3. Linking WebAssembly...
  4. Running program...

## WASM Assets

All binaries are served compressed (gzip) and decompressed in-browser using `pako`:

### Interpreters & Transpilers
**`public/wasm/`:**
- `esbuild.wasm.gz` - 3.5MB (uncompressed: ~12.9MB)
- `quickjs-asyncify.wasm.gz` - 369KB (uncompressed: ~1MB)
- `pyodide.asm.wasm.gz` - 3.1MB (uncompressed: ~9.6MB)

**`public/pyodide/`:**
- `pyodide.js.gz` - 5.8KB (uncompressed: ~15KB)
- `pyodide.asm.js.gz` - 227KB (uncompressed: ~1.2MB)

### Compilers
**`public/langs/`:**
- `clang.wasm.gz` - 10.13MB (uncompressed: ~29.77MB)
- `wasm-ld.wasm.gz` - Size varies (LLVM linker)
- `clang-fs.tar.gz` - 1.71MB (C/C++ standard library headers and runtime)

**Total: ~19MB compressed (saves ~40MB in network transfer)**

The `clang-fs.tar.gz` contains:
- C standard library headers (`/sys/include/`)
- C++ standard library headers (`/sys/include/c++/v1/`)
- Runtime objects (`/sys/lib/wasm32-wasi/crt1.o`, `libc.a`, `libc++.a`, `libc++abi.a`)
- Clang intrinsics (`/sys/lib/clang/8.0.1/include/`)

## Compilation Architecture (C/C++)

C and C++ use a sophisticated 3-stage compilation pipeline:

### Stage 1: Compilation
- **Tool**: Clang (WASM)
- **Input**: Source code (`.c`, `.cpp`, `.cxx`, `.cc`)
- **Output**: Object file (`/program.o`)
- **Flags**: 
  - C: `-x c`, standard includes
  - C++: `-x c++`, C++ stdlib includes
- **Filesystem**: Virtual WASI filesystem with headers from `clang-fs.tar.gz`

### Stage 2: Linking
- **Tool**: wasm-ld (LLVM linker)
- **Input**: Object file + runtime libraries
- **Output**: WebAssembly binary (`/program.wasm`)
- **Libraries**:
  - C: `-lc` (libc only)
  - C++: `-lc`, `-lc++`, `-lc++abi`
- **Runtime**: Includes `crt1.o` for WASI initialization

### Stage 3: Execution
- **Runtime**: WASI (Web Assembly System Interface)
- **Host**: WASIWorkerHost (runs in Web Worker for isolation)
- **Features**: Full stdio support, filesystem access
- **Output**: Captured stdout/stderr streams

### Multi-file Support

Both C and C++ runners support multi-file projects via `executeWithFiles()`:
- Headers (`.h`, `.hpp`) are automatically available for `#include`
- All files are mounted in virtual filesystem
- Entry point specifies which file to compile

## Progressive Feedback

Compilation-based runners (C, C++) provide real-time progress updates through the `onProgress` callback:

```typescript
const runner = new UnifiedCodeRunner({
  onProgress: (message: string) => {
    console.log(message) // "Loading compiler...", "Compiling C code...", etc.
  }
})
```

This allows UIs to show compilation stages instead of just "Executing..."

## Usage

### Single File Execution

```typescript
import { UnifiedCodeRunner } from './runners'

const runner = new UnifiedCodeRunner({ 
  timeout: 30000,
  onProgress: (msg) => console.log(msg)
})

// JavaScript
const jsResult = await runner.run('javascript', `
  console.log('Hello World')
`)

// Python
const pyResult = await runner.run('python', `
import sys
print('Python version:', sys.version)
`)

// C++
const cppResult = await runner.run('cpp', `
#include <iostream>
int main() {
    std::cout << "Hello from C++!" << std::endl;
    return 0;
}
`)

console.log(result.stdout)        // Output
console.log(result.stderr)        // Errors  
console.log(result.exitCode)      // 0 or error code
console.log(result.executionTime) // ms

runner.dispose()
```

### Multi-file Projects

```typescript
const result = await runner.runWithFiles('cpp', '/main.cpp', {
  '/main.cpp': `
    #include "helper.hpp"
    int main() { 
      return helper_function(); 
    }
  `,
  '/helper.hpp': `
    int helper_function() { 
      return 0; 
    }
  `
})
```

## Cache Management

WASM binaries and filesystems are cached in IndexedDB for instant subsequent loads:

```typescript
import { clearWasmCache } from './runners'

// Clear all cached WASM files
await clearWasmCache()
```

Cache is automatically versioned - updates to `DB_VERSION` or `CACHE_FORMAT_VERSION` invalidate old cache.

## Updating WASM Assets

To update all WASM files from their sources:

```bash
npm run update-wasm
```

This script:
1. Compresses WASM from `node_modules` (esbuild, quickjs)
2. Downloads Pyodide from official CDN and compresses
3. Saves to `public/wasm/` and `public/pyodide/`
4. Runs automatically on `postinstall`

**Note**: Clang/LLVM assets must be obtained separately from the Runno project and placed in `public/langs/`.

## Future Languages

Planned additions with implementation notes:

- **Go**: TinyGo WASM compiler
- **Rust**: rustc + wasm32-wasi target

