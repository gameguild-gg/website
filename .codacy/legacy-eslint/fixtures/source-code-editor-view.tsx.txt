"use client"

import { cn } from "@/lib/utils"
import { GripVertical } from "lucide-react"
import type React from "react"
import { ContentEditMenu, type EditMenuOption } from "../../content-edit-menu"
import { CodeEditor } from "../code-editor"
import { FileTabs } from "../file-tabs"
import { SettingsPanel } from "../settings-panel"
import { Terminal } from "../terminal"
import type { CodeFile, ProgrammingLanguage } from "../types"

interface SourceCodeEditorViewProps {
  // File management
  files: CodeFile[]
  activeFileId: string
  setActiveFileId: (id: string) => void
  activeFile?: CodeFile
  activeFileContent: string

  // UI state
  isDarkTheme: boolean
  setIsDarkTheme: (dark: boolean) => void
  selectedLanguage: ProgrammingLanguage
  setSelectedLanguage: (lang: ProgrammingLanguage | string) => void
  readonly: boolean
  showExecution: boolean
  showSettings: boolean
  isPreview: boolean

  // Layout
  codeEditorHeight: number
  terminalHeight: number

  // Handlers
  handleCodeEditorResize: (e: React.MouseEvent, initialY: number) => void
  handleTerminalResize: (e: React.MouseEvent, initialY: number) => void
  handleEditorMount: (editor: any, monaco: any) => void
  updateActiveFileContent: (content: string) => void
  isFileReadOnly: (file: CodeFile, readonly: boolean) => boolean

  // File operations
  toggleFileVisibility: (fileId: string) => void
  setMainFile: (fileId: string) => void
  deleteFile: (fileId: string) => void
  setFileReadOnlyState: (fileId: string, state: "always" | "never" | "hidden" | null) => void
  reorderFiles: (fileId: string, targetFileId: string) => void

  // Dialog handlers
  showFileDialog: () => void
  showRenameDialog: (fileId?: string) => void
  showImportDialog: () => void
  showConfirmDialog: () => void
  showLanguagesDialog: () => void

  // Utility functions
  getBaseName: (name: string) => string
  getExtensionForSelectedLanguage: () => string
  getAllowedProgrammingLanguages: () => ProgrammingLanguage[]
  getLanguageLabel: (lang: any) => string
  getFileIcon: (file: CodeFile) => any
  getStateIcon: (file: CodeFile) => any

  // Drag and drop
  draggedFileId: string | null
  setDraggedFileId: (id: string | null) => void
  dragOverFileId: string | null
  setDragOverFileId: (id: string | null) => void

  // Settings
  showBasicFileActionsInReadMode: boolean
  showFilePropertiesInReadMode: boolean
  setShowBasicFileActionsInReadMode: (show: boolean) => void
  setShowFilePropertiesInReadMode: (show: boolean) => void

  // Terminal props
  isExecuting: boolean
  output: string[]
  input: string
  setInput: (input: string) => void
  waitingForInput: boolean
  inputPrompt: string
  handleTerminalInput: (e: React.KeyboardEvent<HTMLInputElement>) => void
  handleExecute: () => void
  handleStopExecution: () => void
  clearTerminalOnRun: boolean
  setClearTerminalOnRun: (clear: boolean) => void
  updateSourceCode: (data: any) => void
  terminalInputRef: React.RefObject<HTMLInputElement | null>
  activeTab: "terminal" | "tests"
  setActiveTab: (tab: "terminal" | "tests") => void
  showTests: boolean
  testCases: any
  addTestCase: any
  removeTestCase: any
  updateTestCase: any
  testResults: any
  runTests: any
  addSolutionTemplate?: () => void
  addCustomTestFiles?: (testIndex: number) => void

  // Edit menu
  editMenuOptions: EditMenuOption[]
  isAutocompleteEnabled: boolean
}

