"use client"

import { useEffect } from "react"
import type { editor } from "monaco-editor"
import { CppSyntaxHighlighter } from "./cpp-syntax-highlighter"
import { CppTypeChecker } from "./cpp-type-checker"
import { CppCompletionProvider } from "./cpp-completion-provider"

interface CppLanguageServiceProps {
  monaco: typeof import("monaco-editor")
  editor: editor.IStandaloneCodeEditor
  code: string
  enabled: boolean
}

export function CppLanguageService({ monaco, editor, code, enabled }: CppLanguageServiceProps) {
  useEffect(() => {
    if (!enabled) return

    console.log("C++ language service enabled")

    // Initialize all C++ language features
    const syntaxHighlighter = new CppSyntaxHighlighter(monaco)
    const typeChecker = new CppTypeChecker(monaco, editor)
    const completionProvider = new CppCompletionProvider(monaco)

    // Register the language features
    syntaxHighlighter.register()
    typeChecker.register(code)
    const completionDisposable = completionProvider.register()

    return () => {
      // Clean up when component unmounts or language changes
      syntaxHighlighter.dispose()
      typeChecker.dispose()
      completionDisposable.dispose()
      console.log("C++ language service disabled")
    }
  }, [monaco, editor, code, enabled])

  return null
}
