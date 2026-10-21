import type { CodeRunner, RunnerResult, RunnerOptions, FileMap } from './types'
import type { WASIFS } from '@runno/wasi'
import { createWatEnvironment } from './wat-env-bindings'
import { createWasiStdinReader } from './wasi-stdin'

// Load wabt from local public assets (managed by update-wasm.mjs script)
let wabtInstance: any = null

async function getWabtInstance(): Promise<any> {
  if (wabtInstance) {
    return wabtInstance
  }

  try {
    // Load wabt from local assets if not already loaded
    if (typeof window !== 'undefined' && !(window as any).WabtModule) {
      await new Promise((resolve, reject) => {
        const script = document.createElement('script')
        script.src = '/langs/wabt.js'
        script.onload = resolve
        script.onerror = () => reject(new Error('Failed to load wabt.js'))
        document.head.appendChild(script)
      })
    }

    // Initialize wabt
    const WabtModule = (window as any).WabtModule
    if (!WabtModule) {
      throw new Error('WabtModule not found after script load')
    }
    
    wabtInstance = await WabtModule()
    return wabtInstance
  } catch (error) {
    console.error('[WatRunner] Failed to load/initialize wabt:', error)
    throw new Error(`Failed to initialize WebAssembly compiler: ${error instanceof Error ? error.message : String(error)}`)
  }
}

export class WatRunner implements CodeRunner {
  private isInterrupted = false
  private readonly options: RunnerOptions

  constructor(options: RunnerOptions = {}) {
    this.options = {
      timeout: options.timeout || 30000,
      memoryLimit: options.memoryLimit || 64 * 1024 * 1024,
      onRequestInput: options.onRequestInput,
      onProgress: options.onProgress,
    }
  }

  private async compileWatToWasm(watCode: string, filename: string = 'program.wat'): Promise<Uint8Array> {
    try {
      const wabtModule = await getWabtInstance()
      
      // Parse WAT to a module using parseWat
      const wasmModule = wabtModule.parseWat(filename, watCode, {
        exceptions: true,
        mutable_globals: true,
        sat_float_to_int: true,
        sign_extension: true,
        simd: true,
        threads: true,
        multi_value: true,
        tail_call: true,
        bulk_memory: true,
        reference_types: true,
      })

      // Validate the module
      wasmModule.validate()

      // Convert to binary WASM
      const binaryOutput = wasmModule.toBinary({
        log: false,
        write_debug_names: true,
      })

      // Clean up
      wasmModule.destroy()

      // Return the buffer (Uint8Array)
      return binaryOutput.buffer
    } catch (error) {
      // Improve error message for WAT compilation errors
      console.error(`[WatRunner] Compilation error in ${filename}:`, error)
      if (error instanceof Error) {
        throw new Error(`WAT compilation failed in ${filename}: ${error.message}`)
      }
      throw new Error(`WAT compilation failed in ${filename}: ${String(error)}`)
    }
  }

  /**
   * Compile all WAT files to WASM binaries
   * This is necessary when WAT files import each other
   */
  private async compileAllWatFiles(files: FileMap): Promise<Map<string, Uint8Array>> {
    const compiledModules = new Map<string, Uint8Array>()
    const watFiles = Object.entries(files).filter(([path]) => path.endsWith('.wat'))

    this.options.onProgress?.(`Compiling ${watFiles.length} WAT file(s)...`)

    // Compile all WAT files to WASM
    for (const [path, watCode] of watFiles) {
      try {
        console.log(`[WatRunner] Compiling ${path}...`)
        const wasmBinary = await this.compileWatToWasm(watCode, path)
        compiledModules.set(path, wasmBinary)
        
        // Also store the .wasm version of the filename
        const wasmPath = path.replace(/\.wat$/, '.wasm')
        compiledModules.set(wasmPath, wasmBinary)
        
        console.log(`[WatRunner] Compiled ${path} -> ${wasmBinary.length} bytes`)
      } catch (error) {
        console.error(`[WatRunner] Failed to compile ${path}:`, error)
        throw error
      }
    }

    return compiledModules
  }

