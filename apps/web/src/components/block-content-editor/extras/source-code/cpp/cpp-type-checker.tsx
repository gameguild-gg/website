import type { editor, IDisposable } from "monaco-editor"

export class CppTypeChecker {
  private monaco: typeof import("monaco-editor") | null
  private editor: editor.IStandaloneCodeEditor | null
  private disposables: IDisposable[] = []

  constructor(monaco: typeof import("monaco-editor") | null, editor: editor.IStandaloneCodeEditor | null) {
    this.monaco = monaco
    this.editor = editor
  }

  register(code: string) {
    // Simple C++ type checking and error detection
    setTimeout(() => {
      const { monaco, editor: editorInstance } = this
      if (!monaco || !editorInstance) return
      const model = editorInstance.getModel()
      if (!model) return

      const markers: editor.IMarkerData[] = []

      // Simple error detection for demonstration purposes
      // In a real implementation, this would use a proper C++ parser

      // Check for missing semicolons
      const lines = code.split("\n")
      lines.forEach((line, lineIndex) => {
        // Skip comments and preprocessor directives
        if (line.trim().startsWith("//") || line.trim().startsWith("#")) return

        // Check for unclosed brackets, braces, and parentheses
        const openBraces = (line.match(/\{/g) || []).length
        const closeBraces = (line.match(/\}/g) || []).length
        const openParens = (line.match(/\(/g) || []).length
        const closeParens = (line.match(/\)/g) || []).length
        const openBrackets = (line.match(/\[/g) || []).length
        const closeBrackets = (line.match(/\]/g) || []).length

        // Check for statements that should end with semicolons
        // This is a simplified check and will have false positives/negatives
        if (
          !line.trim().endsWith(";") &&
          !line.trim().endsWith("{") &&
          !line.trim().endsWith("}") &&
          !line.trim().endsWith(":") &&
          !line.trim().endsWith("\\") &&
          line.trim().length > 0 &&
          !line.trim().match(/^\s*#/) &&
          !line.trim().match(/^\s*\/\//) &&
          !line.includes("class") &&
          !line.includes("struct") &&
          !line.includes("namespace") &&
          !line.includes("if") &&
          !line.includes("else") &&
          !line.includes("for") &&
          !line.includes("while") &&
          !line.includes("switch") &&
          !line.includes("case")
        ) {
          // Check next line for opening brace to avoid flagging function/class declarations
          const nextLineRaw = lineIndex < lines.length - 1 ? lines[lineIndex + 1] : ""
          const nextLine = nextLineRaw?.trim() ?? ""
          if (!nextLine.startsWith("{")) {
            markers.push({
              severity: monaco.MarkerSeverity.Warning,
              message: "Statement might be missing a semicolon",
              startLineNumber: lineIndex + 1,
              startColumn: line.length + 1,
              endLineNumber: lineIndex + 1,
              endColumn: line.length + 2,
            })
          }
        }

        // Check for unbalanced brackets in the line
        if (openBraces !== closeBraces) {
          markers.push({
            severity: monaco.MarkerSeverity.Error,
            message: "Unbalanced braces in this line",
            startLineNumber: lineIndex + 1,
            startColumn: 1,
            endLineNumber: lineIndex + 1,
            endColumn: line.length + 1,
          })
        }

        if (openParens !== closeParens) {
          markers.push({
            severity: monaco.MarkerSeverity.Error,
            message: "Unbalanced parentheses in this line",
            startLineNumber: lineIndex + 1,
            startColumn: 1,
            endLineNumber: lineIndex + 1,
            endColumn: line.length + 1,
          })
        }

        if (openBrackets !== closeBrackets) {
          markers.push({
            severity: monaco.MarkerSeverity.Error,
            message: "Unbalanced brackets in this line",
            startLineNumber: lineIndex + 1,
            startColumn: 1,
            endLineNumber: lineIndex + 1,
            endColumn: line.length + 1,
          })
        }
      })

      // Set the markers on the model
      monaco.editor.setModelMarkers(model, "cpp", markers)
    }, 100)
  }

  dispose() {
    // Clean up any disposables
    this.disposables.forEach((disposable) => disposable.dispose())

    // Clear markers when disposing
    const { monaco, editor: editorInstance } = this
    if (monaco && editorInstance) {
      const model = editorInstance.getModel()
      if (model) {
        monaco.editor.setModelMarkers(model, "cpp", [])
      }
    }
  }
}
