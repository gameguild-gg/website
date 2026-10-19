"use client"

import type React from "react"
import type { ProgrammingLanguage, SourceCodeData, SourceCodeTestCase, SourceCodeTestCases } from "./types"

import { Button } from "@game-guild/ui/components/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog"
import { Label } from "@game-guild/ui/components/label"
import { Switch } from "@game-guild/ui/components/switch"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@game-guild/ui/components/tooltip"
import { cn } from "@/lib/utils"
import {
  AlertTriangle,
  CheckSquare,
  HelpCircle,
  MessageSquare,
  Play,
  Plus,
  Square,
  TerminalIcon,
  X,
} from "lucide-react"
import { useEffect, useRef, useState } from "react"
import {
  DEFAULT_FIRST_CODE_TEMPLATES,
  DEFAULT_SECOND_CODE_TEMPLATES,
  FUNCTION_FIRST_CODE_TEMPLATES,
  FUNCTION_SECOND_CODE_TEMPLATES,
  getExtensionForSelectedLanguage,
} from "./templates/code-templates"

// Declare global types for our custom window properties
declare global {
  interface Window {
    __currentTestIndex: number
    __awaitingPromptInput: boolean
    __awaitingConfirmInput: boolean
    __awaitingAlertAck: boolean
    __promptMessage: string | null
    __confirmMessage: string | null
    __alertMessage: string | null
    promptCallback: ((value: string) => void)
    confirmCallback: ((value: string) => void)
    alertCallback: (() => void)
  }
}

interface TerminalOutput {
  id: string
  type: "input" | "output" | "error" | "system" | "alert" | "prompt" | "confirm"
  content: string | string[]
}

interface TerminalProps {
  isDarkTheme: boolean
  isExecuting: boolean
  output: string[]
  input: string
  setInput: (input: string) => void
  waitingForInput?: boolean
  inputPrompt?: string
  terminalHeight: number
  handleTerminalResize: (e: React.MouseEvent, initialY: number) => void
  handleTerminalInput: (e: React.KeyboardEvent<HTMLInputElement>) => void
  handleExecute: () => void
  handleStopExecution: () => void
  clearTerminalOnRun: boolean
  setClearTerminalOnRun: (clear: boolean) => void
  updateSourceCode: (data: Partial<SourceCodeData>) => void
  terminalInputRef: React.RefObject<HTMLInputElement | null>
  isCompiled?: boolean
  activeTab: "terminal" | "tests"
  setActiveTab: (tab: "terminal" | "tests") => void
  showTests: boolean
  testCases: SourceCodeTestCases
  activeFileId: string
  addTestCase: (fileId: string, type?: "custom" | "function" | "console") => void
  removeTestCase: (fileId: string, index: number) => void
  updateTestCase: (
    fileId: string,
    index: number,
    testData: Partial<SourceCodeTestCase>,
  ) => void
  testResults: Record<string, { passed: boolean; actual: string; expected: string }[]>
  runTests: (fileId: string) => void
  isEditing?: boolean
  addSolutionTemplate?: () => void
  fileSelectedLanguage?: ProgrammingLanguage // Add this new prop
  addCustomTestFiles?: (testIndex: number) => void
}