  /**
   * Create a WASI filesystem with all compiled WASM modules
   */
  private createWASIFilesystem(
    files: FileMap,
    compiledModules: Map<string, Uint8Array>
  ): WASIFS {
    const wasmFS: WASIFS = {}

    // Add all source files (non-WAT files)
    for (const [path, content] of Object.entries(files)) {
      if (!path.endsWith('.wat')) {
        wasmFS[path] = {
          path,
          timestamps: {
            access: new Date(),
            modification: new Date(),
            change: new Date(),
          },
          mode: 'string',
          content,
        }
      }
    }

    // Add all compiled WASM modules as binary files
    for (const [path, binary] of compiledModules.entries()) {
      wasmFS[path] = {
        path,
        timestamps: {
          access: new Date(),
          modification: new Date(),
          change: new Date(),
        },
        mode: 'binary',
        content: binary,
      }
    }

    return wasmFS
  }

  private async runWasm(
    wasmBinary: Uint8Array,
    wasmFS: WASIFS,
    compiledModules: Map<string, Uint8Array> = new Map(),
    stdin?: string
  ): Promise<{ exitCode: number; stdout: string; stderr: string }> {
    let wasmStdout = ''
    let wasmStderr = ''

    try {
      // Create shared memory for environment
      const sharedMemory = new WebAssembly.Memory({ initial: 256, maximum: 512 })

      // Create comprehensive environment using wat-env-bindings
      const envBindings = createWatEnvironment(
        (text: string) => {
          wasmStdout += text
          console.log('[WAT stdout]', text)
        },
        (text: string) => {
          wasmStderr += text
          console.error('[WAT stderr]', text)
        },
        sharedMemory
      )

      // Ensure importObject is properly structured
      const importObject: Record<string, any> = { ...envBindings }
      
      console.log('[WatRunner] Import object keys:', Object.keys(importObject))
      console.log('[WatRunner] env is object:', typeof importObject.env === 'object' && importObject.env !== null)

      // First, instantiate all imported modules
      for (const [moduleName, moduleBytes] of compiledModules.entries()) {
        try {
          console.log(`[WatRunner] Pre-instantiating module: ${moduleName}`)
          
          // Recursively instantiate with the growing import object
          const instantiated = await WebAssembly.instantiate(
            moduleBytes,
            importObject as WebAssembly.Imports
          ) as unknown as WebAssembly.WebAssemblyInstantiatedSource

          const exported = instantiated.instance.exports

          if (!exported) {
            console.warn(`[WatRunner] Module ${moduleName} has no exports`)
            continue
          }

          // Add exports using multiple name variations
          const variations = new Set<string>()

          // Full path without extension: /wat/math.wat -> /wat/math
          variations.add(moduleName.replace(/\.(wat|wasm)$/, ''))

          // Just filename without extension: /wat/math.wat -> math
          const fileName = moduleName.split('/').pop()?.replace(/\.(wat|wasm)$/, '')
          if (fileName) {
            variations.add(fileName)
          }

          // Register all variations
          for (const name of variations) {
            importObject[name] = exported
            console.log(`[WatRunner] Registered import: "${name}" with ${Object.keys(exported).length} exports`)
          }
        } catch (error) {
          console.error(`[WatRunner] Failed to preload module ${moduleName}:`, error)
          throw new Error(`Failed to load dependency ${moduleName}: ${error instanceof Error ? error.message : String(error)}`)
        }
      }

      console.log('[WatRunner] Available imports:', Object.keys(importObject))

      // Now instantiate the main module with all imports available
      const { WASI } = await import('@runno/wasi')
      
      const wasi = new WASI({
        args: ['program.wasm'],
        env: {},
        fs: wasmFS,
        stdout: (out: string) => { wasmStdout += out },
        stderr: (err: string) => { wasmStderr += err },
      })

      // The pinned WASI string callback truncates encoded input to each iovec.
      // Share a byte cursor across both namespaces; retain native reads for all
      // other descriptors and every other native WASI operation.
      const readStdin = createWasiStdinReader(stdin ?? '', () => wasi.memory)
      const wasiImports = wasi.getImportObject()
      for (const namespace of Object.values(wasiImports)) {
        const fdRead = namespace.fd_read.bind(namespace)
        namespace.fd_read = (fd, pointer, length, result) =>
          fd === 0 ? readStdin(pointer, length, result) : fdRead(fd, pointer, length, result)
      }
      Object.assign(importObject, wasiImports)

      // Instantiate the main module with imports
      const instantiateResult = await WebAssembly.instantiate(
        wasmBinary as BufferSource,
        importObject as WebAssembly.Imports
      )
      const instantiatedSource = instantiateResult as WebAssembly.WebAssemblyInstantiatedSource
      
      // Check if module has _start (WASI command) or _initialize (WASI reactor)
      const exports = instantiatedSource.instance.exports
      const memory = exports.memory instanceof WebAssembly.Memory ? exports.memory : sharedMemory
      let exitCode = 0
      
      console.log('[WatRunner] Module exports:', Object.keys(exports))
      
      if ('_start' in exports && typeof exports._start === 'function') {
        // It's a WASI command - run it
        console.log('[WatRunner] Running WASI command with _start')
        exitCode = wasi.start(instantiatedSource, { memory }).exitCode
      } else if ('_initialize' in exports && typeof exports._initialize === 'function') {
        // It's a WASI reactor - initialize it
        console.log('[WatRunner] Initializing WASI reactor with _initialize')
        wasi.initialize(instantiatedSource, { memory })
        
        // If there's a main function, call it
        if ('main' in exports && typeof exports.main === 'function') {
          console.log('[WatRunner] Calling main function after _initialize')
          const result = (exports.main as Function)()
          exitCode = typeof result === 'number' ? result : 0
        }
      } else if ('main' in exports && typeof exports.main === 'function') {
        // Module has main but no WASI initialization
        wasi.initialize(instantiatedSource, { memory })
        console.log('[WatRunner] Calling main function')
        
        try {
          const result = (exports.main as Function)()
          
          // The result might be the value to print
          if (typeof result === 'number') {
            wasmStdout += result.toString() + '\n'
            exitCode = 0
          } else if (result !== undefined) {
            wasmStdout += String(result) + '\n'
            exitCode = 0
          } else {
            // Function executed successfully but returned nothing
            exitCode = 0
          }
          
          console.log('[WatRunner] Main function returned:', result)
          console.log('[WatRunner] Captured stdout:', wasmStdout)
          console.log('[WatRunner] Captured stderr:', wasmStderr)
        } catch (error) {
          console.error('[WatRunner] Error calling main:', error)
          throw error
        }
      } else {
        // No standard entry point - this might be a pure library module
        wasi.initialize(instantiatedSource, { memory })
        // Try to find any exported function that looks like an entry point
        const exportKeys = Object.keys(exports)
        const callableFuncs = exportKeys.filter(key => typeof exports[key] === 'function')
        
        if (callableFuncs.length === 0) {
          throw new Error(`Module has no callable functions. Exports: ${exportKeys.join(', ')}`)
        }
        
        // If there's exactly one function, call it
        if (callableFuncs.length === 1) {
          const funcName: string = callableFuncs[0] as string
          console.log(`[WatRunner] Calling single exported function: ${funcName}`)
          const result = (exports[funcName] as Function)()
          if (typeof result === 'number') {
            wasmStdout += result.toString() + '\n'
          }
          exitCode = 0
        } else {
          throw new Error(`Module has no standard entry point (_start, _initialize, or main). Available functions: ${callableFuncs.join(', ')}`)
        }
      }

      return {
        exitCode,
        stdout: wasmStdout,
        stderr: wasmStderr,
      }
    } catch (error) {
      console.error('[WatRunner] WASM execution error:', error)
      
      let errorMessage = String(error)
      if (error instanceof Error) {
        errorMessage = error.message
      } else if (typeof error === 'object' && error !== null) {
        errorMessage = JSON.stringify(error, null, 2)
      }
      
      throw new Error(`WASM execution failed: ${errorMessage}`)
    }
  }

