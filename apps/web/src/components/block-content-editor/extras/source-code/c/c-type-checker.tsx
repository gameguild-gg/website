"use client"

import type { editor } from "monaco-editor"
import { useEffect, useState } from "react"

interface CTypeCheckerProps {
  monaco: typeof import("monaco-editor") | null
  editor: editor.IStandaloneCodeEditor | null
  code: string
}

interface TypeError {
  line: number
  column: number
  message: string
  severity: "error" | "warning" | "info"
}

export function CTypeChecker({ monaco, editor, code }: CTypeCheckerProps) {
  const [errors, setErrors] = useState<TypeError[]>([])

  useEffect(() => {
    if (!monaco || !editor) return

    // Create a marker collection for C type errors
    const model = editor.getModel()
    if (!model) return

    // Simple C type checking
    const cTypeCheck = (code: string): TypeError[] => {
      const errors: TypeError[] = []
      const lines = code.split("\n")

      // Track variable declarations and their types
      const variables: Record<string, string> = {}

      // Track function declarations and their return types
      const functions: Record<string, string> = {}

      // Track if we're inside a function, struct, or enum
      let inFunction = false
      let inStruct = false
      let inEnum = false

      // Track braces for block scope
      let braceCount = 0

      // Track preprocessor #if/#ifdef/#ifndef state
      let inPreprocessorIf = false

      lines.forEach((line, lineIndex) => {
        // Skip comments
        if (line.trim().startsWith("//") || line.trim().startsWith("/*")) {
          return
        }

        // Check for missing semicolons
        if (
          !line.trim().endsWith(";") &&
          !line.trim().endsWith("{") &&
          !line.trim().endsWith("}") &&
          !line.trim().startsWith("#") &&
          line.trim() !== "" &&
          !line.trim().endsWith(":") && // For labels
          !line.includes("//") && // Line with comments
          !inPreprocessorIf
        ) {
          // Check if it's not a function declaration or control structure
          if (
            !line.includes("if") &&
            !line.includes("for") &&
            !line.includes("while") &&
            !line.includes("switch") &&
            !line.includes("else") &&
            !line.includes("do")
          ) {
            errors.push({
              line: lineIndex + 1,
              column: line.length,
              message: "Missing semicolon",
              severity: "warning",
            })
          }
        }

        // Check for preprocessor directives
        if (line.trim().startsWith("#")) {
          const directive = line.trim().substring(1).trim().split(" ")[0]

          if (directive === "if" || directive === "ifdef" || directive === "ifndef") {
            inPreprocessorIf = true
          } else if (directive === "endif") {
            inPreprocessorIf = false
          }

          // Check for common preprocessor errors
          if (directive === "include") {
            const includePattern = /#include\s+[<"]([^>"]+)[>"]/
            const match = line.match(includePattern)

            if (!match) {
              errors.push({
                line: lineIndex + 1,
                column: line.indexOf("include") + 7,
                message: "Invalid #include directive",
                severity: "error",
              })
            }
          }

          return
        }

        // Track braces
        const openBraces = (line.match(/{/g) || []).length
        const closeBraces = (line.match(/}/g) ?? []).length
        braceCount += openBraces - closeBraces

        // Check for variable declarations
        const varDeclarationRegex =
          /\b(int|char|float|double|void|long|short|unsigned|signed|struct|enum|union|bool)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*(\[\s*[a-zA-Z0-9_]*\s*\])?\s*(=|;|\(|,)/
        const varMatch = line.match(varDeclarationRegex)

        if (varMatch && !line.includes("(")) {
          const varType = varMatch[1] ?? ''
          const varName = varMatch[2]
          if (varName) {
            variables[varName] = varType
          }
        }

        // Check for function declarations
        const funcDeclarationRegex =
          /\b(int|char|float|double|void|long|short|unsigned|signed|struct|enum|union|bool)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\(/
        const funcMatch = line.match(funcDeclarationRegex)

        if (funcMatch) {
          const returnType = funcMatch[1] ?? ''
          const funcName = funcMatch[2]
          if (funcName) {
            functions[funcName] = returnType
          }

          if (line.includes("{")) {
            inFunction = true
          }
        }

        // Check for struct declarations
        if (line.includes("struct") && line.includes("{")) {
          inStruct = true
        }

        // Check for enum declarations
        if (line.includes("enum") && line.includes("{")) {
          inEnum = true
        }

        // Check for closing braces that end functions, structs, or enums
        if (line.includes("}")) {
          if (inFunction && braceCount === 0) {
            inFunction = false
          }
          if (inStruct && braceCount === 0) {
            inStruct = false
          }
          if (inEnum && braceCount === 0) {
            inEnum = false
          }
        }

        // Check for common C errors

        // 1. Using = instead of == in conditions
        if (
          (line.includes("if") || line.includes("while")) &&
          line.includes("(") &&
          !line.includes("==") &&
          line.includes("=")
        ) {
          errors.push({
            line: lineIndex + 1,
            column: line.indexOf("="),
            message: "Assignment in condition. Did you mean == ?",
            severity: "warning",
          })
        }

        // 2. Missing return statement in non-void functions
        if (inFunction && line.includes("}") && braceCount === 0) {
          // Check if the function has a return type other than void
          const funcName = Object.keys(functions).find((name) => functions[name] !== "void")
          if (funcName && !code.includes("return")) {
            errors.push({
              line: lineIndex + 1,
              column: 0,
              message: `Function '${funcName}' has non-void return type but no return statement`,
              severity: "warning",
            })
          }
        }

        // 3. Potential null pointer dereference
        if (line.includes("->") || line.includes("*")) {
          const pointerRegex = /([a-zA-Z_][a-zA-Z0-9_]*)\s*->/
          const pointerMatch = line.match(pointerRegex)

          if (pointerMatch) {
            const pointerName = pointerMatch[1]
            if (line.includes(`${pointerName} = NULL`) || line.includes(`${pointerName} = 0`)) {
              errors.push({
                line: lineIndex + 1,
                column: line.indexOf("->"),
                message: "Potential null pointer dereference",
                severity: "warning",
              })
            }
          }
        }

        // 4. Array index out of bounds (simple cases)
        const arrayAccessRegex = /([a-zA-Z_][a-zA-Z0-9_]*)\[([^\]]+)\]/g
        let arrayMatch

        while ((arrayMatch = arrayAccessRegex.exec(line)) !== null) {
          const arrayName = arrayMatch[1] ?? ''
          const indexExpr = arrayMatch[2] ?? ''

          // Check for negative indices
          if (indexExpr.includes("-") || indexExpr === "0-1") {
            errors.push({
              line: lineIndex + 1,
              column: arrayMatch.index + arrayName.length + 1,
              message: "Potential negative array index",
              severity: "warning",
            })
          }

          // Check for constant indices that might be out of bounds
          if (/^\d+$/.test(indexExpr)) {
            const index = Number.parseInt(indexExpr, 10)
            if (index < 0) {
              errors.push({
                line: lineIndex + 1,
                column: arrayMatch.index + arrayName.length + 1,
                message: "Negative array index",
                severity: "error",
              })
            }
          }
        }
      })

      // Check for unbalanced braces
      if (braceCount !== 0) {
        const lastLine = lines[lines.length - 1] ?? ""
        errors.push({
          line: lines.length,
          column: lastLine.length,
          message: braceCount > 0 ? "Missing closing brace(s)" : "Extra closing brace(s)",
          severity: "error",
        })
      }

      return errors
    }

    // Run type checking
    const typeErrors = cTypeCheck(code)
    setErrors(typeErrors)

    // Set markers in the editor
    const markers = typeErrors.map((error) => ({
      startLineNumber: error.line,
      startColumn: error.column,
      endLineNumber: error.line,
      endColumn: error.column + 1,
      message: error.message,
      severity:
        error.severity === "error"
          ? monaco.MarkerSeverity.Error
          : error.severity === "warning"
            ? monaco.MarkerSeverity.Warning
            : monaco.MarkerSeverity.Info,
    }))

    monaco.editor.setModelMarkers(model, "c-type-checker", markers)

    return () => {
      // Clear markers when component unmounts
      if (model) {
        monaco.editor.setModelMarkers(model, "c-type-checker", [])
      }
    }
  }, [monaco, editor, code])

  return null
}
