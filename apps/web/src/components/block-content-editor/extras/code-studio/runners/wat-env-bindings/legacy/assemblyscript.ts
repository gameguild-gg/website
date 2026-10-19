/**
 * AssemblyScript-specific bindings
 */
export function createAssemblyScriptBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    // AssemblyScript runtime hooks
    abort: (message: any, fileName: any, line: number, column: number) => {
      stderr(`AssemblyScript abort: ${message} at ${fileName}:${line}:${column}`)
      throw new Error(`Abort: ${message}`)
    },

    trace: (message: any, n: number, ...args: number[]) => {
      stdout(`[TRACE] ${message} n=${n} args=${args.join(', ')}`)
    },

    seed: () => Date.now(),
  }
}
