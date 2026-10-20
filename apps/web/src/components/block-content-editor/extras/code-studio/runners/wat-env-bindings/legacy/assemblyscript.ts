/**
 * AssemblyScript-specific bindings
 */
export function createAssemblyScriptBindings(
  stdout: (text: string) => void,
  stderr: (text: string) => void
) {
  return {
    // AssemblyScript runtime hooks
    abort: (message: unknown, fileName: unknown, line: number, column: number) => {
      stderr(`AssemblyScript abort: ${String(message)} at ${String(fileName)}:${line}:${column}`)
      throw new Error(`Abort: ${String(message)}`)
    },

    trace: (message: unknown, n: number, ...args: number[]) => {
      stdout(`[TRACE] ${String(message)} n=${n} args=${args.join(', ')}`)
    },

    seed: () => Date.now(),
  }
}
