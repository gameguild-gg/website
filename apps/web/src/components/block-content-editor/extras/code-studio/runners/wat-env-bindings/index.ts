/**
 * WebAssembly Component Model Bindings for WAT Runner
 * 
 * Implements WASI Preview 2 and custom GameGuild interfaces following
 * the WebAssembly Component Model specification.
 * 
 * @see https://github.com/WebAssembly/component-model
 * @see https://github.com/WebAssembly/WASI/tree/main/preview2
 */

import type { InputStream, OutputStream, Pollable } from './types'
import {
  createCliEnvironment,
  createCliExit,
  createCliStdin,
  createCliStdout,
  createCliStderr,
} from './wasi/cli'
import { createWallClock, createMonotonicClock } from './wasi/clocks'
import { createFilesystemPreopens } from './wasi/filesystem'
import { createStreams, createPoll } from './wasi/io'
import { createRandom } from './wasi/random'
import { createConsole, createDebug, createTest } from './gameguild'

// Legacy imports for backward compatibility
import { createEnvBindings } from './legacy/env'
import { createAssemblyScriptBindings } from './legacy/assemblyscript'
import { createGoBindings } from './legacy/go'
import { createMathBindings } from './builtin/math'
import { createDateBindings } from './builtin/date'
import { createNumberBindings } from './builtin/number'
import { createPerformanceBindings } from './builtin/performance'
import { createCryptoBindings } from './builtin/crypto'
import { createStringBindings } from './builtin/string'
import { createObjectBindings } from './builtin/object'
import { createReflectBindings } from './builtin/reflect'
import { createJSBindings } from './builtin/js'
import { createWindowBindings } from './builtin/window'
import { createDocumentBindings } from './builtin/document'

export { createMemoryHelpers } from './builtin/memory'
export { createDOMBindings } from './builtin/dom'

/**
 * Creates Component Model compatible bindings (WASI Preview 2)
 */
export function createComponentModelBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  // Stream management
  let nextStreamId = 0
  const streams = new Map<number, { type: 'input' | 'output'; onWrite?: (data: Uint8Array) => void }>()
  
  const stdoutStream = nextStreamId++
  streams.set(stdoutStream, {
    type: 'output',
    onWrite: (data) => stdout(new TextDecoder().decode(data)),
  })
  
  const stderrStream = nextStreamId++
  streams.set(stderrStream, {
    type: 'output',
    onWrite: (data) => stderr(new TextDecoder().decode(data)),
  })
  
  const stdinStream = nextStreamId++
  streams.set(stdinStream, { type: 'input' })

  const createPollable = () => nextStreamId++

  return {
    // WASI CLI interfaces
    'wasi:cli/environment@0.2.0': createCliEnvironment(),
    'wasi:cli/exit@0.2.0': createCliExit(),
    'wasi:cli/stdin@0.2.0': createCliStdin(stdinStream),
    'wasi:cli/stdout@0.2.0': createCliStdout(stdoutStream),
    'wasi:cli/stderr@0.2.0': createCliStderr(stderrStream),
    
    // WASI Clocks
    'wasi:clocks/wall-clock@0.2.0': createWallClock(),
    'wasi:clocks/monotonic-clock@0.2.0': createMonotonicClock(createPollable),
    
    // WASI Filesystem
    'wasi:filesystem/preopens@0.2.0': createFilesystemPreopens(),
    
    // WASI I/O
    'wasi:io/streams@0.2.0': createStreams(streams, createPollable),
    'wasi:io/poll@0.2.0': createPoll(),
    
    // WASI Random
    'wasi:random/random@0.2.0': createRandom(),
    
    // GameGuild custom interfaces
    'gameguild:runtime/console@0.1.0': createConsole(stdout, stderr),
    'gameguild:runtime/debug@0.1.0': createDebug(stdout, stderr),
    'gameguild:runtime/test@0.1.0': createTest(stdout, stderr),
  }
}

/**
 * Creates the complete environment import object for WebAssembly modules
 * 
 * @param stdout - Callback for standard output
 * @param stderr - Callback for standard error
 * @param memory - Optional shared memory instance
 * @param options - Configuration options
 */
export function createWatEnvironment(
  stdout: (text: string) => void,
  stderr: (text: string) => void,
  memory?: WebAssembly.Memory,
  options?: { useComponentModel?: boolean }
) {
  // Use Component Model (WASI Preview 2) if requested
  if (options?.useComponentModel) {
    return createComponentModelBindings(stdout, stderr)
  }

  // Legacy flat import structure (WASI Preview 1 + custom namespaces)
  const sharedMemory = memory || new WebAssembly.Memory({ initial: 256, maximum: 512 })

  return {
    env: createEnvBindings(stdout, stderr, sharedMemory),
    Math: createMathBindings(),
    Date: createDateBindings(),
    Number: createNumberBindings(),
    performance: createPerformanceBindings(),
    crypto: createCryptoBindings(),
    String: createStringBindings(),
    Object: createObjectBindings(),
    Reflect: createReflectBindings(),

    // Language-specific bindings
    'assembly/index': createAssemblyScriptBindings(stdout, stderr),
    go: createGoBindings(stdout, stderr),
    GOImports: createGoBindings(stdout, stderr),

    // JavaScript environment
    js: createJSBindings(),

    // Web APIs stub
    window: createWindowBindings(stdout, stderr),
    document: createDocumentBindings(stdout, stderr),
  }
}