  async execute(code: string, stdin?: string): Promise<RunnerResult> {
    const startTime = performance.now()
    
    try {
      this.isInterrupted = false

      this.options.onProgress?.('Compiling WAT to WASM...')
      
      // Compile WAT to WASM binary
      const wasmBinary = await this.compileWatToWasm(code, 'program.wat')
      
      if (!wasmBinary || wasmBinary.length === 0) {
        throw new Error('WAT compilation produced empty binary')
      }

      const wasmFS: WASIFS = {}

      this.options.onProgress?.('Running WebAssembly...')
      const runResult = await this.runWasm(wasmBinary, wasmFS, undefined, stdin)
      
      const executionTime = performance.now() - startTime
      
      return {
        stdout: runResult.stdout.trimEnd(),
        stderr: runResult.stderr.trimEnd(),
        exitCode: runResult.exitCode,
        executionTime,
      }
    } catch (error) {
      const executionTime = performance.now() - startTime
      
      console.error('[WatRunner] execute caught error:', error)
      
      let errorMessage = 'Unknown error'
      if (error instanceof Error) {
        errorMessage = error.message
        if (error.stack) {
          console.error('WatRunner execute error stack:', error.stack)
        }
      } else if (typeof error === 'object' && error !== null) {
        errorMessage = JSON.stringify(error, null, 2)
      } else {
        errorMessage = String(error)
      }
      
      return {
        stdout: '',
        stderr: errorMessage,
        exitCode: 1,
        executionTime,
      }
    }
  }

