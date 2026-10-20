import type { ProgrammingLanguage } from "@/components/block-content-editor/extras/source-code/types"
import type { ExecutionContext, ExecutionResult, LanguageExecutor } from "./types"
import { getFileContent } from "@/components/block-content-editor/extras/source-code/utils"

export abstract class CompiledLanguageExecutor implements LanguageExecutor {
  handleCommand?: ((command: string, context: ExecutionContext) => boolean) | undefined
  protected abstract languageName: string
  protected abstract compilerName: string
  protected abstract fileExtension: string
  protected abstract supportedLanguages: ProgrammingLanguage[]
  private isExecutionCancelled = false
  public isCompiled = true // Set the isCompiled flag to true

  // Abstract methods to be implemented by subclasses
  protected abstract compile(
    sourceFiles: Record<string, string>,
    mainFile: string,
  ): Promise<ExecutionResult>
  protected abstract executeCompiled(compiledCode: ExecutionResult): Promise<ExecutionResult>

  // Implementation of the LanguageExecutor interface
  execute = async (fileId: string, context: ExecutionContext): Promise<ExecutionResult> => {
    // Ensure context is defined with default values if needed
    if (!context) {
      console.error(`${this.languageName} execution error: context is undefined`)
      return {
        success: false,
        output: [`Error: Execution context is undefined`],
      }
    }

    const { files, selectedLanguage, addOutput, setIsExecuting } = context

    // Additional safety checks for required context properties
    if (!files || !Array.isArray(files)) {
      console.error(`${this.languageName} execution error: files array is missing or invalid`)
      return {
        success: false,
        output: [`Error: Files array is missing or invalid`],
      }
    }

    if (typeof addOutput !== "function") {
      console.error(`${this.languageName} execution error: addOutput function is missing`)
      return {
        success: false,
        output: [`Error: Output handler is missing`],
      }
    }

    if (typeof setIsExecuting !== "function") {
      console.error(`${this.languageName} execution error: setIsExecuting function is missing`)
      return {
        success: false,
        output: [`Error: Execution state handler is missing`],
      }
    }

    this.isExecutionCancelled = false
    setIsExecuting(true)

    try {
      // Find the file to execute
      const visibleFiles = files.filter((file) => file.isVisible)
      const mainFile = visibleFiles.find((file) => file.isMain)
      const activeFile = files.find((file) => file.id === fileId)
      const fileToExecute = mainFile || activeFile

      if (!fileToExecute) {
        addOutput(`Error: No file selected for execution.`)
        setIsExecuting(false)
        return { success: false, output: [`Error: No file selected for execution.`] }
      }

      // If executing a main file that's different from the active file, show a message
      if (mainFile && mainFile.id !== fileId) {
        addOutput(`Executing main file: ${mainFile.name}`)
      }

      // Create a virtual file system for compilation
      const sourceFiles: Record<string, string> = {}
      visibleFiles.forEach((file) => {
        const content = getFileContent(file, selectedLanguage || "plaintext")
        sourceFiles[file.name] = content
      })

      // Compile the code
      addOutput(`Compiling with ${this.compilerName}...`)
      const compilationResult = await this.compile(sourceFiles, fileToExecute.name)

      // Check if compilation was successful
      if (!compilationResult.success) {
        addOutput([`Compilation failed:`, ...compilationResult.output])
        setIsExecuting(false)
        return { success: false, output: [`Compilation failed:`, ...compilationResult.output] }
      }

      // Check if execution was cancelled
      if (this.isExecutionCancelled) {
        addOutput(`Execution cancelled.`)
        setIsExecuting(false)
        return { success: false, output: [`Execution cancelled.`] }
      }

      // Execute the compiled code
      addOutput(`Running ${this.languageName} program...`)
      const executionResult = await this.executeCompiled(compilationResult)

      // Check if execution was successful
      if (!executionResult.success) {
        addOutput([`Execution failed:`, ...executionResult.output])
        setIsExecuting(false)
        return { success: false, output: [`Execution failed:`, ...executionResult.output] }
      }

      // Output the result
      addOutput(executionResult.output)

      setIsExecuting(false)
      return { success: true, output: executionResult.output }
    } catch (error) {
      console.error(`${this.languageName} execution error:`, error)
      const errorMessage = error instanceof Error ? error.message : String(error)
      addOutput(`Error: ${errorMessage}`)
      setIsExecuting(false)
      return { success: false, output: [`Error: ${errorMessage}`] }
    }
  }

  stop = () => {
    this.isExecutionCancelled = true
  }

  getFileExtension = (): string => {
    return this.fileExtension
  }

  getSupportedLanguages = (): ProgrammingLanguage[] => {
    return this.supportedLanguages
  }
}
