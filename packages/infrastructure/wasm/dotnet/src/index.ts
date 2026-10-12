import { RuntimeLoader } from './csharp/runtime-loader'

interface CSharpCompilerExports {
  compileAndRun(code: string): string
  compileAndRunMultiple(mainCode: string, filesJson: string): string
}

export interface CSharpResult {
  output?: string
  error?: string
  executionTime: number
}

export interface CSharpFile {
  name: string
  content: string
}

export class CSharpCompiler {
  private loader: RuntimeLoader
  private isInitialized = false

  constructor(private basePath = '') {
    this.loader = new RuntimeLoader()
  }

  /**
   * Initialize the C# compiler and runtime
   */
  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return
    }

    console.log('[CSharpCompiler] Initializing...')
    await this.loader.initialize(this.basePath)
    this.isInitialized = true
    console.log('[CSharpCompiler] Ready')
  }

  /**
   * Compile and execute C# code
   */
  async execute(code: string): Promise<CSharpResult> {
    if (!this.loader.isInitialized()) {
      await this.initialize()
    }

    const startTime = performance.now()
    
    try {
      // Call the C# function exposed via JSExport
      const result = (window as unknown as { CSharpCompiler: CSharpCompilerExports }).CSharpCompiler.compileAndRun(code)
      const executionTime = performance.now() - startTime

      return this.parseResult(result, executionTime)
    } catch (error) {
      const executionTime = performance.now() - startTime
      return {
        error: error instanceof Error ? error.message : String(error),
        executionTime
      }
    }
  }

  /**
   * Compile and execute C# code with multiple files
   * @param mainCode The main code containing the entry point (Main method)
   * @param files Additional files to compile together
   */
  async executeMultiple(mainCode: string, files: CSharpFile[]): Promise<CSharpResult> {
    if (!this.loader.isInitialized()) {
      await this.initialize()
    }

    const startTime = performance.now()
    
    try {
      // Convert files to JSON format expected by C#
      const filesMap: Record<string, string> = {}
      for (const file of files) {
        filesMap[file.name] = file.content
      }
      const filesJson = JSON.stringify(filesMap)

      // Call the C# function exposed via JSExport
      const result = (window as unknown as { CSharpCompiler: CSharpCompilerExports }).CSharpCompiler.compileAndRunMultiple(mainCode, filesJson)
      const executionTime = performance.now() - startTime

      return this.parseResult(result, executionTime)
    } catch (error) {
      const executionTime = performance.now() - startTime
      return {
        error: error instanceof Error ? error.message : String(error),
        executionTime
      }
    }
  }

  private parseResult(result: string, executionTime: number): CSharpResult {
    // Parse result
    if (result.startsWith('COMPILATION_ERROR')) {
      return {
        error: result.substring('COMPILATION_ERROR\n'.length),
        executionTime
      }
    }

    if (result.startsWith('RUNTIME_ERROR')) {
      return {
        error: result.substring('RUNTIME_ERROR\n'.length),
        executionTime
      }
    }

    if (result.startsWith('ERROR:')) {
      return {
        error: result.substring('ERROR: '.length),
        executionTime
      }
    }

    return {
      output: result,
      executionTime
    }
  }

  /**
   * Check if compiler is initialized
   */
  isReady(): boolean {
    return this.isInitialized
  }
}

// Export runtime loader
export { RuntimeLoader }

// Default export
export default CSharpCompiler
