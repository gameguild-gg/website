import type React from "react"
import type { editor } from "monaco-editor"
export type LanguageType =
  | "javascript"
  | "typescript"
  | "python"
  | "lua"
  | "c"
  | "cpp"
  | "h"
  | "hpp"
  | "cheader"
  | "cppheader"
  | "xml"
  | "yaml"
  | "json"
  | "html"
  | "css"
  | "markdown"
  | "text"

export type ProgrammingLanguage = "javascript" | "typescript" | "python" | "lua" | "c" | "cpp" | "h" | "hpp"

export interface CodeFile {
  id: string
  name: string
  content: string
  language: LanguageType
  isMain: boolean
  isVisible: boolean
  readOnlyState?: "always" | "never" | "hidden" | null
  languageContent?: Record<string, string>
}

export interface SourceCodeData {
  files?: CodeFile[]
  activeFileId?: string
  readonly?: boolean
  showExecution?: boolean
  isDarkTheme?: boolean
  selectedLanguage?: ProgrammingLanguage
  clearTerminalOnRun?: boolean
  showBasicFileActionsInReadMode?: boolean
  showFilePropertiesInReadMode?: boolean
}

// JSON-serializable value types for test cases
export type JsonValue = string | number | boolean | null | { [key: string]: JsonValue } | JsonValue[]
export type JsonArray = JsonValue[]

// Update the TerminalProps interface to include the new predicate test type
export interface TerminalProps {
  terminalOutput: {
    id: string
    type: "input" | "output" | "error" | "system"
    content: string | string[]
  }[]
  terminalInput: string
  setTerminalInput: (input: string) => void
  handleTerminalSubmit: () => void
  handleClearTerminal: () => void
  isExecuting: boolean
  handleStopExecution: () => void
  isDarkTheme: boolean
  showTests: boolean
  testCases: Record<
    string,
    {
      type: "custom" | "function" | "console"
      input?: string
      expectedOutput?: string
      args?: JsonArray
      expectedReturn?: JsonArray
      predicate?: string
      customCode?: string
      customCodeFirst?: string | Record<ProgrammingLanguage, string>
      customCodeSecond?: string | Record<ProgrammingLanguage, string>
    }[]
  >
  setTestCases: (
    cases: Record<
      string,
      {
        type: "custom" | "function" | "console"
        input?: string
        expectedOutput?: string
        args?: JsonArray
        expectedReturn?: JsonArray
        predicate?: string
        customCode?: string
        customCodeFirst?: string | Record<ProgrammingLanguage, string>
        customCodeSecond?: string | Record<ProgrammingLanguage, string>
      }[]
    >,
  ) => void
  testResults: Record<string, { passed: boolean; actual: string; expected: string }[]>
  activeFileId: string
  activeTab: "terminal" | "tests"
  setActiveTab: (tab: "terminal" | "tests") => void
  runTests: (fileId: string) => void
}

export interface CodeEditorProps {
  codeEditorHeight: number
  activeFileLanguage: string
  activeFileContent: string
  isDarkTheme: boolean
  readonly: boolean
  isEditing: boolean
  updateActiveFileContent: (content: string) => void
  handleCodeEditorResize: (e: React.MouseEvent, startY: number) => void
  onEditorMount?: (editor: editor.IStandaloneCodeEditor, monaco: typeof import("monaco-editor")) => void
  isAutocompleteEnabled?: boolean
}

export interface LanguageSettingsDialogProps {
  showLanguagesDialog: boolean
  setShowLanguagesDialog: (show: boolean) => void
  allowedLanguages: Record<string, boolean>
  setAllowedLanguages: (value: Record<string, boolean> | ((prev: Record<string, boolean>) => Record<string, boolean>)) => void
  selectedLanguage: ProgrammingLanguage
  setSelectedLanguage: (lang: ProgrammingLanguage | string) => void
  getLanguageLabel: (lang: LanguageType | string) => string
  updateSourceCode?: (data: Partial<SourceCodeData>) => void
  isAutocompleteEnabled?: boolean
  setIsAutocompleteEnabled?: (enabled: boolean) => void
}

