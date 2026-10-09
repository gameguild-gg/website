import type { ProgrammingLanguage } from "@/components/block-content-editor/extras/source-code/types"
import { getFileContent } from "@/components/block-content-editor/extras/source-code/utils"
import { QuickJSRunner } from "../../../extras/code-studio/runners/quickjs-runner"
import type { ExecutionContext, ExecutionResult, LanguageExecutor } from "./types"

export class JavaScriptExecutor implements LanguageExecutor {
  public isCompiled = false
  private debugMode = false
  private runner: QuickJSRunner | null = null
  private cancelDialog: (() => void) | undefined

  constructor(private readonly fileExtension: "js" | "ts" = "js") {}

  private requestDialog(
    kind: "alert" | "prompt" | "confirm", message: string, defaultValue: string, context: ExecutionContext, output: string[],
  ): Promise<string | boolean | undefined> {
    if (this.cancelDialog) return Promise.reject(new Error("Another dialog is already waiting for input."))
    const line = kind.toUpperCase() + ": " + message + (kind === "prompt" && defaultValue ? " [default: " + defaultValue + "]" : "")
    output.push(line)
    context.addOutput(line)
    return new Promise(resolve => {
      let settled = false
      const finish = (value: string | boolean | undefined) => {
        if (settled) return
        settled = true
        this.cancelDialog = undefined
        if (kind === "alert") {
          window.__awaitingAlertAck = false
          window.__alertMessage = null
          window.alertCallback = () => {}
        } else if (kind === "prompt") {
          window.__awaitingPromptInput = false
          window.__promptMessage = null
          window.promptCallback = () => {}
        } else {
          window.__awaitingConfirmInput = false
          window.__confirmMessage = null
          window.confirmCallback = () => {}
        }
        resolve(value)
      }
      this.cancelDialog = () => finish(undefined)
      if (kind === "alert") {
        window.__awaitingAlertAck = true
        window.__alertMessage = message
        window.alertCallback = () => finish(undefined)
      } else if (kind === "prompt") {
        window.__awaitingPromptInput = true
        window.__promptMessage = message
        window.promptCallback = value => finish(value || defaultValue)
      } else {
        window.__awaitingConfirmInput = true
        window.__confirmMessage = message
        window.confirmCallback = value => finish(value === "0" || ["y", "yes", "true"].includes(value.toLowerCase()))
      }
    })
  }

  private createRunner(context: ExecutionContext, output: string[] = []): QuickJSRunner {
    return new QuickJSRunner({
      persistContext: true,
      onOutput: (text, stream) => {
        const line = stream === "stderr" ? "Error: " + text : text
        output.push(line)
        context.addOutput(line)
      },
      onDialog: (kind, message, defaultValue) => this.requestDialog(kind, message, defaultValue, context, output),
    })
  }

