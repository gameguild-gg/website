"use client"

import { TooltipProvider } from "@game-guild/ui/components/tooltip"
import type React from "react"
import { useEffect } from "react"

// Import types
import type { CodeFile, LanguageType, ProgrammingLanguage, SourceCodeData, SourceCodeTestCase, SourceCodeTestCases } from "./types"

// Import components
import { SourceCodeDialogs } from "./components/source-code-dialogs"
import { SourceCodeEditorEdit } from "./components/source-code-editor-edit"
import { SourceCodeEditorView } from "./components/source-code-editor-view"

// Import utilities
import { getBaseName, getFileIcon, getLanguageLabel, getStateIcon } from "./utils"

// Import hooks
import { useCustomTestFiles } from "@/components/block-content-editor/hooks/editor/use-custom-test-files"
import { useSourceCodeEditor } from "@/components/block-content-editor/hooks/editor/use-source-code-editor"

export interface SourceCodeRendererProps {
  data: SourceCodeData & { hasConfiguredSettings?: boolean; initialFileLanguage?: string }
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
  isExecuting: boolean
  setIsExecuting: (executing: boolean) => void
  output: string[]
  setOutput: (output: string[] | ((prev: string[]) => string[])) => void
  input: string
  setInput: (input: string) => void
  showSettings: boolean
  setShowSettings: (show: boolean) => void
  waitingForInput: boolean
  setWaitingForInput: (waiting: boolean) => void
  inputPrompt: string
  setInputPrompt: (prompt: string) => void
  inputCallback: ((value: string) => void) | null
  setInputCallback: (callback: ((value: string) => void) | null) => void
  terminalInputRef: React.RefObject<HTMLInputElement | null>
  commandHistory: string[]
  setCommandHistory: (history: string[] | ((prev: string[]) => string[])) => void
  historyIndex: number
  setHistoryIndex: (index: number) => void
  isDarkTheme: boolean
  setIsDarkTheme: (dark: boolean) => void
  codeEditorHeight: number
  setCodeEditorHeight: (height: number) => void
  terminalHeight: number
  setTerminalHeight: (height: number) => void
  showFileDialog: boolean
  setShowFileDialog: (show: boolean) => void
  newFileName: string
  setNewFileName: (name: string) => void
  newFileLanguage: LanguageType
  setNewFileLanguage: (lang: LanguageType) => void
  newFileHasStates?: boolean
  setNewFileHasStates?: (hasStates: boolean) => void
  showImportDialog: boolean
  setShowImportDialog: (show: boolean) => void
  importContents: { name: string; content: string }[]
  setImportContents: (contents: { name: string; content: string }[]) => void
  importFileNames: string[]
  setImportFileNames: (names: string[]) => void
  importFileHasStates?: boolean
  setImportFileHasStates?: (hasStates: boolean) => void
  fileInputRef: React.RefObject<HTMLInputElement | null>
  showConfirmDialog: boolean
  setShowConfirmDialog: (show: boolean) => void
  draggedFileId: string | null
  setDraggedFileId: (id: string | null) => void
  dragOverFileId: string | null
  setDragOverFileId: (id: string | null) => void
  showRenameDialog: boolean
  setShowRenameDialog: (show: boolean) => void
  renameFileName: string
  setRenameFileName: (name: string) => void
  renameFileLanguage: LanguageType
  setRenameFileLanguage: (lang: LanguageType) => void
  fileToRename: string | null
  setFileToRename: (id: string | null) => void
  selectedLanguage: ProgrammingLanguage
  setSelectedLanguage: (lang: ProgrammingLanguage | string) => void
  clearTerminalOnRun: boolean
  setClearTerminalOnRun: (clear: boolean) => void
  allowedLanguages: Record<LanguageType, boolean>
  setAllowedLanguages: (value: Record<string, boolean> | ((prev: Record<string, boolean>) => Record<string, boolean>)) => void
  showLanguagesDialog: boolean
  setShowLanguagesDialog: (show: boolean) => void
  showBasicFileActionsInReadMode: boolean
  setShowBasicFileActionsInReadMode: (show: boolean) => void
  showFilePropertiesInReadMode: boolean
  setShowFilePropertiesInReadMode: (show: boolean) => void
  showTests: boolean
  setShowTests: (show: boolean) => void
  activeTab: "terminal" | "tests"
  setActiveTab: (tab: "terminal" | "tests") => void
  testCases: SourceCodeTestCases
  setTestCases: (cases: SourceCodeTestCases) => void
  testResults: Record<string, { passed: boolean; actual: string; expected: string }[]>
  setTestResults: (results: Record<string, { passed: boolean; actual: string; expected: string }[]>) => void
  addTestCase: (fileId: string, type?: "custom" | "function" | "console") => void
  removeTestCase: (fileId: string, index: number) => void
  updateTestCase: (
    fileId: string,
    index: number,
    testData: Partial<SourceCodeTestCase>,
  ) => void
  runTests: (fileId: string) => void
  isPreview?: boolean
  id?: string
  addCustomTestFiles?: (testIndex: number) => void