  async executeWithFiles(entryPoint: string, files: FileMap, stdin?: string): Promise<RunnerResult> {
    const startTime = performance.now()
    
    try {
      this.isInterrupted = false

      // Get the WAT code from the entry point
      const watCode = files[entryPoint]
      if (!watCode) {
        throw new Error(`Entry point file not found: ${entryPoint}`)
      }

      // Compile all WAT files to WASM (handles imports)
      const compiledModules = await this.compileAllWatFiles(files)

      // Get the main module binary
      const mainBinary = compiledModules.get(entryPoint)
      if (!mainBinary) {
        throw new Error(`Failed to compile entry point: ${entryPoint}`)
      }

      // Create filesystem with all compiled modules and other files
      const wasmFS = this.createWASIFilesystem(files, compiledModules)

      // Remove the main module from compiledModules to avoid re-instantiation
      const importModules = new Map(compiledModules)
      importModules.delete(entryPoint)
      
      // Also try removing the .wasm version
      const wasmPath = entryPoint.replace(/\.wat$/, '.wasm')
      importModules.delete(wasmPath)

      this.options.onProgress?.('Running WebAssembly...')
      const runResult = await this.runWasm(mainBinary, wasmFS, importModules, stdin)
      
      const executionTime = performance.now() - startTime
      
      return {
        stdout: runResult.stdout.trimEnd(),
        stderr: runResult.stderr.trimEnd(),
        exitCode: runResult.exitCode,
        executionTime,
      }
    } catch (error) {
      const executionTime = performance.now() - startTime
      
      console.error('[WatRunner] executeWithFiles caught error:', error)
      
      let errorMessage = 'Unknown error'
      if (error instanceof Error) {
        errorMessage = error.message
        if (error.stack) {
          console.error('WatRunner executeWithFiles error stack:', error.stack)
        }
      } else if (typeof error === 'object' && error !== null) {
        errorMessage = JSON.stringify(error, null, 2)
      } else {
        errorMessage = String(error)
      }
      
      return {
        stdout: '',
        stderr: errorMessage,
        exitCode: 1,
        executionTime,
      }
    }
  }

  async interrupt(): Promise<void> {
    this.isInterrupted = true
  }

  dispose(): void {
    // Cleanup if needed
  }
}