function TestTypeDialog({
  isOpen,
  setIsOpen,
  onSelect,
  defaultValue,
  hasCustomTest = false,
  hasFunctionTest = false,
  hasConsoleTest = false,
}: {
  isOpen: boolean
  setIsOpen: (open: boolean) => void
  onSelect: (type: "custom" | "function" | "console") => void
  defaultValue?: "custom" | "function" | "console"
  hasCustomTest?: boolean
  hasFunctionTest?: boolean
  hasConsoleTest?: boolean
}) {
  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogContent className="sm:max-w-5xl">
        <DialogHeader>
          <DialogTitle>Select Test Type</DialogTitle>
          <DialogDescription>Choose the type of test you want to create</DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-3 gap-6 py-6">
          <button
            type="button"
            className={`flex flex-col items-center justify-center p-6 rounded-md border transition-all min-h-[120px]
              ${!hasCustomTest ? "hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20" : "opacity-50 cursor-not-allowed"}
              ${defaultValue === "custom"
                ? "border-blue-500 bg-blue-50 dark:bg-blue-900/20"
                : "border-gray-200 dark:border-gray-700"
              }`}
            onClick={() => {
              if (!hasCustomTest) onSelect("custom")
            }}
            disabled={hasCustomTest}
          >
            <div className="text-base font-medium mb-3 text-center">Custom Test</div>
            <p className="text-sm text-muted-foreground text-center leading-relaxed">
              {hasCustomTest
                ? "Only one custom test allowed per file"
                : "Add custom code that runs before and after the student's code"}
            </p>
          </button>
          <button
            type="button"
            className={`flex flex-col items-center justify-center p-6 rounded-md border transition-all min-h-[120px]
              ${!hasFunctionTest ? "hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20" : "opacity-50 cursor-not-allowed"}
              ${defaultValue === "function"
                ? "border-blue-500 bg-blue-50 dark:bg-blue-900/20"
                : "border-gray-200 dark:border-gray-700"
              }`}
            onClick={() => {
              if (!hasFunctionTest) onSelect("function")
            }}
            disabled={hasFunctionTest}
          >
            <div className="text-base font-medium mb-3 text-center">Function Test</div>
            <p className="text-sm text-muted-foreground text-center leading-relaxed">
              {hasFunctionTest
                ? "Only one function test allowed per file"
                : "Test functions with assertion helpers and structured validation"}
            </p>
          </button>
          <button
            type="button"
            className={`flex flex-col items-center justify-center p-6 rounded-md border transition-all min-h-[120px]
              ${!hasConsoleTest ? "hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20" : "opacity-50 cursor-not-allowed"}
              ${defaultValue === "console"
                ? "border-blue-500 bg-blue-50 dark:bg-blue-900/20"
                : "border-gray-200 dark:border-gray-700"
              }`}
            onClick={() => {
              if (!hasConsoleTest) onSelect("console")
            }}
            disabled={hasConsoleTest}
          >
            <div className="text-base font-medium mb-3 text-center">Console Test</div>
            <p className="text-sm text-muted-foreground text-center leading-relaxed">
              {hasConsoleTest
                ? "Only one console test allowed per file"
                : "Test console interactions with custom validation logic"}
            </p>
          </button>
        </div>
        <DialogFooter>
          <Button type="button" onClick={() => setIsOpen(false)}>
            Cancel
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// Componentized Custom Test UI
function CustomTestUI({
  testCase,
  index,
  activeFileId,
  updateTestCase,
  isEditing,
  isDarkTheme,
  fileSelectedLanguage,
  selectedLanguage,
  addCustomTestFiles,
  testType,
}: {
  testCase: SourceCodeTestCase
  index: number
  activeFileId: string
  updateTestCase: TerminalProps["updateTestCase"]
  isEditing: boolean
  isDarkTheme: boolean
  fileSelectedLanguage?: ProgrammingLanguage
  selectedLanguage: ProgrammingLanguage
  addCustomTestFiles?: (testIndex: number) => void
  testType: "custom" | "function" | "console"
}) {
  const getTemplateForType = (codeType: "first" | "second") => {
    if (testType === "function") {
      return codeType === "first" ? FUNCTION_FIRST_CODE_TEMPLATES : FUNCTION_SECOND_CODE_TEMPLATES
    } else {
      return codeType === "first" ? DEFAULT_FIRST_CODE_TEMPLATES : DEFAULT_SECOND_CODE_TEMPLATES
    }
  }

  const getTestTypeLabel = () => {
    return testType === "function" ? "Function Test" : testType === "console" ? "Console Test" : "Custom Test"
  }

  const getTestTypeDescription = () => {
    return testType === "function"
      ? "This test provides assertion helpers and structured function testing in a single file."
      : testType === "console"
        ? "This test provides console interaction testing with custom validation logic."
        : "This test requires a single custom test file."
  }

  return (
    <div className="space-y-2">
      <div className="flex justify-between items-center mb-2">
        <div className="text-xs font-medium">{getTestTypeLabel()} Files</div>
        {isEditing && (
          <button
            onClick={() => {
              // Create custom test files in the main editor
              addCustomTestFiles?.(index)
            }}
            className={cn(
              "p-1 rounded text-xs flex items-center",
              isDarkTheme ? "bg-blue-900 hover:bg-blue-800 text-white" : "bg-blue-100 hover:bg-blue-200 text-blue-800",
            )}
          >
            <Plus className="h-3 w-3 mr-1" />
            Create Test Files
          </button>
        )}
      </div>

      {/* Campo de informações do teste */}
      <div className="mb-3">
        <label className="block text-xs mb-1">Test Information:</label>
        <textarea
          value={testCase.testInfo || ""}
          onChange={(e) => {
            updateTestCase(activeFileId, index, { testInfo: e.target.value })
          }}
          className={cn(
            "w-full p-2 text-xs border rounded resize-none",
            isDarkTheme ? "bg-gray-900 border-gray-700" : "bg-white border-gray-300",
            !isEditing && "opacity-90",
          )}
          rows={3}
          placeholder={`Add description, instructions, or notes about this ${testType} test...`}
          readOnly={!isEditing}
        />
      </div>

      {isEditing && (
        <div className="p-4 border rounded bg-gray-50 dark:bg-gray-800 text-center">
          <p className="text-sm mb-2">{getTestTypeDescription()}</p>
          <p className="text-xs text-gray-500">
            Click the button above to create or update the {testType} test files in the main editor.
          </p>
          <p className="text-xs text-gray-500 mt-2">File will be created as:</p>
          <ul className="text-xs text-gray-600 dark:text-gray-400 mt-1 list-disc list-inside">
            <li>
              test-{testType}.{getExtensionForSelectedLanguage(fileSelectedLanguage || selectedLanguage)}
            </li>
          </ul>
        </div>
      )}
    </div>
  )
}

export function Terminal({
  isDarkTheme,
  isExecuting,
  output,
  input,
  setInput,
  waitingForInput,
  inputPrompt,
  terminalHeight,
  handleTerminalResize,
  handleTerminalInput,
  handleExecute,
  handleStopExecution,
  clearTerminalOnRun,
  setClearTerminalOnRun,
  updateSourceCode,
  terminalInputRef,
  isCompiled,
  activeTab,
  setActiveTab,
  showTests,
  testCases,
  activeFileId,
  addTestCase,
  removeTestCase,
  updateTestCase,
  testResults,
  runTests,
  isEditing = true,
  addSolutionTemplate,
  fileSelectedLanguage,
  addCustomTestFiles,
}: TerminalProps) {
  const terminalRef = useRef<HTMLDivElement>(null)
  const [showTestTypeDialog, setShowTestTypeDialog] = useState(false)
  const [selectedLanguage, setSelectedLanguage] = useState<ProgrammingLanguage>("javascript")
  const [availableLanguages, setAvailableLanguages] = useState<ProgrammingLanguage[]>(["javascript"])
  const [selectedTestIndex, setSelectedTestIndex] = useState(0)
  const [showAlertDialog, setShowAlertDialog] = useState(false)
  const [alertMessage, setAlertMessage] = useState("")
  const [isTestMode, setIsTestMode] = useState(false)
  const [lastRespondedPrompt, setLastRespondedPrompt] = useState<number>(-1)

  const [beforeCodeHeight, setBeforeCodeHeight] = useState(150)
  const [afterCodeHeight, setAfterCodeHeight] = useState(150)
  const [setupCodeHeight, setSetupCodeHeight] = useState(150)
  const [validationCodeHeight, setValidationCodeHeight] = useState(150)

  // Initialize the global current test index if it doesn't exist
  useEffect(() => {
    if (typeof window.__currentTestIndex === "undefined") {
      window.__currentTestIndex = -1
    }
  }, [])

  // Effect to reset the selected test index when the active file changes:
  useEffect(() => {
    setSelectedTestIndex(0)
  }, [activeFileId])

  // Effect to update selected language when fileSelectedLanguage changes
  useEffect(() => {
    if (
      fileSelectedLanguage &&
      availableLanguages.includes(fileSelectedLanguage) &&
      selectedLanguage === "javascript"
    ) {
      setSelectedLanguage(fileSelectedLanguage)
    }
  }, [fileSelectedLanguage, availableLanguages, selectedLanguage])

  // Check for alert messages
  useEffect(() => {
    if (window.__awaitingAlertAck && window.__alertMessage) {
      setAlertMessage(window.__alertMessage)
      setShowAlertDialog(true)
    }
  }, [output])

  // Auto-insert test input when prompt is waiting
  useEffect(() => {
    let timer: NodeJS.Timeout | null = null // Define timer outside the if block
    const fileCases = testCases[activeFileId]

    if (isTestMode && window.__awaitingPromptInput && fileCases && fileCases.length > 0) {
      // Use the current test index from the test runner if available, otherwise use the selected index
      const testIndex = window.__currentTestIndex >= 0 ? window.__currentTestIndex : selectedTestIndex

      // Only respond if this is a new prompt or a different test
      if (testIndex !== lastRespondedPrompt) {
        const currentTest = fileCases[testIndex]

        if (currentTest && Array.isArray(currentTest.args) && currentTest.args.length > 0) {
          const firstArg = currentTest.args[0]
          const testInput =
            typeof firstArg === "string" ? firstArg : JSON.stringify(firstArg)

          // Set the input value silently
          setInput(testInput)

          // Auto-submit after a short delay
          timer = setTimeout(() => {
            if (terminalInputRef.current) {
              // Call the handler directly
              handleTerminalInput({
                key: "Enter",
                preventDefault: () => { },
                stopPropagation: () => { },
              } as React.KeyboardEvent<HTMLInputElement>)

              // Track that we've responded to this prompt
              setLastRespondedPrompt(testIndex)

              // Clear the input field after submission
              setTimeout(() => setInput(""), 50)
            }
          }, 100)
        }
      }
    }

    return () => {
      if (timer) {
        clearTimeout(timer)
      }
    }
  }, [
    isTestMode,
    window.__awaitingPromptInput,
    activeFileId,
    selectedTestIndex,
    testCases,
    setInput,
    handleTerminalInput,
    terminalInputRef,
    lastRespondedPrompt,
    window.__currentTestIndex,
  ])

  // Reset lastRespondedPrompt when starting a new test run
  useEffect(() => {
    if (!isExecuting) {
      setLastRespondedPrompt(-1)
    }
  }, [isExecuting])

  // Focus input when prompt is waiting
  useEffect(() => {
    if (window.__awaitingPromptInput && terminalInputRef.current) {
      terminalInputRef.current.focus()
    }
  }, [window.__awaitingPromptInput, terminalInputRef])

  // Handle alert dialog close
  const handleAlertClose = () => {
    if (window.alertCallback) {
      window.alertCallback()
    }
    setShowAlertDialog(false)
    setAlertMessage("")
  }

  // Format terminal output - with safety checks
  const terminalOutput = (output || []).map((line, index) => {
    const lineStr = typeof line === "string" ? line : String(line || "")

    if (lineStr.startsWith("ALERT:")) {
      return {
        id: `output-${index}`,
        type: "alert",
        content: lineStr.substring(7).trim(),
      }
    } else if (lineStr.startsWith("PROMPT:")) {
      return {
        id: `output-${index}`,
        type: "prompt",
        content: lineStr.substring(8).trim(),
      }
    } else if (lineStr.startsWith("CONFIRM:")) {
      return {
        id: `output-${index}`,
        type: "confirm",
        content: lineStr.substring(9).trim(),
      }
    } else if (lineStr.startsWith("Error:")) {
      return {
        id: `output-${index}`,
        type: "error",
        content: lineStr,
      }
    }

    return {
      id: `output-${index}`,
      type: "output",
      content: lineStr,
    }
  }) as TerminalOutput[]

  // Scroll to bottom when terminal output changes
  useEffect(() => {
    if (terminalRef.current) {
      terminalRef.current.scrollTop = terminalRef.current.scrollHeight
    }
  }, [terminalOutput])

  // Focus input when terminal is clicked
  const focusInput = () => {
    if (terminalInputRef.current) {
      terminalInputRef.current.focus()
    }
  }

  // Handle terminal submit
  const handleTerminalSubmit = () => {
    if (input.trim()) {
      // Add the input to the terminal output
      const newOutput = [...output, `$ ${input}`]

      // Process the command
      handleTerminalInput({
        key: "Enter",
        preventDefault: () => { },
      } as React.KeyboardEvent<HTMLInputElement>)
    }
  }

  // Clear terminal
  const handleClearTerminal = () => {
    updateSourceCode({ output: [] })
  }

  // Button style classes
  const buttonBaseClass = cn(
    "flex items-center justify-center px-2 py-1 rounded text-xs font-medium transition-colors",
    isDarkTheme
      ? "bg-gray-800 text-gray-200 hover:bg-gray-700 border border-gray-700"
      : "bg-gray-100 text-gray-800 hover:bg-gray-200 border border-gray-300",
  )

  const runButtonClass = cn(
    buttonBaseClass,
    "mr-1",
    isDarkTheme ? "bg-blue-900 hover:bg-blue-800 border-blue-800" : "bg-blue-100 hover:bg-blue-200 border-blue-200",
    isExecuting && "opacity-50 cursor-not-allowed",
  )

  const runTestButtonClass = cn(
    buttonBaseClass,
    "mr-1",
    isDarkTheme
      ? "bg-green-900 hover:bg-green-800 border-green-800"
      : "bg-green-100 hover:bg-green-200 border-green-200",
    (isExecuting || !(showTests && (testCases[activeFileId]?.length ?? 0) > 0)) && "opacity-50 cursor-not-allowed",
  )

  const stopButtonClass = cn(
    buttonBaseClass,
    "ml-1",
    isDarkTheme ? "bg-red-900 hover:bg-red-800 border-red-800" : "bg-red-100 hover:bg-red-200 border-red-200",
  )

  // Helper function to get custom code for a specific language
  const getCustomCode = (
    testCase: SourceCodeTestCase | null | undefined,
    codeType: "customCodeFirst" | "customCodeSecond",
    language: ProgrammingLanguage,
    index = 0,
  ): string => {
    if (!testCase) return ""

    // If it's a string, return it (backward compatibility)
    if (typeof testCase[codeType] === "string") {
      return testCase[codeType]
    }

    // If it's an object with language keys, return the language-specific code
    if (testCase[codeType] && typeof testCase[codeType] === "object") {
      return testCase[codeType][language] || ""
    }

    // For customCodeFirst, also check the legacy customCode field
    if (codeType === "customCodeFirst" && typeof testCase.customCode === "string") {
      return testCase.customCode
    }

    // Return default template if nothing is found, but only for the first test case
    // For subsequent test cases, return empty string to preserve existing code
    if (index === 0) {
      return codeType === "customCodeFirst"
        ? DEFAULT_FIRST_CODE_TEMPLATES[language]
        : DEFAULT_SECOND_CODE_TEMPLATES[language]
    } else {
      // For non-first test cases, try to get code from the first test case
      const firstTestCase = testCases[activeFileId]?.[0]
      if (firstTestCase) {
        if (typeof firstTestCase[codeType] === "string") {
          return firstTestCase[codeType] as string
        } else if (firstTestCase[codeType] && typeof firstTestCase[codeType] === "object") {
          return (firstTestCase[codeType] as Record<ProgrammingLanguage, string>)[language] || ""
        } else if (codeType === "customCodeFirst" && typeof firstTestCase.customCode === "string") {
          return firstTestCase.customCode
        }
      }
      return ""
    }
  }

  // Helper function to update custom code for a specific language
  const updateCustomCode = (
    fileId: string,
    index: number,
    codeType: "customCodeFirst" | "customCodeSecond",
    language: ProgrammingLanguage,
    code: string,
  ) => {
    const testCase = testCases[fileId]?.[index]
    if (!testCase) return

    // Create a new object for the custom code if it doesn't exist or is a string
    let updatedCode: Record<ProgrammingLanguage, string> = {
      javascript: "",
      typescript: "",
      python: "",
      lua: "",
      c: "",
      cpp: "",
      h: "",
      hpp: ""
    }

    // If it's already an object, use it as the base
    if (testCase[codeType] && typeof testCase[codeType] === "object") {
      updatedCode = { ...(testCase[codeType] as Record<ProgrammingLanguage, string>) }
    }
    // If it's a string, convert it to an object with the current language
    else if (typeof testCase[codeType] === "string") {
      // Only preserve the string value for the current language
      updatedCode[language] = testCase[codeType] as string
    }
    // For customCodeFirst, also check the legacy customCode field
    else if (codeType === "customCodeFirst" && typeof testCase.customCode === "string") {
      updatedCode[language] = testCase.customCode
    }

    // Update the code for the current language
    updatedCode[language] = code

    // Update the test case
    const update: Partial<SourceCodeTestCase> = {}
    update[codeType] = updatedCode

    // For backward compatibility, also update customCode if we're updating customCodeFirst
    if (codeType === "customCodeFirst") {
      update.customCode = code
    }

    updateTestCase(fileId, index, update)
  }

  // Helper function to get test-specific code for a specific language
  const getTestSpecificCode = (
    testCase: SourceCodeTestCase | null | undefined,
    codeType: "testSpecificFirst" | "testSpecificSecond",
    language: ProgrammingLanguage,
  ): string => {
    if (!testCase) return ""

    // If it's a string, return it
    if (typeof testCase[codeType] === "string") {
      return testCase[codeType]
    }

    // If it's an object with language keys, return the language-specific code
    if (testCase[codeType] && typeof testCase[codeType] === "object") {
      return testCase[codeType][language] || ""
    }

    // Return empty string if nothing is found
    return ""
  }

  // Helper function to update test-specific code for a specific language
  const updateTestSpecificCode = (
    fileId: string,
    index: number,
    codeType: "testSpecificFirst" | "testSpecificSecond",
    language: ProgrammingLanguage,
    code: string,
  ) => {
    const testCase = testCases[activeFileId]?.[index]
    if (!testCase) return

    // Create a new object for the test-specific code if it doesn't exist or is a string
    let updatedCode: Record<ProgrammingLanguage, string> = {
      javascript: "",
      typescript: "",
      python: "",
      lua: "",
      c: "",
      cpp: "",
      h: "",
      hpp: ""
    }

    // If it's already an object, use it as the base
    if (testCase[codeType] && typeof testCase[codeType] === "object") {
      updatedCode = { ...(testCase[codeType] as Record<ProgrammingLanguage, string>) }
    }
    // If it's a string, convert it to an object with the current language
    else if (typeof testCase[codeType] === "string") {
      updatedCode[language] = testCase[codeType] as string
    }

    // Update the code for the current language
    updatedCode[language] = code

    // Update the test case
    const update: Partial<SourceCodeTestCase> = {}
    update[codeType] = updatedCode

    updateTestCase(fileId, index, update)
  }

  // Effect to determine available languages for the current file
  useEffect(() => {
    // This is a simplified example - in a real implementation, you would
    // determine the available languages from the file's languageContent
    const langs: ProgrammingLanguage[] = ["javascript", "typescript", "python", "lua", "c", "cpp", "h", "hpp"]
    setAvailableLanguages(langs)

    // Set the selected language to the first available language
    const firstLang = langs[0]
    if (langs.length > 0 && firstLang && !langs.includes(selectedLanguage)) {
      setSelectedLanguage(firstLang)
    }
  }, [activeFileId, selectedLanguage])

  return (
    <TooltipProvider>
      <div className="border-t">
        {/* Alert Dialog */}
        <Dialog open={showAlertDialog} onOpenChange={setShowAlertDialog}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle className="flex items-center">
                <AlertTriangle className="h-5 w-5 mr-2 text-yellow-500" />
                Alert
              </DialogTitle>
            </DialogHeader>
            <div className="py-4">
              <p>{alertMessage}</p>
            </div>
            <DialogFooter>
              <Button type="button" onClick={handleAlertClose}>
                OK
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Tab Header */}
        <div
          className={cn("flex border-b", isDarkTheme ? "border-gray-700 bg-gray-900" : "border-gray-200 bg-gray-50")}
        >
          <button
            className={cn(
              "px-4 py-2 text-xs font-medium flex items-center transition-colors",
              activeTab === "terminal"
                ? cn("border-b-2 border-blue-500", isDarkTheme ? "text-white" : "text-black")
                : cn(isDarkTheme ? "text-gray-400 hover:text-gray-300" : "text-gray-600 hover:text-gray-800"),
            )}
            onClick={() => setActiveTab("terminal")}
          >
            <TerminalIcon className="h-4 w-4 mr-2" />
            Terminal
          </button>
          {showTests && (
            <button
              className={cn(
                "px-4 py-2 text-xs font-medium flex items-center transition-colors",
                activeTab === "tests"
                  ? cn("border-b-2 border-blue-500", isDarkTheme ? "text-white" : "text-black")
                  : cn(isDarkTheme ? "text-gray-400 hover:text-gray-300" : "text-gray-600 hover:text-gray-800"),
              )}
              onClick={() => setActiveTab("tests")}
            >
              <CheckSquare className="h-4 w-4 mr-2" />
              Tests
            </button>
          )}
        </div>

        {/* Terminal Tab */}
        {activeTab === "terminal" && (
          <>
            <div
              className={cn("p-2 flex items-center", isDarkTheme ? "bg-black text-white" : "bg-gray-200 text-black")}
            >
              <TerminalIcon className="h-4 w-4 mr-2" />
              <span className="text-xs font-medium">Terminal</span>
              <div className="flex-grow" />
              <div className="flex items-center space-x-2">
                <Tooltip>
                  <TooltipTrigger render={<button onClick={() => {
                        setIsTestMode(false)
                        handleExecute()
                      }}
                      className={runButtonClass}
                      aria-label="Run code"
                      disabled={isExecuting} />}>
                    <Play className="h-3 w-3 mr-1" />
                    Run
                  </TooltipTrigger>
                  <TooltipContent>
                    <p className="text-xs">Run code</p>
                  </TooltipContent>
                </Tooltip>
                <Tooltip>
                  <TooltipContent>
                    <p className="text-xs">Run code and execute tests</p>
                  </TooltipContent>
                </Tooltip>
                {isExecuting && (
                  <Tooltip>
                    <TooltipTrigger render={<button onClick={handleStopExecution} className={stopButtonClass} aria-label="Stop execution" />}>
                      <Square className="h-3 w-3 mr-1" />
                      Stop
                    </TooltipTrigger>
                    <TooltipContent>
                      <p className="text-xs">Stop execution</p>
                    </TooltipContent>
                  </Tooltip>
                )}
                <Tooltip>
                  <TooltipTrigger render={<div className="flex items-center ml-2 text-xs" />}>
                    <div className="flex items-center space-x-2">
                      <Switch
                        id="clear-on-run"
                        checked={clearTerminalOnRun}
                        onCheckedChange={setClearTerminalOnRun}
                        className="scale-75"
                      />
                      <Label htmlFor="clear-on-run" className="text-xs cursor-pointer">
                        Clear on run
                      </Label>
                    </div>
                  </TooltipTrigger>
                  <TooltipContent>
                    <p className="text-xs">Clear terminal automatically when running code</p>
                  </TooltipContent>
                </Tooltip>
              </div>
            </div>
            <div
              ref={terminalRef}
              className={cn(
                "h-40 overflow-y-auto p-2 font-mono text-xs",
                isDarkTheme ? "bg-black text-white" : "bg-white text-black",
              )}
              style={{ height: `${terminalHeight}px` }}
              onClick={focusInput}
            >
              {terminalOutput.map((output) => (
                <div key={output.id} className="whitespace-pre-wrap mb-1">
                  {output.type === "input" && <span className="text-green-500">$ {output.content}</span>}
                  {output.type === "output" && (
                    <span>
                      {Array.isArray(output.content)
                        ? output.content.map((line, i) => <div key={i}>{String(line)}</div>)
                        : String(output.content)}
                    </span>
                  )}
                  {output.type === "error" && <span className="text-red-500">{String(output.content)}</span>}
                  {output.type === "system" && <span className="text-blue-400">{String(output.content)}</span>}
                  {output.type === "alert" && (
                    <div className="flex items-center text-yellow-500">
                      <AlertTriangle className="h-3 w-3 mr-1" />
                      <span>{String(output.content)}</span>
                    </div>
                  )}
                  {output.type === "prompt" && (
                    <div className="flex items-center text-blue-500">
                      <MessageSquare className="h-3 w-3 mr-1" />
                      <span>{String(output.content)}</span>
                    </div>
                  )}
                  {output.type === "confirm" && (
                    <div className="flex items-center text-green-500">
                      <HelpCircle className="h-3 w-3 mr-1" />
                      <span>{String(output.content)}</span>
                    </div>
                  )}
                </div>
              ))}
              <div className="flex items-center">
                <span
                  className={cn(
                    "mr-2",
                    window.__awaitingAlertAck
                      ? "text-yellow-500"
                      : window.__awaitingPromptInput
                        ? "text-blue-500"
                        : window.__awaitingConfirmInput
                          ? "text-green-500"
                          : "text-green-500",
                  )}
                >
                  {window.__awaitingAlertAck
                    ? "!"
                    : window.__awaitingPromptInput
                      ? ">"
                      : window.__awaitingConfirmInput
                        ? "?"
                        : "$"}
                </span>
                <input
                  ref={terminalInputRef}
                  type="text"
                  value={typeof input === "string" ? input : ""}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={handleTerminalInput}
                  className={cn(
                    "bg-transparent border-none outline-none flex-grow text-xs font-mono",
                    isDarkTheme ? "text-white" : "text-black",
                  )}
                  placeholder={
                    window.__awaitingAlertAck
                      ? "Press Enter to acknowledge..."
                      : window.__awaitingPromptInput
                        ? window.__promptMessage || "Enter your response..."
                        : window.__awaitingConfirmInput
                          ? window.__confirmMessage || "Confirm (y/n)..."
                          : "Type a command..."
                  }
                  disabled={
                    isExecuting &&
                    !(window.__awaitingPromptInput || window.__awaitingConfirmInput || window.__awaitingAlertAck)
                  }
                  autoFocus
                />
              </div>
            </div>
          </>
        )}

        {/* Tests Tab */}
        {activeTab === "tests" && showTests && (
          <>
            <div
              className={cn("p-2 flex items-center", isDarkTheme ? "bg-black text-white" : "bg-gray-200 text-black")}
            >
              <CheckSquare className="h-4 w-4 mr-2" />
              <span className="text-xs font-medium">Tests</span>
              <div className="flex-grow" />
              {isEditing && (
                <>
                  <Tooltip>
                    <TooltipTrigger render={<button onClick={() => {
                          if (!testCases[activeFileId] || testCases[activeFileId].length === 0) {
                            // First test case, show dialog to select type
                            setShowTestTypeDialog(true)
                          } else {
                            // Check if a custom, function, or console test already exists
                            const hasCustomTest = testCases[activeFileId].some((test) => test.type === "custom")
                            const hasFunctionTest = testCases[activeFileId].some((test) => test.type === "function")
                            const hasConsoleTest = testCases[activeFileId].some((test) => test.type === "console")

                            // If a custom, function, or console test exists, don't allow adding more tests
                            if (hasCustomTest || hasFunctionTest || hasConsoleTest) {
                              return
                            }

                            // Use the same type as existing tests
                            const firstTest = testCases[activeFileId]?.[0]
                            const existingType = firstTest?.type || "custom"
                            addTestCase(activeFileId, existingType)
                          }
                        }}
                        className={cn(
                          buttonBaseClass,
                          // Add opacity if a custom or function test exists
                          testCases[activeFileId]?.some(
                            (test) => test.type === "custom" || test.type === "function" || test.type === "console",
                          )
                            ? "opacity-50 cursor-not-allowed"
                            : "",
                        )}
                        aria-label="Add test case"
                        disabled={testCases[activeFileId]?.some(
                          (test) => test.type === "custom" || test.type === "function" || test.type === "console",
                        )} />}>
                      <Plus className="h-3 w-3 mr-1" />
                      Add Test
                    </TooltipTrigger>
                    <TooltipContent>
                      <p className="text-xs">
                        {testCases[activeFileId]?.some(
                          (test) => test.type === "custom" || test.type === "function" || test.type === "console",
                        )
                          ? "Only one custom/function/console test allowed per file"
                          : "Add test case"}
                      </p>
                    </TooltipContent>
                  </Tooltip>
                  <Tooltip>
                    <TooltipTrigger render={<button onClick={() => addSolutionTemplate?.()}
                        className={buttonBaseClass}
                        aria-label="Add solution template" />}>
                      <Plus className="h-3 w-3 mr-1" />
                      Add Template
                    </TooltipTrigger>
                    <TooltipContent>
                      <p className="text-xs">Add solution template to file</p>
                    </TooltipContent>
                  </Tooltip>
                </>
              )}
              <Tooltip>
                <TooltipContent>
                  <p className="text-xs">Run tests</p>
                </TooltipContent>
              </Tooltip>
            </div>
            <div
              className={cn("flex flex-col h-40", isDarkTheme ? "bg-black text-white" : "bg-white text-black")}
              style={{ height: `${terminalHeight}px` }}
            >
              {!testCases[activeFileId] || testCases[activeFileId].length === 0 ? (
                <div className="text-center text-gray-500 text-xs py-4">
                  No test cases defined. Click the + button to add a test case.
                </div>
              ) : (
                <>
                  {/* Content for all tests */}
                  <div className="overflow-y-auto p-2 flex-1">
                    {testCases[activeFileId].map((testCase, index) => (
                      <div key={index} className="border rounded p-2 mb-2">
                        <div className="flex items-center justify-between mb-2">
                          <div className="flex items-center gap-2">
                            {/* Remove the span element that displays the test type and number */}
                            {/* Display test result status if available - only for non-custom tests */}
                            {testCase.type !== "custom" && testResults[activeFileId]?.[index] && (
                              <span
                                className={cn(
                                  "px-1.5 py-0.5 text-xs rounded-full",
                                  testResults[activeFileId][index].passed
                                    ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400"
                                    : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
                                )}
                              >
                                {testResults[activeFileId][index].passed ? "Passed" : "Failed"}
                              </span>
                            )}
                          </div>
                          {isEditing && (
                            <button
                              onClick={() => removeTestCase(activeFileId, index)}
                              className={cn(
                                "p-1 rounded",
                                isDarkTheme
                                  ? "bg-red-900 hover:bg-red-800 text-white"
                                  : "bg-red-100 hover:bg-red-200 text-red-800",
                              )}
                              aria-label="Remove test case"
                            >
                              <X className="h-3 w-3" />
                            </button>
                          )}
                        </div>

                        {testCase.type === "custom" || testCase.type === "function" || testCase.type === "console" ? (
                          <CustomTestUI
                            testCase={testCase}
                            index={index}
                            activeFileId={activeFileId}
                            updateTestCase={updateTestCase}
                            isEditing={isEditing}
                            isDarkTheme={isDarkTheme}
                            fileSelectedLanguage={fileSelectedLanguage}
                            selectedLanguage={selectedLanguage}
                            addCustomTestFiles={addCustomTestFiles}
                            testType={testCase.type}
                          />
                        ) : (
                          // In/Out Test UI
                          <div>
                            <div className="flex items-center justify-between mb-1">
                              <label className="block text-xs">Arguments:</label>
                              {isEditing && (
                                <button
                                  onClick={() => {
                                    const currentArgs = Array.isArray(testCase.args) ? testCase.args : []
                                    updateTestCase(activeFileId, index, { args: [...currentArgs, ""] })
                                  }}
                                  className={cn(
                                    "p-1 rounded text-xs flex items-center",
                                    isDarkTheme
                                      ? "bg-blue-900 hover:bg-blue-800 text-white"
                                      : "bg-blue-100 hover:bg-blue-200 text-blue-800",
                                  )}
                                >
                                  <Plus className="h-3 w-3 mr-1" />
                                  Add Argument
                                </button>
                              )}
                            </div>

                            {!testCase.args || !Array.isArray(testCase.args) || testCase.args.length === 0 ? (
                              <div className="text-xs text-gray-500 italic mb-2">
                                No arguments added. Click "Add Argument" to add input values.
                              </div>
                            ) : (
                              <div className="space-y-2 mb-2">
                                {testCase.args.map((arg, argIndex) => (
                                  <div key={argIndex} className="flex items-center gap-2">
                                    <input
                                      value={typeof arg === "string" ? arg : JSON.stringify(arg)}
                                      onChange={(e) => {
                                        try {
                                          const newArgs = [...(Array.isArray(testCase.args) ? testCase.args : [])]
                                          // Verifica se o valor começa e termina com aspas para preservá-las como string literal
                                          if (
                                            e.target.value.trim().startsWith('"') &&
                                            e.target.value.trim().endsWith('"')
                                          ) {
                                            newArgs[argIndex] = e.target.value
                                          } else {
                                            // Try to parse as JSON first
                                            if (e.target.value.trim()) {
                                              try {
                                                newArgs[argIndex] = JSON.parse(e.target.value)
                                              } catch {
                                                // If not valid JSON, store as string
                                                newArgs[argIndex] = e.target.value
                                              }
                                            } else {
                                              newArgs[argIndex] = ""
                                            }
                                          }
                                          updateTestCase(activeFileId, index, { args: newArgs })
                                        } catch (err) {
                                          console.error("Error updating argument:", err)
                                        }
                                      }}
                                      className={cn(
                                        "flex-grow p-1 text-xs font-mono border rounded",
                                        isDarkTheme ? "bg-gray-900 border-gray-700" : "bg-white border-gray-300",
                                        !isEditing && "opacity-90",
                                      )}
                                      placeholder={`Argument ${argIndex + 1}`}
                                      readOnly={!isEditing}
                                    />
                                    {isEditing && (
                                      <button
                                        onClick={() => {
                                          const newArgs = [...(Array.isArray(testCase.args) ? testCase.args : [])]
                                          newArgs.splice(argIndex, 1)
                                          updateTestCase(activeFileId, index, { args: newArgs })
                                        }}
                                        className={cn(
                                          "p-1 rounded",
                                          isDarkTheme
                                            ? "bg-red-900 hover:bg-red-800 text-white"
                                            : "bg-red-100 hover:bg-red-200 text-red-800",
                                        )}
                                      >
                                        <X className="h-3 w-3" />
                                      </button>
                                    )}
                                  </div>
                                ))}
                              </div>
                            )}
                            <div className="mt-3">
                              <label className="block text-xs mb-1">Expected Return:</label>
                              <input
                                value={
                                  testCase.expectedReturn &&
                                    Array.isArray(testCase.expectedReturn) &&
                                    testCase.expectedReturn.length > 0
                                    ? typeof testCase.expectedReturn[0] === "string"
                                      ? testCase.expectedReturn[0]
                                      : JSON.stringify(testCase.expectedReturn[0])
                                    : ""
                                }
                                onChange={(e) => {
                                  try {
                                    // Verifica se o valor começa e termina com aspas para preservá-las como string literal
                                    if (e.target.value.trim().startsWith('"') && e.target.value.trim().endsWith('"')) {
                                      updateTestCase(activeFileId, index, { expectedReturn: [e.target.value] })
                                    } else {
                                      // Try to parse as JSON first
                                      if (e.target.value.trim()) {
                                        try {
                                          const parsedValue = JSON.parse(e.target.value)
                                          updateTestCase(activeFileId, index, { expectedReturn: [parsedValue] })
                                        } catch {
                                          // If not valid JSON, store as string
                                          updateTestCase(activeFileId, index, { expectedReturn: [e.target.value] })
                                        }
                                      } else {
                                        updateTestCase(activeFileId, index, { expectedReturn: [] })
                                      }
                                    }
                                  } catch (err) {
                                    console.error("Error updating return value:", err)
                                  }
                                }}
                                className={cn(
                                  "w-full p-1 text-xs font-mono border rounded",
                                  isDarkTheme ? "bg-gray-900 border-gray-700" : "bg-white border-gray-300",
                                  !isEditing && "opacity-90",
                                )}
                                placeholder="Expected return value"
                                readOnly={!isEditing}
                              />
                            </div>
                          </div>
                        )}
                        {/* Test Results Section */}
                        {testCase.type !== "custom" && testResults[activeFileId]?.[index] && (
                          <div className="mt-4 border-t pt-2">
                            <h4 className="text-xs font-medium mb-2">Test Results:</h4>
                            <div
                              className={cn(
                                "p-2 rounded text-xs font-mono whitespace-pre-wrap",
                                testResults[activeFileId][index].passed
                                  ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400"
                                  : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
                              )}
                            >
                              <div className="font-semibold">
                                {testResults[activeFileId][index].passed ? "✓ Test Passed" : "✗ Test Failed"}
                              </div>
                              {!testResults[activeFileId][index].passed && (
                                <>
                                  <div className="mt-1">
                                    <span className="font-semibold">Expected:</span>
                                    <pre className="mt-1 p-1 bg-white/50 dark:bg-black/50 rounded overflow-x-auto">
                                      {testResults[activeFileId][index].expected}
                                    </pre>
                                  </div>
                                  <div className="mt-1">
                                    <span className="font-semibold">Actual:</span>
                                    <pre className="mt-1 p-1 bg-white/50 dark:bg-black/50 rounded overflow-x-auto">
                                      {testResults[activeFileId][index].actual}
                                    </pre>
                                  </div>
                                </>
                              )}
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </>
        )}
        {/* Test Type Dialog */}
        <TestTypeDialog
          isOpen={showTestTypeDialog}
          setIsOpen={setShowTestTypeDialog}
          onSelect={(type) => {
            addTestCase(activeFileId, type)
            setShowTestTypeDialog(false)
          }}
          hasCustomTest={testCases[activeFileId]?.some((test) => test.type === "custom")}
          hasFunctionTest={testCases[activeFileId]?.some((test) => test.type === "function")}
          hasConsoleTest={testCases[activeFileId]?.some((test) => test.type === "console")}
        />
      </div>
    </TooltipProvider>
  )
}
