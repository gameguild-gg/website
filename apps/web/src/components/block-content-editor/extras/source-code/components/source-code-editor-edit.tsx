"use client"

import { Button } from "@game-guild/ui/components/button"
import { Label } from "@game-guild/ui/components/label"
import { Switch } from "@game-guild/ui/components/switch"
import { cn } from "@/lib/utils"
import type React from "react"
import { useEffect } from "react"
import { CodeEditor } from "../code-editor"
import { FileTabs } from "../file-tabs"
import { Terminal } from "../terminal"
import type { CodeFile, ProgrammingLanguage } from "../types"

interface SourceCodeEditorEditProps extends Pick<
  React.ComponentProps<typeof Terminal>,
  "addTestCase" | "removeTestCase" | "testResults" | "runTests" | "testCases" | "updateTestCase" | "updateSourceCode"
> {
  // File management
  files: CodeFile[]
  activeFileId: string
  setActiveFileId: (id: string) => void
  activeFile: CodeFile | undefined
  activeFileContent: string
  setFiles: (files: CodeFile[] | ((prev: CodeFile[]) => CodeFile[])) => void

  // UI state
  isDarkTheme: boolean
  setIsDarkTheme: (dark: boolean) => void
  selectedLanguage: ProgrammingLanguage
  setSelectedLanguage: (lang: ProgrammingLanguage | string) => void
  readonly: boolean
  setReadonly: (readonly: boolean) => void
  showExecution: boolean
  setShowExecution: (show: boolean) => void
  showTests: boolean
  setShowTests: (show: boolean) => void
  hasConfiguredSettings: boolean
  setShowLanguagesDialog: (show: boolean) => void

  // Layout
  codeEditorHeight: number
  terminalHeight: number

  // Handlers
  handleCodeEditorResize: (e: React.MouseEvent, initialY: number) => void
  handleTerminalResize: (e: React.MouseEvent, initialY: number) => void
  handleEditorMount: NonNullable<React.ComponentProps<typeof CodeEditor>["onEditorMount"]>
  updateActiveFileContent: (content: string) => void
  isFileReadOnly: (file: CodeFile, readonly: boolean) => boolean

  // File operations
  toggleFileVisibility: (fileId: string) => void
  setMainFile: (fileId: string) => void
  deleteFile: (fileId: string) => void
  setFileReadOnlyState: (fileId: string, state: "always" | "never" | "hidden" | null) => void
  reorderFiles: (fileId: string, targetFileId: string) => void
  addNewFile: () => void

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
  getLanguageLabel: (lang: string) => string
  getFileIcon: (file: CodeFile) => React.ReactNode
  getStateIcon: (file: CodeFile) => React.ReactNode

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
  terminalInputRef: React.RefObject<HTMLInputElement | null>
  activeTab: "terminal" | "tests"
  setActiveTab: (tab: "terminal" | "tests") => void
  addSolutionTemplate?: () => void
  addCustomTestFiles?: (testIndex: number) => void

  // Save/Cancel
  setIsEditing: (editing: boolean) => void
  allowedLanguages: Record<string, boolean>
  activeEnvironments: Record<string, boolean>
  isAutocompleteEnabled: boolean
}

