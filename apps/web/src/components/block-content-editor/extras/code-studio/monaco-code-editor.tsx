"use client"

import { useCallback, useEffect, useRef, useState } from "react"
import type { IDisposable, editor } from "monaco-editor"
import type { Monaco, OnMount } from "@monaco-editor/react"
import { useTheme } from "next-themes"
import type { SupportedLanguage } from "./types"
import { isShikiActive } from "@/components/block-content-editor/lib/shiki/highlighter"
import { BaseMonacoEditor } from "@/components/block-content-editor/lib/monaco"
import { registerPathCompletionProvider } from "./monaco-file-system"
import { LinkConfirmDialog } from "../dialogs/link-confirm-dialog"
import type { MonacoOptionsPreferences } from "@/components/block-content-editor/lib/storage/editor/editor-preferences"

let pathCompletionRegistered = false
let styleInjected = false

// Re-export so existing imports under `monaco-code-editor` keep working.
export { isShikiActive }

interface MonacoCodeEditorProps {
  value: string
  language: SupportedLanguage
  onChange?: (value: string) => void
  onCursorOffsetChange?: (offset: number) => void
  ariaLabel?: string
  readonly?: boolean
  /** Deprecated — kept for back-compat; theme is resolved via next-themes. */
  theme?: "vs-light" | "vs-dark"
  /**
   * Resolved Monaco-surface options. Either the global `editor` group
   * (editable surfaces) or the global `preview` group (read-only / base
   * display) — the caller decides which to pass based on the surface
   * role. `null` is tolerated during hydration.
   */
  options?: MonacoOptionsPreferences | null
  height?: string
  /** ID único do arquivo para garantir instâncias separadas. */
  fileId?: string
  /** Caminho do arquivo para o sistema de arquivos virtual. */
  filePath?: string
  /** ID da instância do Code Studio para isolamento completo. */
  instanceId?: string
}

/**
 * Code Studio's Monaco surface — thin wrapper on top of
 * {@link BaseMonacoEditor} that adds the things specific to this surface:
 *
 *  - TypeScript/JavaScript compiler options + diagnostics tweaks.
 *  - One-time registration of the in-memory path completion provider.
 *  - Ctrl/Cmd-click handling on URLs with a confirmation dialog.
 *  - Inline link decorations + the quick-input centering style.
 *  - External-value reconciliation (the editor runs in uncontrolled mode
 *    via `defaultValue` + `keepCurrentModel` to preserve per-file model
 *    state across remounts).
 */