  // Functions
  addNewFile: () => void
  importFile: () => void
  handleFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  deleteFile: (fileId: string) => void
  toggleFileVisibility: (fileId: string) => void
  setMainFile: (fileId: string) => void
  reorderFiles: (fileId: string, targetFileId: string) => void
  renameFile: () => void
  updateSourceCode: (newData: Partial<SourceCodeData>) => void
  handleSave: () => void
  handleExecute: () => void
  handleStopExecution: () => void
  handleTerminalInput: (e: React.KeyboardEvent<HTMLInputElement>) => void
  handleCodeEditorResize: (e: React.MouseEvent, initialY: number) => void
  handleTerminalResize: (e: React.MouseEvent, initialY: number) => void
  getAllowedLanguageTypes: () => LanguageType[]
  getAllowedProgrammingLanguages: () => ProgrammingLanguage[]
  isFileReadOnly: (file: CodeFile, readonly: boolean) => boolean
  updateActiveFileContent: (content: string) => void
  setFileReadOnlyState: (fileId: string, state: "always" | "never" | "hidden" | null) => void
  addSolutionTemplate?: () => void
}

export function SourceCodeRenderer({
  id = `source-code-${crypto.randomUUID()}`,
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
  isExecuting,
  setIsExecuting,
  output,
  setOutput,
  input,
  setInput,
  showSettings,
  setShowSettings,
  waitingForInput,
  setWaitingForInput,
  inputPrompt,
  setInputPrompt,
  inputCallback,
  setInputCallback,
  terminalInputRef,
  commandHistory,
  setCommandHistory,
  historyIndex,
  setHistoryIndex,
  isDarkTheme,
  setIsDarkTheme,
  codeEditorHeight,
  setCodeEditorHeight,
  terminalHeight,
  setTerminalHeight,
  showFileDialog,
  setShowFileDialog,
  newFileName,
  setNewFileName,
  newFileLanguage,
  setNewFileLanguage,
  showImportDialog,
  setShowImportDialog,
  importContents,
  setImportContents,
  importFileNames,
  setImportFileNames,
  fileInputRef,
  showConfirmDialog,
  setShowConfirmDialog,
  draggedFileId,
  setDraggedFileId,
  dragOverFileId,
  setDragOverFileId,
  showRenameDialog,
  setShowRenameDialog,
  renameFileName,
  setRenameFileName,
  renameFileLanguage,
  setRenameFileLanguage,
  fileToRename,
  setFileToRename,
  selectedLanguage,
  setSelectedLanguage,
  clearTerminalOnRun,
  setClearTerminalOnRun,
  allowedLanguages,
  setAllowedLanguages,
  showLanguagesDialog,
  setShowLanguagesDialog,
  showBasicFileActionsInReadMode,
  setShowBasicFileActionsInReadMode,
  showFilePropertiesInReadMode,
  setShowFilePropertiesInReadMode,
  showTests,
  setShowTests,
  activeTab,
  setActiveTab,
  testCases,
  setTestCases,
  testResults,
  setTestResults,
  addTestCase,
  removeTestCase,
  updateTestCase,
  runTests,
  isPreview = false,
  addCustomTestFiles,

  // Functions
  addNewFile,
  importFile,
  handleFileUpload,
  deleteFile,
  toggleFileVisibility,
  setMainFile,
  reorderFiles,
  renameFile,
  updateSourceCode,
  handleSave,
  handleExecute,
  handleStopExecution,
  handleTerminalInput,
  handleCodeEditorResize,
  handleTerminalResize,
  getAllowedLanguageTypes,
  getAllowedProgrammingLanguages,
  isFileReadOnly,
  updateActiveFileContent,
  setFileReadOnlyState,
  addSolutionTemplate,
}: SourceCodeRendererProps) {
  // Use the custom hook for editor logic
  const {
    isAutocompleteEnabled,
    setIsAutocompleteEnabled,
    hasConfiguredSettings,
    setHasConfiguredSettings,
    activeEnvironments,
    setActiveEnvironments,
    visibleFiles,
    activeFile,
    activeFileContent,
    editMenuOptions,
    codeIsExecuting,
    setCodeIsExecuting,
    executeCode,
    stopCodeExecution,
    handleEditorMount,
    addOutput,
    clearTerminal,
  } = useSourceCodeEditor({
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
    output,
    setOutput,
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
    testCases,
    setTestResults,
    handleExecute,
    updateSourceCode,
    handleSave,
  })

  // Use the custom hook for test files
  const { createCustomTestFiles } = useCustomTestFiles({
    files,
    setFiles,
    activeFileId,
    testCases,
  })

  useEffect(() => {
    // Check if configuration data exists in the data object
    const configData = data

    if (configData.hasConfiguredSettings !== undefined) {
      console.log("[v0] Found hasConfiguredSettings:", configData.hasConfiguredSettings)
      setHasConfiguredSettings(configData.hasConfiguredSettings)
    }

    if (configData.allowedLanguages) {
      console.log("[v0] Found allowedLanguages:", configData.allowedLanguages)
      setAllowedLanguages(configData.allowedLanguages)
    }

    if (configData.activeEnvironments) {
      console.log("[v0] Found activeEnvironments:", configData.activeEnvironments)
      setActiveEnvironments(configData.activeEnvironments)
    }

    // Also check if the configuration is nested deeper in the data structure
    if (!configData.hasConfiguredSettings && configData.data) {
      const nestedData = configData.data
      if (nestedData.hasConfiguredSettings !== undefined) {
        console.log("[v0] Found nested hasConfiguredSettings:", nestedData.hasConfiguredSettings)
        setHasConfiguredSettings(nestedData.hasConfiguredSettings)
      }

      if (nestedData.allowedLanguages) {
        console.log("[v0] Found nested allowedLanguages:", nestedData.allowedLanguages)
        setAllowedLanguages(nestedData.allowedLanguages)
      }

      if (nestedData.activeEnvironments) {
        console.log("[v0] Found nested activeEnvironments:", nestedData.activeEnvironments)
        setActiveEnvironments(nestedData.activeEnvironments)
      }
    }
  }, [data, setHasConfiguredSettings, setAllowedLanguages, setActiveEnvironments])

  useEffect(() => {
    console.log(
      "[v0] Checking if should show language dialog - isEditing:",
      isEditing,
      "hasConfiguredSettings:",
      hasConfiguredSettings,
    )

    // Only show the dialog if we're editing AND settings are not configured
    if (isEditing && !hasConfiguredSettings) {
      console.log("[v0] Showing language configuration dialog")
      setShowLanguagesDialog(true)
    } else {
      console.log("[v0] Not showing language dialog - already configured")
    }
  }, [isEditing, hasConfiguredSettings, setShowLanguagesDialog])

  // Create initial file based on environment configuration
  useEffect(() => {
    if (isEditing && files.length === 0 && hasConfiguredSettings && allowedLanguages) {
      // Encontrar a primeira linguagem permitida como padrão
      const enabledLanguages = Object.entries(allowedLanguages)
        .filter(([_, enabled]) => enabled && _ !== "txt") // Exclui txt das linguagens de programação
        .map(([lang, _]) => lang as ProgrammingLanguage)

      // Usar a linguagem inicial especificada se estiver habilitada, senão usar a primeira habilitada
      let defaultLanguage: ProgrammingLanguage = selectedLanguage

      if (data.initialFileLanguage && enabledLanguages.includes(data.initialFileLanguage as ProgrammingLanguage)) {
        defaultLanguage = data.initialFileLanguage as ProgrammingLanguage
      } else if (enabledLanguages.length > 0 && enabledLanguages[0]) {
        defaultLanguage = enabledLanguages[0]
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
        isMain: true,
        isVisible: true,
      }

      setFiles([newFile])
      setActiveFileId(newFile.id)

      // Atualizar a linguagem selecionada para corresponder ao arquivo inicial
      if (defaultLanguage !== selectedLanguage) {
        setSelectedLanguage(defaultLanguage)
      }
    }
  }, [
    isEditing,
    files,
    hasConfiguredSettings,
    allowedLanguages,
    data.initialFileLanguage,
    setFiles,
    setActiveFileId,
    selectedLanguage,
    setSelectedLanguage,
  ])

  // Helper functions for file operations
  const getExtensionForSelectedLanguage = () => {
    switch (selectedLanguage) {
      case "javascript":
        return "js"
      case "typescript":
        return "ts"
      case "python":
        return "py"
      case "lua":
        return "lua"
      case "c":
        return "c"
      case "cpp":
        return "cpp"
      case "h":
        return "h"
      case "hpp":
        return "hpp"
      default:
        return "txt"
    }
  }

  // Dialog handlers
  const handleShowRenameDialog = (fileId?: string) => {
    if (!fileId) return
    const file = files.find((f) => f.id === fileId)
    if (file) {
      setRenameFileName(getBaseName(file.name))
      setRenameFileLanguage(file.language)
      setFileToRename(file.id)
      setShowRenameDialog(true)
    }
  }

  const handleShowImportDialog = () => {
    setImportFileNames([])
    setImportContents([])
    setShowImportDialog(true)
  }

  // Render the component
  return (
    <TooltipProvider>
      <div id={id} className="my-4 relative group">
        {!isEditing ? (
          <SourceCodeEditorView
            files={visibleFiles}
            activeFileId={activeFileId}
            setActiveFileId={setActiveFileId}
            activeFile={activeFile}
            activeFileContent={activeFileContent}
            isDarkTheme={isDarkTheme}
            setIsDarkTheme={setIsDarkTheme}
            selectedLanguage={selectedLanguage}
            setSelectedLanguage={setSelectedLanguage}
            readonly={readonly}
            showExecution={showExecution}
            showSettings={showSettings}
            isPreview={isPreview}
            codeEditorHeight={codeEditorHeight}
            terminalHeight={terminalHeight}
            handleCodeEditorResize={handleCodeEditorResize}
            handleTerminalResize={handleTerminalResize}
            handleEditorMount={handleEditorMount}
            updateActiveFileContent={updateActiveFileContent}
            isFileReadOnly={isFileReadOnly}
            toggleFileVisibility={toggleFileVisibility}
            setMainFile={setMainFile}
            deleteFile={deleteFile}
            setFileReadOnlyState={setFileReadOnlyState}
            reorderFiles={reorderFiles}
            showFileDialog={() => setShowFileDialog(true)}
            showRenameDialog={handleShowRenameDialog}
            showImportDialog={handleShowImportDialog}
            showConfirmDialog={() => setShowConfirmDialog(true)}
            showLanguagesDialog={() => setShowLanguagesDialog(true)}
            getBaseName={getBaseName}
            getExtensionForSelectedLanguage={getExtensionForSelectedLanguage}
            getAllowedProgrammingLanguages={getAllowedProgrammingLanguages}
            getLanguageLabel={getLanguageLabel}
            getFileIcon={(file) => getFileIcon(file, selectedLanguage)}
            getStateIcon={getStateIcon}
            draggedFileId={draggedFileId}
            setDraggedFileId={setDraggedFileId}
            dragOverFileId={dragOverFileId}
            setDragOverFileId={setDragOverFileId}
            showBasicFileActionsInReadMode={showBasicFileActionsInReadMode}
            showFilePropertiesInReadMode={showFilePropertiesInReadMode}
            setShowBasicFileActionsInReadMode={setShowBasicFileActionsInReadMode}
            setShowFilePropertiesInReadMode={setShowFilePropertiesInReadMode}
            isExecuting={codeIsExecuting}
            output={output}
            input={input}
            setInput={setInput}
            waitingForInput={waitingForInput}
            inputPrompt={inputPrompt}
            handleTerminalInput={handleTerminalInput}
            handleExecute={() => executeCode(activeFileId)}
            handleStopExecution={stopCodeExecution}
            clearTerminalOnRun={clearTerminalOnRun}
            setClearTerminalOnRun={setClearTerminalOnRun}
            updateSourceCode={updateSourceCode}
            terminalInputRef={terminalInputRef}
            activeTab={activeTab}
            setActiveTab={setActiveTab}
            showTests={showTests}
            testCases={testCases}
            addTestCase={addTestCase}
            removeTestCase={removeTestCase}
            updateTestCase={updateTestCase}
            testResults={testResults}
            runTests={() => Promise.resolve()}
            addSolutionTemplate={addSolutionTemplate}
            addCustomTestFiles={(testIndex: number) => {
              createCustomTestFiles(testIndex, selectedLanguage)
            }}
            editMenuOptions={editMenuOptions}
            isAutocompleteEnabled={isAutocompleteEnabled}
          />
        ) : (
          <SourceCodeEditorEdit
            files={files}
            activeFileId={activeFileId}
            setActiveFileId={setActiveFileId}
            activeFile={activeFile}
            activeFileContent={activeFileContent}
            setFiles={setFiles}
            isDarkTheme={isDarkTheme}
            setIsDarkTheme={setIsDarkTheme}
            selectedLanguage={selectedLanguage}
            setSelectedLanguage={setSelectedLanguage}
            readonly={readonly}
            setReadonly={setReadonly}
            showExecution={showExecution}
            setShowExecution={setShowExecution}
            showTests={showTests}
            setShowTests={setShowTests}
            hasConfiguredSettings={hasConfiguredSettings}
            setShowLanguagesDialog={setShowLanguagesDialog}
            codeEditorHeight={codeEditorHeight}
            terminalHeight={terminalHeight}
            handleCodeEditorResize={handleCodeEditorResize}
            handleTerminalResize={handleTerminalResize}
            handleEditorMount={handleEditorMount}
            updateActiveFileContent={updateActiveFileContent}
            isFileReadOnly={isFileReadOnly}
            toggleFileVisibility={toggleFileVisibility}
            setMainFile={setMainFile}
            deleteFile={deleteFile}
            setFileReadOnlyState={setFileReadOnlyState}
            reorderFiles={reorderFiles}
            addNewFile={addNewFile}
            showFileDialog={() => setShowFileDialog(true)}
            showRenameDialog={handleShowRenameDialog}
            showImportDialog={handleShowImportDialog}
            showConfirmDialog={() => setShowConfirmDialog(true)}
            showLanguagesDialog={() => setShowLanguagesDialog(true)}
            getBaseName={getBaseName}
            getExtensionForSelectedLanguage={getExtensionForSelectedLanguage}
            getAllowedProgrammingLanguages={getAllowedProgrammingLanguages}
            getLanguageLabel={getLanguageLabel}
            getFileIcon={(file) => getFileIcon(file, selectedLanguage)}
            getStateIcon={getStateIcon}
            draggedFileId={draggedFileId}
            setDraggedFileId={setDraggedFileId}
            dragOverFileId={dragOverFileId}
            setDragOverFileId={setDragOverFileId}
            showBasicFileActionsInReadMode={showBasicFileActionsInReadMode}
            showFilePropertiesInReadMode={showFilePropertiesInReadMode}
            setShowBasicFileActionsInReadMode={setShowBasicFileActionsInReadMode}
            setShowFilePropertiesInReadMode={setShowFilePropertiesInReadMode}
            isExecuting={codeIsExecuting}
            output={output}
            input={input}
            setInput={setInput}
            waitingForInput={waitingForInput}
            inputPrompt={inputPrompt}
            handleTerminalInput={handleTerminalInput}
            handleExecute={() => executeCode(activeFileId)}
            handleStopExecution={stopCodeExecution}
            clearTerminalOnRun={clearTerminalOnRun}
            setClearTerminalOnRun={setClearTerminalOnRun}
            updateSourceCode={updateSourceCode}
            terminalInputRef={terminalInputRef}
            activeTab={activeTab}
            setActiveTab={setActiveTab}
            testCases={testCases}
            addTestCase={addTestCase}
            removeTestCase={removeTestCase}
            updateTestCase={updateTestCase}
            testResults={testResults}
            runTests={() => Promise.resolve()}
            addSolutionTemplate={addSolutionTemplate}
            setIsEditing={setIsEditing}
            allowedLanguages={allowedLanguages}
            activeEnvironments={activeEnvironments}
            isAutocompleteEnabled={isAutocompleteEnabled}
          />
        )}

        {/* Dialogs */}
        <SourceCodeDialogs
          showFileDialog={showFileDialog}
          setShowFileDialog={setShowFileDialog}
          newFileName={newFileName}
          setNewFileName={setNewFileName}
          newFileLanguage={newFileLanguage}
          setNewFileLanguage={setNewFileLanguage}
          addNewFile={addNewFile}
          getAllowedLanguageTypes={getAllowedLanguageTypes}
          getLanguageLabel={getLanguageLabel}
          showImportDialog={showImportDialog}
          setShowImportDialog={setShowImportDialog}
          importFileNames={importFileNames}
          importContents={importContents}
          handleFileUpload={handleFileUpload}
          importFile={importFile}
          fileInputRef={fileInputRef}
          showConfirmDialog={showConfirmDialog}
          setShowConfirmDialog={setShowConfirmDialog}
          activeFileId={activeFileId}
          showRenameDialog={showRenameDialog}
          setShowRenameDialog={setShowRenameDialog}
          renameFileName={renameFileName}
          setRenameFileName={setRenameFileName}
          renameFileLanguage={renameFileLanguage}
          setRenameFileLanguage={setRenameFileLanguage}
          fileToRename={fileToRename}
          renameFile={renameFile}
          files={files}
          showLanguagesDialog={showLanguagesDialog}
          setShowLanguagesDialog={setShowLanguagesDialog}
          allowedLanguages={allowedLanguages}
          setAllowedLanguages={setAllowedLanguages}
          selectedLanguage={selectedLanguage}
          setSelectedLanguage={setSelectedLanguage}
          updateSourceCode={updateSourceCode}
          isAutocompleteEnabled={isAutocompleteEnabled}
          setIsAutocompleteEnabled={setIsAutocompleteEnabled}
          hasConfiguredSettings={hasConfiguredSettings}
          setHasConfiguredSettings={setHasConfiguredSettings}
          activeEnvironments={activeEnvironments}
          setActiveEnvironments={setActiveEnvironments}
          isPreview={isPreview}
        />
      </div>
    </TooltipProvider>
  )
}
