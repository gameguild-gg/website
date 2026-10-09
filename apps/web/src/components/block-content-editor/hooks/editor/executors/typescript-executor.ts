import type { ProgrammingLanguage } from "@/components/block-content-editor/extras/source-code/types"
import { getFileContent } from "@/components/block-content-editor/extras/source-code/utils"
import typeScriptManifest from "typescript/package.json"
import { JavaScriptExecutor } from "./javascript-executor"
import type { ExecutionContext, ExecutionResult, LanguageExecutor } from "./types"

class TypeScriptExecutor implements LanguageExecutor {
  public isCompiled = false
  private readonly javascript = new JavaScriptExecutor("ts")
  private executionId = 0
  private activeContext: ExecutionContext | null = null

  execute = async (fileId: string, context: ExecutionContext): Promise<ExecutionResult> => {
    this.stop()
    const executionId = this.executionId
    this.activeContext = context
    const { addOutput, setIsExecuting } = context
    setIsExecuting(true)

    try {
      addOutput("Loading TypeScript compiler...")
      const ts = await import("typescript")
      if (executionId !== this.executionId) throw new Error("Execution cancelled.")
      addOutput("TypeScript compiler loaded successfully.")
      const errors: string[] = []
      const files = context.files.map(file => {
        if (!file.isVisible) return file
        const transpiled = ts.transpileModule(getFileContent(file, context.selectedLanguage), {
          fileName: file.name, reportDiagnostics: true,
          compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
        })
        for (const diagnostic of transpiled.diagnostics ?? []) {
          if (diagnostic.category !== ts.DiagnosticCategory.Error) continue
          const location = diagnostic.file?.getLineAndCharacterOfPosition(diagnostic.start ?? 0)
          const position = location ? `(${location.line + 1},${location.character + 1})` : ""
          errors.push(`Error ${file.name}${position}: ${ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")}`)
        }
        return { ...file, content: transpiled.outputText }
      })
      if (errors.length) {
        addOutput(["TypeScript compilation failed with errors:", ...errors])
        return { success: false, output: errors }
      }
      if (executionId !== this.executionId) throw new Error("Execution cancelled.")
      addOutput("Executing transpiled JavaScript...")
      return await this.javascript.execute(fileId, { ...context, files, selectedLanguage: "javascript" })
    } catch (error) {
      const message = "Error: " + (error instanceof Error ? error.message : String(error))
      addOutput(message)
      return { success: false, output: [message] }
    } finally {
      if (executionId === this.executionId) {
        this.activeContext = null
        setIsExecuting(false)
      }
    }
  }

  stop = () => {
    this.executionId += 1
    this.javascript.stop()
    this.activeContext?.setIsExecuting(false)
    this.activeContext = null
  }

  getFileExtension = (): string => "ts"

  getSupportedLanguages = (): ProgrammingLanguage[] => ["typescript"]

  handleCommand = (command: string, context: ExecutionContext): boolean => {
    if (command.toLowerCase() === "tsc --version") {
      context.addOutput("TypeScript Version " + typeScriptManifest.version)
      return true
    }
    if (command.includes(":") || command.includes("interface") || command.includes("class")) {
      context.addOutput("Direct TypeScript evaluation in the terminal is not supported. Please create a file to execute TypeScript code.")
      return true
    }
    return false
  }
}

export const typescriptExecutor = new TypeScriptExecutor()
