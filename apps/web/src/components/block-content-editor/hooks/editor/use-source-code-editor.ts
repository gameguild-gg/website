"use client"

import type { EditMenuOption } from "@/components/block-content-editor/extras/content-edit-menu"
import type { CodeFile, ProgrammingLanguage, SourceCodeData, SourceCodeTestCases } from "@/components/block-content-editor/extras/source-code/types"
import type { editor } from "monaco-editor"
import { Pencil, Play, Settings } from "lucide-react"
import React, { useState } from "react"
import { useCodeExecution } from "./use-code-execution"

interface UseSourceCodeEditorProps {
  data: SourceCodeData & { hasConfiguredSettings?: boolean; activeEnvironments?: Record<string, boolean> }
  isEditing: boolean
  setIsEditing: (isEditing: boolean) => void
  files: CodeFile[]
  setFiles: (files: CodeFile[] | ((prev: CodeFile[]) => CodeFile[])) => void
  activeFileId: string
  setActiveFileId: (id: string) => void
  readonly: boolean
  setReadonly: (readonly: boolean) => void
  showExecution: boolean
  setShowExecution: (show: boolean) => void
  output: string[]
  setOutput: (output: string[] | ((prev: string[]) => string[])) => void
  isDarkTheme: boolean
  setIsDarkTheme: (dark: boolean) => void
  selectedLanguage: ProgrammingLanguage
  setSelectedLanguage: (lang: string) => void
  clearTerminalOnRun: boolean
  setClearTerminalOnRun: (clear: boolean) => void
  showBasicFileActionsInReadMode: boolean
  setShowBasicFileActionsInReadMode: (show: boolean) => void
  showFilePropertiesInReadMode: boolean
  setShowFilePropertiesInReadMode: (show: boolean) => void
  showSettings: boolean
  setShowSettings: (show: boolean) => void
  testCases: SourceCodeTestCases
  setTestResults: (results: Record<string, { passed: boolean; actual: string; expected: string }[]>) => void
  handleExecute: () => void
  updateSourceCode: (newData: Partial<SourceCodeData>) => void
  handleSave: () => void
}

export function useSourceCodeEditor({
  data,
  isEditing,
  setIsEditing,
  files,
  setFiles,
  activeFileId,
  setActiveFileId,
  readonly,
  setReadonly,
  showExecution,
  setShowExecution,
  isDarkTheme,
  setIsDarkTheme,
  selectedLanguage,
  setSelectedLanguage,
  clearTerminalOnRun,
  setClearTerminalOnRun,
  showBasicFileActionsInReadMode,
  setShowBasicFileActionsInReadMode,
  showFilePropertiesInReadMode,
  setShowFilePropertiesInReadMode,
  showSettings,
  setShowSettings,
  output,
  setOutput,
  testCases,
  setTestResults,
  handleExecute,
  updateSourceCode,
  handleSave,
}: UseSourceCodeEditorProps) {
  // State management
  const [isAutocompleteEnabled, setIsAutocompleteEnabled] = useState<boolean>(true)
  const [hasConfiguredSettings, setHasConfiguredSettings] = useState<boolean>(data.hasConfiguredSettings ?? false)
  const [activeEnvironments, setActiveEnvironments] = useState<Record<string, boolean>>(
    data.activeEnvironments ?? {
      javascript: false,
      web: false,
      typescript: false,
      python: false,
      lua: false,
      cpp: false,
      c: false,
    },
  )

  // Computed values
  const visibleFiles = isEditing ? files : files.filter((file) => file.isVisible)
  const activeFile = visibleFiles.find((file) => file.id === activeFileId) || visibleFiles[0] || files[0]
  const activeFileContent = activeFile?.content || ""

  // Edit menu options
  const editMenuOptions: EditMenuOption[] = [
    {
      id: "edit",
      icon: React.createElement(Pencil, { className: "h-4 w-4" }),
      label: "Edit Code",
      action: () => {
        // Reset to original data from the node when entering edit mode
        setFiles(
          data.files
            ? JSON.parse(JSON.stringify(data.files))
            : [
              {
                id: crypto.randomUUID(),
                name: "index.js",
                content: "",
                language: "javascript",
                isMain: true,
                isVisible: true,
              },
            ],
        )
        setActiveFileId(
          data.activeFileId ??
          data.files?.[0]?.id ??
          crypto.randomUUID()
        )
        setReadonly(data.readonly ?? false)
        setShowExecution(data.showExecution ?? false)
        setIsDarkTheme(data.isDarkTheme ?? false)
        setSelectedLanguage(data.selectedLanguage || "javascript")
        setClearTerminalOnRun(data.clearTerminalOnRun ?? false)
        setShowBasicFileActionsInReadMode(data.showBasicFileActionsInReadMode ?? true)
        setShowFilePropertiesInReadMode(data.showFilePropertiesInReadMode ?? false)
        setActiveEnvironments(
          data.activeEnvironments ?? {
            javascript: false,
            web: false,
            typescript: false,
            python: false,
            lua: false,
            cpp: false,
            c: false,
          },
        )
        setIsEditing(true)
      },
    },
    {
      id: "settings",
      icon: React.createElement(Settings, { className: "h-4 w-4" }),
      label: "Settings",
      action: () => setShowSettings(!showSettings),
    },
    {
      id: "execute",
      icon: React.createElement(Play, { className: "h-4 w-4" }),
      label: "Run Code",
      action: handleExecute,
    },
  ]

  // Terminal management
  const addOutput = (output: string | string[]) => {
    setOutput((prevOutput) =>
      Array.isArray(output)
        ? [...prevOutput, ...output]
        : [...prevOutput, output]
    )
  }

  const clearTerminal = () => {
    setOutput([])
  }

  // Code execution
  const {
    isExecuting: codeIsExecuting,
    setIsExecuting: setCodeIsExecuting,
    handleExecute: executeCode,
    handleStopExecution: stopCodeExecution,
    handleTerminalCommand,
  } = useCodeExecution({
    files,
    selectedLanguage,
    clearTerminalOnRun,
    addOutput,
    clearTerminal,
    setTestResults,
  })

  // Editor mount handler
  const handleEditorMount = (editor: editor.IStandaloneCodeEditor, monaco: typeof import("monaco-editor")) => {
    editor.focus()
    editor.updateOptions({
      theme: isDarkTheme ? "vs-dark" : "vs-light",
      fixedOverflowWidgets: true,
    })
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
      handleSave()
    })
  }

  return {
    // State
    isAutocompleteEnabled,
    setIsAutocompleteEnabled,
    hasConfiguredSettings,
    setHasConfiguredSettings,
    activeEnvironments,
    setActiveEnvironments,

    // Computed
    visibleFiles,
    activeFile,
    activeFileContent,
    editMenuOptions,

    // Code execution
    codeIsExecuting,
    setCodeIsExecuting,
    executeCode,
    stopCodeExecution,

    // Handlers
    handleEditorMount,
    addOutput,
    clearTerminal,
  }
}