export function SourceCodeEditorView({
  files,
  activeFileId,
  setActiveFileId,
  activeFile,
  activeFileContent,
  isDarkTheme,
  setIsDarkTheme,
  selectedLanguage,
  setSelectedLanguage,
  readonly,
  showExecution,
  showSettings,
  isPreview,
  codeEditorHeight,
  terminalHeight,
  handleCodeEditorResize,
  handleTerminalResize,
  handleEditorMount,
  updateActiveFileContent,
  isFileReadOnly,
  toggleFileVisibility,
  setMainFile,
  deleteFile,
  setFileReadOnlyState,
  reorderFiles,
  showFileDialog,
  showRenameDialog,
  showImportDialog,
  showConfirmDialog,
  showLanguagesDialog,
  getBaseName,
  getExtensionForSelectedLanguage,
  getAllowedProgrammingLanguages,
  getLanguageLabel,
  getFileIcon,
  getStateIcon,
  draggedFileId,
  setDraggedFileId,
  dragOverFileId,
  setDragOverFileId,
  showBasicFileActionsInReadMode,
  showFilePropertiesInReadMode,
  setShowBasicFileActionsInReadMode,
  setShowFilePropertiesInReadMode,
  isExecuting,
  output,
  input,
  setInput,
  waitingForInput,
  inputPrompt,
  handleTerminalInput,
  handleExecute,
  handleStopExecution,
  clearTerminalOnRun,
  setClearTerminalOnRun,
  updateSourceCode,
  terminalInputRef,
  activeTab,
  setActiveTab,
  showTests,
  testCases,
  addTestCase,
  removeTestCase,
  updateTestCase,
  testResults,
  runTests,
  addSolutionTemplate,
  addCustomTestFiles,
  editMenuOptions,
  isAutocompleteEnabled,
}: SourceCodeEditorViewProps) {
  return (
    <div className="rounded-lg border overflow-hidden">
      {/* File tabs */}
      <FileTabs
        files={files}
        activeFileId={activeFileId}
        setActiveFileId={setActiveFileId}
        isDarkTheme={isDarkTheme}
        setIsDarkTheme={setIsDarkTheme}
        selectedLanguage={selectedLanguage}
        setSelectedLanguage={setSelectedLanguage}
        isEditing={false}
        showFileDialog={showFileDialog}
        showRenameDialog={showRenameDialog}
        showImportDialog={showImportDialog}
        showConfirmDialog={showConfirmDialog}
        toggleFileVisibility={toggleFileVisibility}
        setMainFile={setMainFile}
        deleteFile={deleteFile}
        getBaseName={getBaseName}
        getExtensionForSelectedLanguage={getExtensionForSelectedLanguage}
        getAllowedProgrammingLanguages={getAllowedProgrammingLanguages}
        getLanguageLabel={getLanguageLabel}
        showLanguagesDialog={showLanguagesDialog}
        draggedFileId={draggedFileId}
        setDraggedFileId={setDraggedFileId}
        dragOverFileId={dragOverFileId}
        setDragOverFileId={setDragOverFileId}
        reorderFiles={reorderFiles}
        getFileIcon={getFileIcon}
        getStateIcon={getStateIcon}
        setFileReadOnlyState={setFileReadOnlyState}
        showBasicFileActionsInReadMode={showBasicFileActionsInReadMode}
        showFilePropertiesInReadMode={showFilePropertiesInReadMode}
        setShowBasicFileActionsInReadMode={setShowBasicFileActionsInReadMode}
        setShowFilePropertiesInReadMode={setShowFilePropertiesInReadMode}
        isFileReadOnly={(file: CodeFile) => isFileReadOnly(file, readonly)}
      />

      {/* Code editor */}
      <div
        className={cn("bg-background font-mono text-sm relative", readonly ? "opacity-90" : "")}
        style={{ overflow: "visible" }}
      >
        {activeFile && (
          <CodeEditor
            codeEditorHeight={codeEditorHeight}
            activeFileLanguage={activeFile.language}
            activeFileContent={activeFileContent}
            isDarkTheme={isDarkTheme}
            readonly={isFileReadOnly(activeFile, readonly)}
            isEditing={false}
            updateActiveFileContent={updateActiveFileContent}
            handleCodeEditorResize={handleCodeEditorResize}
            onEditorMount={handleEditorMount}
            isAutocompleteEnabled={isAutocompleteEnabled}
          />
        )}
        {/* Drag handle para redimensionar o editor */}
        <div
          className={cn(
            "h-2 w-full cursor-ns-resize flex items-center justify-center border-t",
            isDarkTheme ? "border-gray-800 hover:bg-gray-800 bg-gray-900" : "border-gray-200 hover:bg-gray-200",
          )}
          onMouseDown={(e) => handleCodeEditorResize(e, e.clientY)}
        >
          <div className={cn("w-8 h-1 rounded-full", isDarkTheme ? "bg-gray-600" : "bg-gray-400")} />
        </div>
      </div>

      {/* Edit menu */}
      <ContentEditMenu options={editMenuOptions} className="opacity-100 hover:opacity-100" isPreviewMode={isPreview} />

      {/* Terminal */}
      {showExecution && (
        <>
          <Terminal
            isDarkTheme={isDarkTheme}
            isExecuting={isExecuting}
            output={output}
            input={typeof input === "string" ? input : ""}
            setInput={setInput}
            waitingForInput={waitingForInput || false}
            inputPrompt={inputPrompt || ""}
            terminalHeight={terminalHeight}
            handleTerminalResize={handleTerminalResize}
            handleTerminalInput={handleTerminalInput}
            handleExecute={handleExecute}
            handleStopExecution={handleStopExecution}
            clearTerminalOnRun={clearTerminalOnRun}
            setClearTerminalOnRun={setClearTerminalOnRun}
            updateSourceCode={updateSourceCode}
            terminalInputRef={terminalInputRef}
            isCompiled={["c", "cpp"].includes(selectedLanguage)}
            activeTab={activeTab}
            setActiveTab={setActiveTab}
            showTests={showTests}
            testCases={testCases}
            activeFileId={activeFileId}
            addTestCase={addTestCase}
            removeTestCase={removeTestCase}
            updateTestCase={updateTestCase}
            testResults={testResults}
            runTests={runTests}
            isEditing={false}
            addSolutionTemplate={addSolutionTemplate}
            fileSelectedLanguage={selectedLanguage}
            addCustomTestFiles={addCustomTestFiles}
          />
          {/* Add drag handle for resizing */}
          <div
            className={cn(
              "h-1 cursor-ns-resize flex items-center justify-center",
              isDarkTheme ? "bg-gray-800" : "bg-gray-200",
            )}
            onMouseDown={(e) => handleTerminalResize(e, e.clientY)}
          >
            <GripVertical className={cn("h-3 w-3", isDarkTheme ? "text-gray-600" : "text-gray-400")} />
          </div>
        </>
      )}

      {/* Settings panel */}
      {showSettings && (
        <SettingsPanel
          isDarkTheme={isDarkTheme}
          readonly={readonly}
          setReadonly={() => { }} // No-op in view mode
          showExecution={showExecution}
          setShowExecution={() => { }} // No-op in view mode
          showTests={showTests}
          setShowTests={() => { }} // No-op in view mode
          updateSourceCode={updateSourceCode} setSelectedLanguage={function (language: ProgrammingLanguage): void {
            throw new Error("Function not implemented.")
          }} fontSize={0} setFontSize={function (size: number): void {
            throw new Error("Function not implemented.")
          }} tabSize={0} setTabSize={function (size: number): void {
            throw new Error("Function not implemented.")
          }} wordWrap={false} setWordWrap={function (wrap: boolean): void {
            throw new Error("Function not implemented.")
          }} showLineNumbers={false} setShowLineNumbers={function (show: boolean): void {
            throw new Error("Function not implemented.")
          }} showMinimap={false} setShowMinimap={function (show: boolean): void {
            throw new Error("Function not implemented.")
          }} setIsDarkTheme={function (dark: boolean): void {
            throw new Error("Function not implemented.")
          }} clearTerminalOnRun={false} setClearTerminalOnRun={function (clear: boolean): void {
            throw new Error("Function not implemented.")
          }} setShowSettings={function (show: boolean): void {
            throw new Error("Function not implemented.")
          }} selectedLanguage={"javascript"} />
      )}
    </div>
  )
}
