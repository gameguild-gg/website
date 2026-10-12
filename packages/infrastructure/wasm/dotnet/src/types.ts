/**
 * Type definitions for Mono WebAssembly Runtime
 */

export interface MonoConfig {
  assemblyRootFolder?: string
  assets?: MonoAsset[]
  debugLevel?: number
  maxParallelDownloads?: number
  enableDebugging?: boolean
  globalizationMode?: 'icu' | 'invariant' | 'sharded'
  remoteSources?: string[]
}

export interface MonoAsset {
  name: string
  behavior: 'assembly' | 'resource' | 'icu' | 'vfs'
  virtualPath?: string
  culture?: string
  loadRemote?: boolean
  isOptional?: boolean
}

export interface MonoRuntime {
  MONO: {
    mono_wasm_load_runtime: (unused: string, debugLevel: number) => void
    mono_wasm_setenv: (name: string, value: string) => void
    mono_call_assembly_entry_point: (
      assembly: string,
      args: string[],
      signature: string
    ) => number
    mono_wasm_add_assembly: (name: string, data: Uint8Array) => void
  }
  Module: {
    print: (text: string) => void
    printErr: (text: string) => void
    onRuntimeInitialized?: () => void
    preRun?: Array<() => void>
    postRun?: (() => void)[]
    FS: MonoFS
  }
  config?: MonoConfig
}

export interface MonoFS {
  createPath: (
    parent: string,
    path: string,
    canRead: boolean,
    canWrite: boolean
  ) => void
  createDataFile: (
    parent: string,
    name: string,
    data: Uint8Array | string,
    canRead: boolean,
    canWrite: boolean,
    canOwn: boolean
  ) => void
  readFile: (path: string, opts?: { encoding?: string }) => string | Uint8Array
  writeFile: (path: string, data: string | Uint8Array) => void
  mkdir: (path: string) => void
  rmdir: (path: string) => void
  unlink: (path: string) => void
}

declare global {
  interface Window {
    MonoRuntime?: MonoRuntime
  }
}
