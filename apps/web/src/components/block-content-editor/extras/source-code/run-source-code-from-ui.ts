/** Start an asynchronous execution from a synchronous UI callback. */
export function runSourceCodeFromUi(
  execute: () => Promise<void>,
  reportFailure: (message: string) => void,
): void {
  void execute().catch((error: unknown) => {
    reportFailure(error instanceof Error ? error.message : String(error))
  })
}