  execute = async (fileId: string, context: ExecutionContext): Promise<ExecutionResult> => {
    const { files, selectedLanguage, addOutput, setIsExecuting } = context
    this.runner?.dispose()
    this.cancelDialog?.()
    const output: string[] = []
    const runner = this.createRunner(context, output)
    this.runner = runner
    setIsExecuting(true)

    try {
      const visibleFiles = files.filter(file => file.isVisible)
      const mainFile = visibleFiles.find(file => file.isMain)
      const activeFile = files.find(file => file.id === fileId)
      const fileToExecute = mainFile || activeFile
      if (!fileToExecute) throw new Error("No file selected for execution.")
      if (mainFile && mainFile.id !== fileId) addOutput("Executing main file: " + fileToExecute.name)

      const virtualFileSystem: Record<string, string> = Object.create(null)
      for (const file of visibleFiles) virtualFileSystem[file.name] = getFileContent(file, selectedLanguage)
      const bundledCode = this.resolveImports(fileToExecute.name, virtualFileSystem, message => {
        if (this.debugMode) addOutput(message)
      })
      const code = bundledCode
        .replace(/alert\s*\(/g, "await customAlert(")
        .replace(/prompt\s*\(/g, "await customPrompt(")
        .replace(/confirm\s*\(/g, "await customConfirm(")
      const result = await runner.execute(code)
      return { success: result.exitCode === 0, output }
    } catch (error) {
      const message = "Error: " + (error instanceof Error ? error.message : String(error))
      addOutput(message)
      return { success: false, output: [...output, message] }
    } finally {
      if (this.runner === runner) {
        this.cancelDialog?.()
        setIsExecuting(false)
      }
    }
  }

  private isLineCommented(content: string, matchIndex: number): boolean {
    // Find the start of the line containing the match
    const beforeMatch = content.substring(0, matchIndex)
    const lastNewlineIndex = beforeMatch.lastIndexOf("\n")
    const lineStart = lastNewlineIndex === -1 ? 0 : lastNewlineIndex + 1

    // Get the content from line start to the match
    const lineBeforeMatch = content.substring(lineStart, matchIndex)

    // Check if the line starts with // (ignoring whitespace)
    return /^\s*\/\//.test(lineBeforeMatch)
  }

  private extractExports(content: string): {
    named: Set<string>
    hasDefaultExport: boolean
    defaultExportName: string | null
  } {
    const namedExports = new Set<string>()
    let hasDefaultExport = false
    let defaultExportName: string | null = null

    // Remove comments to avoid false positives
    const contentWithoutComments = content.replace(/\/\/.*$/gm, "").replace(/\/\*[\s\S]*?\*\//g, "")

    // Pattern 1: export { name1, name2 }
    const namedExportRegex = /export\s*{\s*([^}]+)\s*}/g
    let match
    while ((match = namedExportRegex.exec(contentWithoutComments)) !== null) {
      const exportList = (match[1] ?? '')
        .split(",")
        .map((name) => name.trim())
        .filter((name) => name.length > 0)
      exportList.forEach((name) => namedExports.add(name))
    }

    // Pattern 2: export function functionName()
    const exportFunctionRegex = /export\s+function\s+(\w+)/g
    while ((match = exportFunctionRegex.exec(contentWithoutComments)) !== null) {
      if (match[1]) namedExports.add(match[1])
    }

    // Pattern 3: export const variableName =
    const exportConstRegex = /export\s+const\s+(\w+)/g
    while ((match = exportConstRegex.exec(contentWithoutComments)) !== null) {
      if (match[1]) namedExports.add(match[1])
    }

    // Pattern 4: export let variableName =
    const exportLetRegex = /export\s+let\s+(\w+)/g
    while ((match = exportLetRegex.exec(contentWithoutComments)) !== null) {
      if (match[1]) namedExports.add(match[1])
    }

    // Pattern 5: export var variableName =
    const exportVarRegex = /export\s+var\s+(\w+)/g
    while ((match = exportVarRegex.exec(contentWithoutComments)) !== null) {
      if (match[1]) namedExports.add(match[1])
    }

    // Pattern 6: export default expression
    const exportDefaultRegex = /export\s+default\s+(\w+)/
    match = exportDefaultRegex.exec(contentWithoutComments)
    if (match) {
      hasDefaultExport = true
      defaultExportName = match[1] ?? null
    }

    // Pattern 7: export default function name() or export default class name
    const exportDefaultFunctionRegex = /export\s+default\s+(function|class)\s+(\w+)/
    match = exportDefaultFunctionRegex.exec(contentWithoutComments)
    if (match) {
      hasDefaultExport = true
      defaultExportName = match[2] ?? null
    }

    // Pattern 8: export default anonymous function or object
    const exportDefaultAnonRegex = /export\s+default\s+(function\s*\(|class\s*{|\{|\[)/
    if (exportDefaultAnonRegex.test(contentWithoutComments)) {
      hasDefaultExport = true
      defaultExportName = null // Anonymous export
    }

    return { named: namedExports, hasDefaultExport, defaultExportName }
  }

  private resolveImports(entryFile: string, vfs: Record<string, string>, debugLog: (msg: string) => void): string {
    const visited = new Set<string>()
    const importRegex = /import\s+(?:(\w+)|{([^}]+)}|\*\s+as\s+(\w+))?\s*from\s*['"](.+?)['"];?/g

    const resolveFile = (fileName: string): string => {
      if (visited.has(fileName)) {
        debugLog(`Circular import detected: ${fileName}`)
        return ""
      }
      visited.add(fileName)

      const content = vfs[fileName]
      if (!content) {
        debugLog(`Warning: File not found: ${fileName}`)
        return ""
      }

      let result = ""
      const imports = Array.from(content.matchAll(importRegex))

      // Filter out commented imports
      const activeImports = imports.filter((match) => {
        const isCommented = this.isLineCommented(content, match.index || 0)
        if (isCommented) {
          debugLog(`Skipping commented import: ${match[0].trim()}`)
        }
        return !isCommented
      })

      // Process active imports first
      for (const match of activeImports) {
        const [fullMatch, defaultImport, namedImports, namespaceImport, importPath] = match
        const resolvedPath = this.resolveRelativePath(fileName, importPath ?? '', vfs)

        if (resolvedPath) {
          debugLog(`Importing: ${importPath} -> ${resolvedPath}`)

          // Get the imported file content
          const importedContent = resolveFile(resolvedPath)
          const importedFileContent = vfs[resolvedPath] ?? ''

          // Extract exports from the imported file
          const {
            named: availableExports,
            hasDefaultExport,
            defaultExportName,
          } = this.extractExports(importedFileContent)

          debugLog(
            `Available exports from ${resolvedPath}: [${Array.from(availableExports).join(", ")}]${hasDefaultExport ? ` with default export${defaultExportName ? ` (${defaultExportName})` : ""}` : ""
            }`,
          )

          // Handle default import
          if (defaultImport) {
            if (!hasDefaultExport) {
              debugLog(`Error: No default export found in ${resolvedPath}`)
              continue
            }

            // If we know the name of the default export, use it directly
            if (defaultExportName) {
              result += `
// === Default import from ${resolvedPath} ===
(function() {
${importedContent}

// Export default to global scope
if (typeof ${defaultExportName} !== 'undefined') { 
  window.${defaultImport} = ${defaultExportName}; 
}
})();
`
            } else {
              // For anonymous default exports, we need to extract it differently
              result += `
// === Default import from ${resolvedPath} ===
(function() {
${importedContent}

// For anonymous default export, we need to capture it
// This is a simplified approach - in a real bundler this would be more sophisticated
window.${defaultImport} = (function() {
  // The default export should be the last expression in the file
  var exports = {};
  ${importedContent.replace(/export\s+default\s+/, "exports.default = ")}
  return exports.default;
})();
})();
`
            }
          }
          // Handle named imports
          else if (namedImports) {
            const requestedImports = namedImports.split(",").map((name) => name.trim())
            debugLog(`Requested imports: {${requestedImports.join(", ")}} from ${resolvedPath}`)

            // Check if all requested imports are available
            const unavailableImports = requestedImports.filter((name) => !availableExports.has(name))
            if (unavailableImports.length > 0) {
              debugLog(
                `Error: The following exports are not available in ${resolvedPath}: ${unavailableImports.join(", ")}`,
              )
              debugLog(`Available exports: ${Array.from(availableExports).join(", ") || "none"}`)
              continue // Skip this import
            }

            // Create a wrapper that exposes only the named exports
            result += `
// === Named imports from ${resolvedPath} ===
(function() {
${importedContent}

// Export only the explicitly exported and requested functions to global scope
${requestedImports.map((name) => `if (typeof ${name} !== 'undefined') { window.${name} = ${name}; }`).join("\n")}
})();
`
          }
          // Handle namespace imports (import * as name)
          else if (namespaceImport) {
            // Create a namespace object with all exports
            result += `
// === Namespace import from ${resolvedPath} ===
(function() {
${importedContent}

// Create namespace object
window.${namespaceImport} = {};

// Add all named exports to namespace
${Array.from(availableExports)
                .map((name) => `if (typeof ${name} !== 'undefined') { window.${namespaceImport}.${name} = ${name}; }`)
                .join("\n")}

// Add default export to namespace if available
${hasDefaultExport && defaultExportName
                ? `if (typeof ${defaultExportName} !== 'undefined') { window.${namespaceImport}.default = ${defaultExportName}; }`
                : ""
              }
})();
`
          } else {
            // Regular import - just include the file
            result += importedContent + "\n"
          }
        }
      }

      // Remove ALL import statements and export statements, then add the file content
      const contentWithoutImportsAndExports = content
        .replace(importRegex, "")
        .replace(/export\s*{\s*[^}]*\s*};?/g, "") // Remove export { ... }, including erased type-only modules
        .replace(/export\s+(function|const|let|var|class)\s+/g, "$1 ") // Remove export keyword from declarations
        .replace(/export\s+default\s+/g, "") // Remove export default

      result += contentWithoutImportsAndExports + "\n"

      return result
    }

    debugLog(`Starting bundle from: ${entryFile}`)
    return resolveFile(entryFile)
  }

  private resolveRelativePath(currentFile: string, importPath: string, vfs: Record<string, string>): string | null {
    // Handle relative imports starting with './'
    if (importPath.startsWith("./")) {
      const targetFile = importPath.substring(2)

      // Try with .js extension if not present
      if (vfs[targetFile]) {
        return targetFile
      }
      if (!targetFile.includes(".") && vfs[targetFile + "." + this.fileExtension]) {
        return targetFile + "." + this.fileExtension
      }
    }

    // Handle direct file names
    if (vfs[importPath]) {
      return importPath
    }

    // Try with .js extension
    if (!importPath.includes(".") && vfs[importPath + "." + this.fileExtension]) {
      return importPath + "." + this.fileExtension
    }

    return null
  }

  stop = () => {
    this.runner?.dispose()
    this.cancelDialog?.()
  }

  getFileExtension = (): string => {
    return "js"
  }

  getSupportedLanguages = (): ProgrammingLanguage[] => {
    return ["javascript"]
  }

  handleCommand = (command: string, context: ExecutionContext): boolean => {
    if (command.includes("console.log")) return false
    this.runner ??= this.createRunner(context)
    let errorReported = false
    void this.runner.evaluate(command, (text, stream) => {
      if (stream === "stderr") errorReported = true
      context.addOutput(stream === "stderr" ? "Error: " + text : text)
    })
      .then(result => {
        if (result.exitCode === 0) context.addOutput(result.value ?? "undefined")
        else if (!errorReported) context.addOutput("Error: " + (result.stderr || "JavaScript execution failed."))
      })
    return true
  }
}

// Export as named export
export const javascriptExecutor = new JavaScriptExecutor()