export interface FileTabsProps {
  files: CodeFile[]
  activeFileId: string
  setActiveFileId?: (id: string) => void
  isDarkTheme?: boolean
  setIsDarkTheme?: (dark: boolean) => void
  isEditing?: boolean
  showFileDialog?: () => void
  showRenameDialog?: (fileId?: string) => void
  showImportDialog?: () => void
  showConfirmDialog?: () => void
  showLanguagesDialog?: () => void
  toggleFileVisibility?: (fileId: string) => void
  setMainFile?: (fileId: string) => void
  deleteFile?: (fileId: string) => void
  getBaseName?: (name: string) => string
  getExtensionForSelectedLanguage?: () => string
  draggedFileId?: string | null
  setDraggedFileId?: (id: string | null) => void
  dragOverFileId?: string | null
  setDragOverFileId?: (id: string | null) => void
  reorderFiles?: (draggedId: string, targetId: string) => void
  getFileIcon?: (file: CodeFile) => React.ReactNode
  getStateIcon?: (file: CodeFile) => React.ReactNode
  setFileReadOnlyState?: (fileId: string, state: "always" | "never" | "hidden" | null) => void
  showBasicFileActionsInReadMode?: boolean
  showFilePropertiesInReadMode?: boolean
  setShowBasicFileActionsInReadMode?: (show: boolean) => void
  setShowFilePropertiesInReadMode?: (show: boolean) => void
  showDeleteConfirmDialog?: boolean
  setShowDeleteConfirmDialog?: (show: boolean) => void
  // Legacy support
  onFileSelect?: (fileId: string) => void
  onFileClose?: (fileId: string) => void
  onFileRename?: (fileId: string, newName: string) => void
  readonly?: boolean
  selectedLanguage?: ProgrammingLanguage
  setSelectedLanguage?: (lang: ProgrammingLanguage | string) => void
  getAllowedProgrammingLanguages?: () => ProgrammingLanguage[]
  getLanguageLabel?: (lang: string) => string
  isFileReadOnly?: (file: CodeFile) => boolean
}

export interface NewFileDialogProps {
  showFileDialog: boolean
  setShowFileDialog: (show: boolean) => void
  newFileName: string
  setNewFileName: (name: string) => void
  newFileLanguage: LanguageType
  setNewFileLanguage: (lang: LanguageType) => void
  newFileHasStates?: boolean
  setNewFileHasStates?: (hasStates: boolean) => void
  addNewFile: () => void
  getAllowedLanguageTypes: () => LanguageType[]
  getLanguageLabel: (lang: LanguageType) => string
  isPreview?: boolean
}

export interface RenameFileDialogProps {
  showRenameDialog: boolean
  setShowRenameDialog: (show: boolean) => void
  renameFileName: string
  setRenameFileName: (name: string) => void
  renameFileLanguage: LanguageType
  setRenameFileLanguage: (lang: LanguageType) => void
  fileToRename: string | null
  renameFile: () => void
  files: CodeFile[]
  getAllowedLanguageTypes: () => LanguageType[]
  getLanguageLabel: (lang: LanguageType | string) => string
  isPreview?: boolean
}

export interface ImportFileDialogProps {
  showImportDialog: boolean
  setShowImportDialog: (show: boolean) => void
  importFileNames: string[]
  importContents: { name: string; content: string }[]
  handleFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  importFile: () => void
  fileInputRef: React.RefObject<HTMLInputElement | null>
  isPreview?: boolean
  importFileHasStates?: boolean
  setImportFileHasStates?: (hasStates: boolean) => void
}

export interface ConfirmDialogProps {
  showConfirmDialog: boolean
  setShowConfirmDialog: (show: boolean) => void
  activeFileId: string
  isPreview?: boolean
  toggleFileStates: () => void
}

export interface LanguageConfig {
  name: string
  extensions: string[]
  monacoLanguage: string
  highlightLanguage: string
  supportsExecution?: boolean
  defaultTemplate?: string
}
