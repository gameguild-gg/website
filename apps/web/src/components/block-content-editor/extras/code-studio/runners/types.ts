export interface RunnerResult {
  stdout: string
  stderr: string
  exitCode: number
  executionTime: number
}

export interface FileMap {
  [path: string]: string
}

export interface CodeRunner {
  execute(code: string, stdin?: string): Promise<RunnerResult>
  executeWithFiles?(entryPoint: string, files: FileMap, stdin?: string): Promise<RunnerResult>
  interrupt(): Promise<void>
  dispose(): void
}

export interface RunnerOptions {
  onOutput?: (output: string, stream: 'stdout' | 'stderr') => void
  onDialog?: (kind: 'alert' | 'prompt' | 'confirm', message: string, defaultValue: string) => Promise<string | boolean | undefined>
  persistContext?: boolean
  timeout?: number // ms
  memoryLimit?: number // bytes
  onRequestInput?: (prompt?: string, currentOutput?: string) => Promise<string> // Callback for interactive input
  onProgress?: (message: string) => void // Callback para feedback de progresso
}
