/**
 * Go WebAssembly bindings (GOImports namespace)
 * For WebAssembly compiled from Go
 */
export function createGoBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    // Go's WebAssembly runtime expects specific imports
    'runtime.wasmExit': (code: number) => {
      stdout(`\n[Exit code: ${code}]`)
    },

    'runtime.wasmWrite': (fd: number, ptr: number, len: number) => {
      // fd: 1 = stdout, 2 = stderr
      const output = fd === 1 ? stdout : stderr
      output(`[Go write to fd=${fd}, len=${len}]`)
    },

    'runtime.nanotime': () => BigInt(Date.now() * 1000000),
    'runtime.walltime': () => BigInt(Date.now()),

    'runtime.scheduleCallback': () => 0,
    'runtime.clearScheduledCallback': () => {},
    'runtime.getRandomData': (ptr: number, len: number) => {},
  }
}