export function SourceCodeEditorEdit({
  files,
  activeFileId,
  setActiveFileId,
  activeFile,
  activeFileContent,
  setFiles,
  isDarkTheme,
  setIsDarkTheme,
  selectedLanguage,
  setSelectedLanguage,
  readonly,
  setReadonly,
  showExecution,
  setShowExecution,
  showTests,
  setShowTests,
  hasConfiguredSettings,
  setShowLanguagesDialog,
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
  addNewFile,
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
  testCases,
  addTestCase,
  removeTestCase,
  updateTestCase,
  testResults,
  runTests,
  addSolutionTemplate,
  addCustomTestFiles,
  setIsEditing,
  allowedLanguages,
  activeEnvironments,
  isAutocompleteEnabled,
}: SourceCodeEditorEditProps) {
  // Criar um arquivo padrão se não houver nenhum e se as configurações já foram definidas
  useEffect(() => {
    if (files.length === 0 && hasConfiguredSettings && allowedLanguages) {
      // Encontrar a primeira linguagem permitida como padrão
      const enabledLanguages = Object.entries(allowedLanguages)
        .filter(([_, enabled]) => enabled && _ !== "txt") // Exclui txt das linguagens de programação
        .map(([lang, _]) => lang as ProgrammingLanguage)

      // Usar a linguagem selecionada se estiver habilitada, senão usar a primeira habilitada
      let defaultLanguage = selectedLanguage
      const firstEnabled = enabledLanguages[0]
      if (!enabledLanguages.includes(selectedLanguage) && enabledLanguages.length > 0 && firstEnabled) {
        defaultLanguage = firstEnabled
      }

      // Determinar a extensão do arquivo com base na linguagem padrão
      const getExtension = (lang: ProgrammingLanguage) => {
        switch (lang) {
          case "javascript":
            return "js"
          case "typescript":
            return "ts"
          case "python":
            return "py"
          case "lua":
            return "lua"
          case "cpp":
            return "cpp"
          case "c":
            return "c"
          case "h":
            return "h"
          case "hpp":
            return "hpp"
          default:
            return "txt"
        }
      }

      // Criar um arquivo inicial com base na linguagem padrão
      const newFile = {
        id: crypto.randomUUID(),
        name: `index.${getExtension(defaultLanguage)}`,
        content: "",
        language: defaultLanguage,
        isMain: false,
        isVisible: true,
      }

      setFiles([newFile])
      setActiveFileId(newFile.id)

      // Atualizar a linguagem selecionada se necessário
      if (defaultLanguage !== selectedLanguage) {
        setSelectedLanguage(defaultLanguage)
      }
    }
  }, [files, hasConfiguredSettings, allowedLanguages, selectedLanguage, setFiles, setActiveFileId, setSelectedLanguage])

  return (
    <div className="rounded-lg border overflow-hidden">
      {/* Verificar se as configurações já foram configuradas e abrir o diálogo se necessário */}
      {!hasConfiguredSettings && (
        <div className="absolute inset-0 z-50 bg-black/50 flex items-center justify-center">
          <div className="bg-background p-4 rounded-lg shadow-lg text-center">
            <h3 className="text-lg font-medium mb-2">Configuração Inicial</h3>
            <p className="mb-4">Configure as linguagens disponíveis para este ambiente de código.</p>
            <Button
              onClick={() => {
                setShowLanguagesDialog(true)
              }}
              className="w-full"
            >
              Configurar Linguagens
            </Button>
          </div>
        </div>
      )}

      {/* File tabs for editing mode */}
      <FileTabs
        files={files}
        activeFileId={activeFileId}
        setActiveFileId={setActiveFileId}
        isDarkTheme={isDarkTheme}
        setIsDarkTheme={setIsDarkTheme}
        selectedLanguage={selectedLanguage}
        setSelectedLanguage={setSelectedLanguage}
        isEditing={true}
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

      {/* Code editor for editing mode */}
      <div className="bg-background font-mono text-sm relative" style={{ overflow: "visible" }}>
        {activeFile ? (
          <CodeEditor
            codeEditorHeight={codeEditorHeight}
            activeFileLanguage={activeFile.language}
            activeFileContent={activeFileContent}
            isDarkTheme={isDarkTheme}
            readonly={false} // Garantir que não esteja readonly no modo de edição
            isEditing={true}
            updateActiveFileContent={updateActiveFileContent}
            handleCodeEditorResize={handleCodeEditorResize}
            onEditorMount={handleEditorMount}
            isAutocompleteEnabled={isAutocompleteEnabled}
          />
        ) : (
          <div
            className="flex items-center justify-center p-4 text-muted-foreground"
            style={{ height: `${codeEditorHeight}px` }}
          >
            Nenhum arquivo disponível. Clique em "+" para adicionar um arquivo.
          </div>
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

      {/* Terminal for editing mode */}
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
            isEditing={true}
            addSolutionTemplate={addSolutionTemplate}
            fileSelectedLanguage={selectedLanguage}
            addCustomTestFiles={addCustomTestFiles}
          />
          {/* Drag handle para redimensionar o terminal */}
          <div
            className={cn(
              "h-2 w-full cursor-ns-resize flex items-center justify-center border-t",
              isDarkTheme ? "border-gray-800 hover:bg-gray-800 bg-gray-900" : "border-gray-200 hover:bg-gray-200",
            )}
            onMouseDown={(e) => handleTerminalResize(e, e.clientY)}
          >
            <div className={cn("w-8 h-1 rounded-full", isDarkTheme ? "bg-gray-600" : "bg-gray-400")} />
          </div>
        </>
      )}

      {/* Settings bar */}
      <div
        className={cn(
          "p-3 flex items-center justify-between",
          isDarkTheme ? "bg-gray-800 text-white" : "bg-muted text-foreground",
        )}
      >
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-2">
            <Switch
              id="readonly-toggle-edit"
              checked={readonly}
              onCheckedChange={(checked) => {
                setReadonly(checked)
              }}
            />
            <Label htmlFor="readonly-toggle-edit" className="text-sm">
              Read-only
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <Switch id="execution-toggle-edit" checked={showExecution} onCheckedChange={setShowExecution} />
            <Label htmlFor="execution-toggle-edit" className="text-sm">
              Show Execution
            </Label>
          </div>
          {showExecution && (
            <div className="flex items-center space-x-2">
              <Switch id="tests-toggle-edit" checked={showTests} onCheckedChange={setShowTests} />
              <Label htmlFor="tests-toggle-edit" className="text-sm">
                Show Tests
              </Label>
            </div>
          )}
        </div>
        <div className="flex space-x-2">
          <Button
            variant={isDarkTheme ? "secondary" : "outline"}
            className={cn(isDarkTheme ? "hover:bg-gray-700 text-gray-200" : "hover:bg-gray-100")}
            onClick={() => {
              setIsEditing(false)
            }}
          >
            Cancel
          </Button>
          <Button
            variant="default"
            className={cn(isDarkTheme ? "bg-blue-600 hover:bg-blue-700" : "bg-primary hover:bg-primary/90")}
            onClick={() => {
              const filesCopy = files.map((file) => ({ ...file }))

              updateSourceCode({
                readonly,
                showExecution,
                files: filesCopy,
                testCases: testCases,
                hasConfiguredSettings: true,
                allowedLanguages: allowedLanguages,
                activeEnvironments: activeEnvironments,
              })

              setIsEditing(false)
            }}
          >
            Save
          </Button>
        </div>
      </div>
    </div>
  )
}