export function MonacoCodeEditor({
  value,
  language,
  onChange,
  onCursorOffsetChange,
  ariaLabel = "Code editor",
  readonly = false,
  options,
  height = "100%",
  fileId,
  filePath,
  instanceId,
}: MonacoCodeEditorProps) {
  const editorRef = useRef<editor.IStandaloneCodeEditor | null>(null)
  const cursorListenerRef = useRef<IDisposable | null>(null)
  const isUserTypingRef = useRef(false)
  const lastValueRef = useRef(value)
  const [linkConfirmDialog, setLinkConfirmDialog] = useState<{ open: boolean; url: string }>({
    open: false,
    url: "",
  })

  const { resolvedTheme, theme: themeState } = useTheme()
  const isDarkMode = (resolvedTheme || themeState) === "dark"

  const handleBeforeMount = useCallback(async (monaco: Monaco) => {
    const ts = monaco?.languages?.typescript
    if (ts) {
      ts.typescriptDefaults.setCompilerOptions({
        target: ts.ScriptTarget.ES2020,
        allowNonTsExtensions: true,
        moduleResolution: ts.ModuleResolutionKind.NodeJs,
        module: ts.ModuleKind.ESNext,
        noEmit: true,
        esModuleInterop: true,
        jsx: ts.JsxEmit.React,
        allowJs: true,
        typeRoots: [],
        allowSyntheticDefaultImports: true,
        skipLibCheck: true,
        skipDefaultLibCheck: true,
      })

      ts.javascriptDefaults.setCompilerOptions({
        target: ts.ScriptTarget.ES2020,
        allowNonTsExtensions: true,
        moduleResolution: ts.ModuleResolutionKind.NodeJs,
        module: ts.ModuleKind.ESNext,
        noEmit: true,
        esModuleInterop: true,
        jsx: ts.JsxEmit.React,
        allowJs: true,
        allowSyntheticDefaultImports: true,
        skipLibCheck: true,
        skipDefaultLibCheck: true,
      })

      // Disable semantic validation so missing-module errors don't bubble
      // up in the in-memory file system.
      ts.typescriptDefaults.setDiagnosticsOptions({
        noSemanticValidation: true,
        noSyntaxValidation: false,
        diagnosticCodesToIgnore: [],
      })
      ts.javascriptDefaults.setDiagnosticsOptions({
        noSemanticValidation: true,
        noSyntaxValidation: false,
        diagnosticCodesToIgnore: [],
      })
    }

    if (!pathCompletionRegistered) {
      registerPathCompletionProvider(monaco)
      pathCompletionRegistered = true
    }
  }, [])

  const handleMount: OnMount = (ed) => {
    editorRef.current = ed
    const reportCursor = () => {
      const model = ed.getModel()
      const position = ed.getPosition()
      if (model && position) onCursorOffsetChange?.(model.getOffsetAt(position))
    }
    cursorListenerRef.current?.dispose()
    cursorListenerRef.current = ed.onDidChangeCursorPosition(reportCursor)
    reportCursor()

    // Ctrl/Cmd-click on URLs → confirmation dialog.
    ed.onMouseDown((e) => {
      if (!e.event.ctrlKey && !e.event.metaKey) return
      if (!e.target.position) return

      const model = ed.getModel()
      if (!model) return

      const lineContent = model.getLineContent(e.target.position.lineNumber)
      const urlRegex = /(https?:\/\/[^\s"')\]]+)/g
      let match
      while ((match = urlRegex.exec(lineContent)) !== null) {
        const startColumn = match.index + 1
        const endColumn = startColumn + match[0].length
        if (
          e.target.position.column >= startColumn &&
          e.target.position.column <= endColumn
        ) {
          e.event.preventDefault()
          e.event.stopPropagation()
          setLinkConfirmDialog({ open: true, url: match[0] })
          return
        }
      }
    })

    // Inline link decorations (kept in sync with model edits). We use
    // `createDecorationsCollection` instead of the deprecated
    // `deltaDecorations` so the decorations are tied to *this* editor's
    // lifetime: when the editor is disposed (e.g. file switch with
    // `keepCurrentModel=true`) the collection is automatically cleared
    // from the surviving model — otherwise stale decoration IDs leak
    // into the model and crash Monaco's renderer with
    // "this.domNode is undefined".
    const linkDecorations = ed.createDecorationsCollection()
    const updateLinkDecorations = () => {
      const model = ed.getModel()
      if (!model) return
      const decorations: editor.IModelDeltaDecoration[] = []
      const urlRegex = /(https?:\/\/[^\s"')\]]+)/g
      for (let lineNumber = 1; lineNumber <= model.getLineCount(); lineNumber++) {
        const lineContent = model.getLineContent(lineNumber)
        let match
        while ((match = urlRegex.exec(lineContent)) !== null) {
          decorations.push({
            range: {
              startLineNumber: lineNumber,
              startColumn: match.index + 1,
              endLineNumber: lineNumber,
              endColumn: match.index + match[0].length + 1,
            },
            options: {
              inlineClassName: "monaco-link-decoration",
              hoverMessage: { value: `Ctrl+Clique para abrir: ${match[0]}` },
            },
          })
        }
      }
      linkDecorations.set(decorations)
    }
    updateLinkDecorations()
    ed.onDidChangeModelContent(() => updateLinkDecorations())

    // Inject the quick-input centering + link-decoration style once per
    // document — these styles apply globally to any Monaco container.
    if (!styleInjected && typeof document !== "undefined") {
      const style = document.createElement("style")
      style.textContent = `
        .monaco-editor .quick-input-widget {
          position: fixed !important;
          left: 50% !important;
          top: 50% !important;
          transform: translate(-50%, -50%) !important;
          z-index: 9999 !important;
          max-width: 600px !important;
        }
        .monaco-editor .monaco-workbench .part.editor > .content .monaco-editor .quick-input-widget {
          position: absolute !important;
        }
        .monaco-link-decoration {
          text-decoration: underline !important;
          color: #0066cc !important;
          cursor: pointer !important;
        }
        .monaco-editor.vs-dark .monaco-link-decoration {
          color: #3794ff !important;
        }
      `
      document.head.appendChild(style)
      styleInjected = true
    }
  }

  useEffect(() => () => cursorListenerRef.current?.dispose(), [])

  const handleChange = (next: string | undefined) => {
    isUserTypingRef.current = true
    if (onChange && !readonly) {
      onChange(next || "")
    }
  }

  // External value reconciliation: the editor is uncontrolled
  // (`defaultValue` + `keepCurrentModel`) so we manually push remote
  // value changes (e.g. file switches, undo on the parent) while
  // preserving cursor position.
  useEffect(() => {
    if (editorRef.current && !isUserTypingRef.current && value !== lastValueRef.current) {
      const ed = editorRef.current
      const model = ed.getModel()
      if (model) {
        const currentValue = model.getValue()
        if (currentValue !== value) {
          const position = ed.getPosition()
          model.setValue(value)
          if (position) ed.setPosition(position)
        }
      }
      lastValueRef.current = value
    }
    if (isUserTypingRef.current) {
      const timeout = setTimeout(() => {
        isUserTypingRef.current = false
      }, 100)
      return () => clearTimeout(timeout)
    }
  }, [value])

  const path = filePath && instanceId
    ? `file:///${instanceId}/${filePath}`
    : filePath
      ? `file:///${filePath}`
      : undefined

  return (
    <>
      <LinkConfirmDialog
        open={linkConfirmDialog.open}
        onOpenChange={(open) => setLinkConfirmDialog({ open, url: "" })}
        url={linkConfirmDialog.url}
        onConfirm={() => {
          window.open(linkConfirmDialog.url, "_blank", "noopener,noreferrer")
          setLinkConfirmDialog({ open: false, url: "" })
        }}
      />

      <BaseMonacoEditor
        editorKey={fileId}
        height={height}
        language={language}
        defaultValue={value}
        path={path}
        keepCurrentModel
        readOnly={readonly}
        isDark={isDarkMode}
        options={options}
        onChange={handleChange}
        beforeMount={handleBeforeMount}
        onMount={handleMount}
        extraOptions={{
          ariaLabel,
          padding: { top: 8, bottom: 8 },
          suggest: { showKeywords: true, showSnippets: true },
          quickSuggestions: { other: true, comments: false, strings: false },
          // We render our own link decorations + Ctrl/Cmd-click handler
          // above, so disable Monaco's built-in URL link rendering.
          links: false,
        }}
      />
    </>
  )
}
