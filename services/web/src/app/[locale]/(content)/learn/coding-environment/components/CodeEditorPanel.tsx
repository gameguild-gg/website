"use client"

import { useState, useEffect, useRef, useCallback } from "react"
import dynamic from "next/dynamic"
import { QuestionStatus, type CodeQuestionv1_0_0 } from "@/lib/interface-base/question.base.v1.0.0"
import type { CodeQuestionSubmissionv1_0_0 } from "@/lib/interface-base/question.submission.v1.0.0"
import TopMenu from "./TopMenu"
import BottomMenu from "./BottomMenu"
import CodeTabs from "./CodeTabs"
import TestResultsTab from "./TestResultsTab"
import LessonPanel from "./LessonPanel"
import { CharacterCount } from "./CharacterCount"
import { Panel, PanelGroup, PanelResizeHandle, type ImperativePanelHandle } from "react-resizable-panels"
import JSZip from "jszip"
import { toast } from "@/components/learn/ui/use-toast"
import { SubmissionWarningDialog } from "./SubmissionWarningDialog"
import { type History } from "../types/codeEditor"
import { Undo, Redo, AlertCircle } from "lucide-react"
import * as monaco from "monaco-editor"
import SettingsModal, { type Settings } from "./SettingsModal"
//import { EditorPanel } from "./EditorPanel"
import { loader } from "@monaco-editor/react"
import { insertInputs } from "./InputInserter"
import { ConfirmSubmissionDialog } from "./ConfirmSubmissionDialog"
import { AbortController } from "node-abort-controller"
import { SplitViewFileSelector } from "./SplitViewFileSelector"
import { Alert, AlertDescription, AlertTitle } from "@/components/learn/ui/alert"
import { useRouter, useSearchParams } from "next/navigation"
import type { HierarchyBasev1_0_0 } from "@/lib/interface-base/structure.base.v1.0.0"
import type { UserListQuestionv1_0_0, StudentSubmission } from "@/lib/interface-base/user.list.question.v1.0.0"
import FileExplorer from "./FileExplorer" // Import FileExplorer component
//import type { CodeQuestionv1_0_0 } from "@/lib/interface-base/question.base.v1.0.0" // Import CodeQuestionv1_0_0
import { FolderUp } from "lucide-react" // Added import
import Image from "next/image"


interface CodeFile {
  id: string
  name: string
  language: string
  content: string
  history: History
}

const getFileCharLimit = (fileName: string, rules: string[]): number => {
  const fileSpecificRule = rules.find((rule) => rule.startsWith(`chars-${fileName}:`))
  if (fileSpecificRule) {
    return Number.parseInt(fileSpecificRule.split(":")[1].trim())
  }
  return 0 // Return 0 if no specific limit is set for this file
}

const saveStateToLocalStorage = (
  state: any,
  courseId: string | null,
  moduleId: string | null,
  assessmentId: string | null,
) => {
  if (!courseId || !moduleId || !assessmentId || !state.assignment) return

  const { id } = state.assignment
  const storageKey = `assignmentState_${courseId}_${moduleId}_${assessmentId}_${id}`

  // Filter out deleted files
  const updatedCodeFiles = state.codeFiles.filter((file) =>
    state.globalCodeFiles.some((globalFile) => globalFile.id === file.id),
  )

  const stateToSave = {
    ...state,
    codeFiles: updatedCodeFiles,
    globalCodeFiles: state.globalCodeFiles,
  }

  localStorage.setItem(storageKey, JSON.stringify(stateToSave))
}

const MonacoEditor = dynamic(() => import("@monaco-editor/react"), { ssr: false })

interface CodeEditorPanelProps {
  assignment: CodeQuestionv1_0_0
  onReturn: () => void
  onSubmit: (submission: CodeQuestionSubmissionv1_0_0) => void
  hierarchy?: HierarchyBasev1_0_0
  isSandboxMode: boolean
}

const languageMap: { [key: string]: string } = {
  js: "javascript",
  jsx: "javascript",
  ts: "typescript",
  tsx: "typescript",
  py: "python",
  java: "java",
  cpp: "cpp",
  c: "c",
  go: "go",
  html: "html",
  css: "css",
  md: "markdown",
  json: "json",
  yaml: "yaml",
  xml: "xml",
  rb: "ruby",
  rs: "rust",
  cs: "csharp",
  svg: "svg",
  png: "bitmap",
  jpg: "bitmap",
  jpeg: "bitmap",
  gif: "bitmap",
  bmp: "bitmap",
}

const getLanguageFromExtension = (filename: string): string => {
  const extension = filename.split(".").pop()?.toLowerCase() || ""
  return languageMap[extension] || "plaintext"
}

const DEFAULT_MAX_FILES = Infinity;
const DEFAULT_MAX_CHARS = Infinity;

const defaultSettings: Settings = {
  runSettings: {
    fileExtensions: {
      ".html": "Open new guide",
      ".js": "Internal output",
      ".jsx": "Internal output",
      ".ts": "Internal output",
      ".tsx": "Internal output",
      ".py": "Internal output",
      ".java": "Internal output",
      ".cpp": "Internal output",
      ".c": "Internal output",
    },
    applicationFileName: "index",
  },
}

// Add this function to calculate total character count
const calculateTotalChars = (files: CodeFile[], rules: string[]): number => {
  return files.reduce((sum, file) => {
    const fileLimit = getFileCharLimit(file.name, rules)
    return fileLimit === 0 ? sum + file.content.length : sum
  }, 0)
}

export default function CodeEditorPanel({ assignment, onSubmit, hierarchy = {
  idHierarchy: [],
  idUser: "",
  idRole: ""
}, isSandboxMode }: CodeEditorPanelProps) {
  const [globalCodeFiles, setGlobalCodeFiles] = useState<CodeFile[]>([]) // Global instance
  const [codeFiles, setCodeFiles] = useState<CodeFile[]>([]) // Local instance
  const [activeFileIndex, setActiveFileIndex] = useState(0)
  const [activeTab, setActiveTab] = useState<"output" | "problems" | "testResults" | null>(null)
  const [output, setOutput] = useState("")
  const [testResults, setTestResults] = useState<any[]>([])
  const [mode, setMode] = useState<"light" | "dark" | "high-contrast">(hierarchy.colorMode)
  const [submissionData, setSubmissionData] = useState<Partial<CodeQuestionSubmissionv1_0_0>>({
    submittedCode: [],
    output: "",
    testResults: [],
  })
  const editorRef = useRef<any>(null)
  const [currentFileChars, setCurrentFileChars] = useState(0)
  const [totalChars, setTotalChars] = useState(0)
  const [isWarningDialogOpen, setIsWarningDialogOpen] = useState(false)
  const [submissionConflicts, setSubmissionConflicts] = useState<{ fileName: string; reason: string }[]>([])
  const [isSubmitDisabled, setIsSubmitDisabled] = useState(false)
  const [lessonPanelSize, setLessonPanelSize] = useState(30)
  const [isLessonCollapsed, setIsLessonCollapsed] = useState(false)
  const [prevLessonPanelSize, setPrevLessonPanelSize] = useState(30)
  const lessonPanelRef = useRef<ImperativePanelHandle>(null)
  const [isSettingsOpen, setIsSettingsOpen] = useState(false)
  const [settings, setSettings] = useState<Settings>(defaultSettings)
  const [isUndoing, setIsUndoing] = useState(false)
  const [isRedoing, setIsRedoing] = useState(false)
  const undoTimerRef = useRef<NodeJS.Timeout | null>(null)
  const redoTimerRef = useRef<NodeJS.Timeout | null>(null)
  const [isConfirmDialogOpen, setIsConfirmDialogOpen] = useState(false)
  const [abortController, setAbortController] = useState<AbortController | null>(null)
  const [isSplitViewActive, setIsSplitViewActive] = useState(false)
  const [splitViewFileName, setSplitViewFileName] = useState<string | null>(null)
  const [isFileSelectorOpen, setIsFileSelectorOpen] = useState(false)
  const [openedWindow, setOpenedWindow] = useState<Window | null>(null)
  const [isRunAnimating, setIsRunAnimating] = useState(false)
  const [isRunThisAnimating, setIsRunThisAnimating] = useState(false)
  const [isRunAppAnimating, setIsRunAppAnimating] = useState(false)
  const [failedTests, setFailedTests] = useState(0)
  const [totalTests, setTotalTests] = useState(0)
  const [testResultsViewed, setTestResultsViewed] = useState(false) // Added state
  const initialIsSubmitHidden = assignment.rules.includes("submit: n")
  const [isSubmitHidden, setIsSubmitHidden] = useState(false) // Added state
  const [splitViewFileIndex, setSplitViewFileIndex] = useState<number | null>(null) // Added state
  const router = useRouter()
  const searchParams = useSearchParams()
  const assessmentId = searchParams.get("assessmentId")
  const userId = searchParams.get("userId")
  const role = searchParams.get("role")
  const courseId = searchParams.get("courseId")
  const moduleId = searchParams.get("moduleId")

  const [studentSubmission, setStudentSubmission] = useState<StudentSubmission | null>(null)
  const submissionId = searchParams.get("submissionId")

  const [pyodide, setPyodide] = useState<any>(null);

  if (!assignment) {
    return <div>Loading...</div>
  }

  // Modify the existing maxChars calculation
  const maxChars =
    assignment.rules && assignment.rules.find((rule) => rule.startsWith("chars:"))
      ? Number.parseInt(
          assignment.rules
            .find((rule) => rule.startsWith("chars:"))!
            .split(":")[1]
            .trim(),
        )
      : DEFAULT_MAX_CHARS

  const maxFiles =
    assignment.rules && assignment.rules.find((rule) => rule.startsWith("files:"))
      ? Number.parseInt(
          assignment.rules
            .find((rule) => rule.startsWith("files:"))!
            .split(":")[1]
            .trim(),
        )
      : DEFAULT_MAX_FILES

  const getFileCharLimit = (fileName: string, rules: string[]): number => {
    if (!assignment.rules) return maxChars
    const fileSpecificRule = assignment.rules.find((rule) => rule.startsWith(`chars-${fileName}:`))
    if (fileSpecificRule) {
      return Number.parseInt(fileSpecificRule.split(":")[1].trim())
    }
    return maxChars
  }

  useEffect(() => {
    if (!assignment || !courseId || !moduleId || !assessmentId) return

    const savedState = localStorage.getItem(`assignmentState_${courseId}_${moduleId}_${assessmentId}_${assignment.id}`)

    if (savedState) {
      const parsedState = JSON.parse(savedState)
      const loadedGlobalCodeFiles = parsedState.globalCodeFiles || []
      setGlobalCodeFiles(loadedGlobalCodeFiles)

      setOutput(parsedState.output || "")
      setTestResults(parsedState.testResults || [])
      setActiveFileIndex(-1) // Definir como -1 para indicar que nenhum arquivo está aberto
    } else {
      let initialFiles: CodeFile[] = []
      if (assignment.type === "code") {
        const codeQuestion = assignment as CodeQuestionv1_0_0
        initialFiles = codeQuestion.codeName.map(({ id, name }) => {
          const content = codeQuestion.initialCode[id] || ""
          const language = getLanguageFromExtension(name)
          return {
            id,
            name,
            language,
            content: codeQuestion.rules.includes("addinput")
              ? insertInputs(content, codeQuestion.inputs[id] || [], language, 0)
              : content,
            history: { past: [], future: [] },
          }
        })
      }

      setGlobalCodeFiles(initialFiles)

      // Inicializar character counts
      const initialTotalChars = calculateTotalChars(initialFiles, assignment.rules)
      setTotalChars(initialTotalChars)
      setCurrentFileChars(0) // Definir como 0 já que nenhum arquivo está aberto
    }

    setSubmissionData({
      //...assignment,  //comentar resolve para build, mas nao resolve para processo de submissao
      submittedCode: [],
      output: "",
      testResults: [],
    })

    // Verificar se o botão de submissão deve ser ocultado
    setIsSubmitHidden(assignment.rules.includes("submit: n"))
  }, [assignment, courseId, moduleId, assessmentId])

  useEffect(() => {
    const updateCharCounts = () => {
      const newTotalChars = calculateTotalChars(globalCodeFiles, assignment.rules)
      setTotalChars(newTotalChars)
      setCurrentFileChars(codeFiles[activeFileIndex]?.content.length || 0)
    }

    const intervalId = setInterval(updateCharCounts, 500)

    return () => clearInterval(intervalId)
  }, [globalCodeFiles, codeFiles, activeFileIndex, assignment.rules])

  useEffect(() => {
    const fetchData = async () => {
      if (submissionId) {
        try {
          const response = await fetch(`../../../../api/learn/teach/question/${assignment.id}?courseId=${courseId}`)
          if (!response.ok) {
            throw new Error("Failed to fetch student submission")
          }
          const data: UserListQuestionv1_0_0 = await response.json()
          const submission = data.submissions[submissionId]?.submission as StudentSubmission
          if (submission) {
            setStudentSubmission(submission)
            setGlobalCodeFiles(
              submission.submittedCode.map((content, index) => ({
                // Update global instance
                name: `file${index + 1}.js`,
                language: "javascript",
                content,
                history: { past: [], future: [] },
                id: `file${index + 1}`, // Add id
              })),
            )
            setCodeFiles(
              submission.submittedCode.map((content, index) => ({
                // Update local instance
                name: `file${index + 1}.js`,
                language: "javascript",
                content,
                history: { past: [], future: [] },
                id: `file${index + 1}`, // Add id
              })),
            )
          }
        } catch (error) {
          console.error("Error fetching student submission:", error)
          toast({
            title: "Error",
            description: "Failed to load student submission",
            variant: "destructive",
          })
        }
      }
    }

    fetchData()
  }, [assignment.id, submissionId, courseId])

  const handleEditorChange = (value: string | undefined) => {
    if (value !== undefined) {
      updateFileHistory(activeFileIndex, value)

      const fileLimit = getFileCharLimit(codeFiles[activeFileIndex].name, assignment.rules)
      const effectiveLimit = fileLimit === 0 ? maxChars : fileLimit
      if (value.length > effectiveLimit) {
        toast({
          title: "Character limit exceeded",
          description: `You have exceeded the maximum character limit of ${effectiveLimit} for this file. Please reduce the content.`,
          variant: "destructive",
        })
        return
      }

      const updatedCodeFiles = [...codeFiles]
      updatedCodeFiles[activeFileIndex].content = value
      setCodeFiles(updatedCodeFiles)

      // Update global code files
      const updatedGlobalCodeFiles = [...globalCodeFiles]
      const globalIndex = updatedGlobalCodeFiles.findIndex((file) => file.id === updatedCodeFiles[activeFileIndex].id)
      if (globalIndex !== -1) {
        updatedGlobalCodeFiles[globalIndex].content = value
        setGlobalCodeFiles(updatedGlobalCodeFiles)
      }

      // Update total character count
      const newTotalChars = calculateTotalChars(updatedCodeFiles, assignment.rules)
      setTotalChars(newTotalChars)
      setCurrentFileChars(value.length)

      setSubmissionData((prev) => ({
        ...prev,
        submittedCode: updatedCodeFiles.map((file) => file.content),
      }))

      // Trigger an immediate save
      const currentState = {
        assignment,
        codeFiles: updatedCodeFiles,
        output,
        testResults,
        activeFileIndex,
        globalCodeFiles: updatedGlobalCodeFiles,
      }
      saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId)
    }
  }

  const handleTabChange = (index: number) => {
    setActiveFileIndex(index)
  }

  const compareOutputs = (actualOutputs: string[], expectedOutputs: { [id: string]: string[] }): any[] => {
    return Object.entries(expectedOutputs).flatMap(([id, expectedFileOutputs]) => {
      const actualFileOutput = actualOutputs[codeFiles.findIndex((file) => file.id === id)] || ""
      const actualLines = actualFileOutput.trim().split("\n")

      if (!expectedFileOutputs || expectedFileOutputs.length === 0) {
        return [
          {
            fileId: id,
            expectedOutput: "No output expected",
            actualOutput: actualFileOutput,
            passed: actualFileOutput.trim() === "",
          },
        ]
      }

      return expectedFileOutputs.map((expected, index) => ({
        fileId: id,
        expectedOutput: expected,
        actualOutput: actualLines[index] || "",
        passed: actualLines[index]?.trim() === expected.trim(),
      }))
    })
  }

  const isWindowOpen = (window: Window | null): boolean => {
    return window !== null && !window.closed
  }

  // Load Pyodide on component mount
  const loadPyodideInstance = async () => {
    if (!pyodide) {
      const script = document.createElement('script');
      script.src = 'https://cdn.jsdelivr.net/pyodide/v0.27.0/full/pyodide.js';
      script.async = true;

      await new Promise((resolve, reject) => {
        script.onload = resolve;
        script.onerror = reject;
        document.body.appendChild(script);
      });

      // @ts-ignore - `loadPyodide` será disponibilizado globalmente pelo script
      const instance = await (window as any).loadPyodide();
      setPyodide(instance);
      return instance;
    }
    return pyodide;
  };

  const runFile = () => {
    setIsRunAnimating(true)
    if (abortController) {
      abortController.abort()
    }
    const newAbortController = new AbortController()
    setAbortController(newAbortController)

    const currentFile = codeFiles[activeFileIndex]
    const fileExtension = `.${currentFile.name.split(".").pop()}`
    const runType = settings.runSettings.fileExtensions[fileExtension] || "Internal output"

    setOutput("")
    setTestResults([])

    const openInNewTab = (content: string) => {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open()
        openedWindow!.document.write(content)
        openedWindow!.document.close()
      } else {
        const newWindow = window.open()
        if (newWindow) {
          newWindow.document.write(content)
          newWindow.document.close()
          setOpenedWindow(newWindow)
        }
      }
    }

    const handleInternalOutput = () => {
      if (currentFile.language === "javascript") {
        const capturedOutput: string[] = []
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
          error: (...args: any[]) => {
            const output = "Error: " + args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
        }

        try {
          const func = new Function("console", currentFile.content)
          func(customConsole)

          if (assignment.rules.includes("outputs: y") && !newAbortController.signal.aborted) {
            const results = compareOutputs([capturedOutput.join("\n")], {
              [currentFile.id]: assignment.outputs[currentFile.id] || [],
            })
            setTestResults(results)
          }
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`)
          }
        }
      } else if (currentFile.language === 'python') {
        const executePython = async (code: string) => {
          try {
            setOutput((prev) => prev + '\nLoading Python runtime...');

            const pyodideInstance = await loadPyodideInstance();

            setOutput((prev) =>
              prev.replace('\nLoading Python runtime...', ''),
            );

            // Variáveis para capturar o stdout e stderr
            let capturedOutput = '';
            let capturedError = '';

            // Redirecionar o stdout
            pyodideInstance.setStdout({
              batched: (output: string) => {
                capturedOutput += output;
                if (!newAbortController.signal.aborted) {
                  setOutput((prev) => prev + output);
                }
              },
            });

            // Redirecionar o stderr
            pyodideInstance.setStderr({
              batched: (error: string) => {
                capturedError += error;
                if (!newAbortController.signal.aborted) {
                  setOutput((prev) => prev + `Error: ${error}\n`);
                }
              },
            });

            // Executar o código Python
            await pyodideInstance.runPythonAsync(code);

            // Retornar o output e erro capturados
            return { capturedOutput, capturedError };
          } catch (error) {
            return {
              capturedOutput: '',
              capturedError: (error as Error).message,
            };
          }
        };

        const executeAndCapture = async () => {
          const currentCode = currentFile.content;
          console.log('Content: ' + currentCode);

          const { capturedOutput, capturedError } =
            await executePython(currentCode);

          console.log('Captured Output: ' + capturedOutput);
          console.log('Captured Error: ' + capturedError);

          // Adicionar o erro capturado ao output, se houver
          if (capturedError && !newAbortController.signal.aborted) {
            setOutput((prev) => prev + `\nCaptured Error:\n${capturedError}`);
          }

          if (
            assignment.rules.includes('outputs: y') &&
            !newAbortController.signal.aborted
          ) {
            const results = compareOutputs(
              [capturedOutput],
              assignment.outputs,
            );
            setTestResults(results);
          }
        };

        executeAndCapture().catch((error) => {
          if (!newAbortController.signal.aborted) {
            setOutput((prev) => prev + `\nUnhandled Error: ${error}\n`);
          }
        });

      } else if (currentFile.language === "html") {
        const processedHTML = processHTML(currentFile.content)
        if (!newAbortController.signal.aborted) {
          setOutput(`HTML content rendered in the output:\n\n${processedHTML}`)
        }
      } else if (currentFile.language === "svg") {
        if (!newAbortController.signal.aborted) {
          // Render SVG content directly in output
          setOutput(`SVG content rendered in the output:${currentFile.content}`)
        }
      } else if (currentFile.language === "bitmap") {
        if (!newAbortController.signal.aborted) {
          // Renderizar conteúdo de imagem bitmap diretamente na saída
          setOutput(`BITMAP_IMAGE:${currentFile.content}`)
        }
      } else {
        if (!newAbortController.signal.aborted) {
          setOutput(
            `Running ${currentFile.name}...\n\nContent:\n${currentFile.content}\n\nNote: Execution for ${currentFile.language} files is not implemented in this environment.`,
          )
        }
      }
      if (!newAbortController.signal.aborted) {
        handleSetActiveTab("output") // Updated
      }
    }

    if (runType === "Open new guide" || runType === "Both") {
      if (currentFile.language === "html") {
        const processedHTML = processHTML(currentFile.content)
        openInNewTab(processedHTML)
      } else if (currentFile.language === "bitmap") {
        // Abrir a imagem em uma nova aba
        const newWindow = window.open()
        if (newWindow) {
          newWindow.document.write(`<img src="${currentFile.content}" alt="Bitmap Image">`)
          newWindow.document.close()
        }
      } else {
        openInNewTab(currentFile.content)
      }
    }

    if (runType === "Internal output" || runType === "Both") {
      handleInternalOutput()
    }
    setTimeout(() => setIsRunAnimating(false), 1000) // Stop animation after 1 second
  }

  const runThis = () => {
    setIsRunThisAnimating(true)
    if (abortController) {
      abortController.abort()
    }
    const newAbortController = new AbortController()
    setAbortController(newAbortController)

    const currentFile = codeFiles[activeFileIndex]
    const fileExtension = `.${currentFile.name.split(".").pop()}`
    const runType = settings.runSettings.fileExtensions[fileExtension] || "Internal output"

    setOutput("")
    setTestResults([])

    const openInNewTab = (content: string) => {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open()
        openedWindow!.document.write(content)
        openedWindow!.document.close()
      } else {
        const newWindow = window.open()
        if (newWindow) {
          newWindow.document.write(content)
          newWindow.document.close()
          setOpenedWindow(newWindow)
        }
      }
    }

    const handleInternalOutput = () => {
      if (currentFile.language === "javascript") {
        const capturedOutput: string[] = []
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
          error: (...args: any[]) => {
            const output = "Error: " + args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
        }

        try {
          const func = new Function("console", currentFile.content)
          func(customConsole)
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`)
          }
        }
      } else if (currentFile.language === "html") {
        if (!newAbortController.signal.aborted) {
          setOutput(`HTML content (not rendered):\n\n${currentFile.content}`)
        }
      } else {
        if (!newAbortController.signal.aborted) {
          setOutput(
            `Running ${currentFile.name}...\n\nContent:\n${currentFile.content}\n\nNote: Execution for ${currentFile.language} files is not implemented in this environment.`,
          )
        }
      }
    }

    if (runType === "Open new guide" || runType === "Both") {
      openInNewTab(currentFile.content)
    }

    if (runType === "Internal output" || runType === "Both") {
      handleInternalOutput()
    }

    if (!newAbortController.signal.aborted) {
      handleSetActiveTab("output") // Updated
    }
    setTimeout(() => setIsRunThisAnimating(false), 1000) // Stop animation after 1 second
  }

  const processHTML = (html: string) => {
    const parser = new DOMParser()
    const doc = parser.parseFromString(html, "text/html")

    // Process CSS
    const styleLinks = doc.querySelectorAll('link[rel="stylesheet"]')
    styleLinks.forEach((link) => {
      const href = link.getAttribute("href")
      if (href) {
        const cssFile = codeFiles.find((file) => file.name === href)
        if (cssFile) {
          const style = doc.createElement("style")
          style.textContent = cssFile.content
          link.parentNode?.replaceChild(style, link)
        }
      }
    })

    // Process JS
    const scripts = doc.querySelectorAll("script")
    scripts.forEach((script) => {
      const src = script.getAttribute("src")
      if (src) {
        const jsFile = codeFiles.find((file) => file.name === src)
        if (jsFile) {
          script.removeAttribute("src")
          script.textContent = jsFile.content
        }
      }
    })

    return doc.documentElement.outerHTML
  }

  const runApplication = (fileName: string) => {
    setIsRunAppAnimating(true)
    if (abortController) {
      abortController.abort()
    }
    const newAbortController = new AbortController()
    setAbortController(newAbortController)

    const indexFileName = settings.runSettings.applicationFileName || fileName
    const indexFile = codeFiles.find((file) => file.name === indexFileName)

    if (!indexFile) {
      toast({
        title: "Application file not found",
        description: `Could not find the file "${indexFileName}". Please check your settings.`,
        variant: "destructive",
      })
      setIsRunAppAnimating(false)
      return
    }

    const indexExtension = `.${indexFile.name.split(".").pop()}`
    const outputType = settings.runSettings.fileExtensions[indexExtension] || "Internal output"

    setOutput("")
    setOutput(`Running application (${indexFile.name})...\n\n`)

    if (outputType === "Open new guide" || outputType === "Both") {
      if (isWindowOpen(openedWindow)) {
        openedWindow!.document.open()
        if (indexFile.name.endsWith(".html") || indexFile.name.endsWith(".svg")) {
          // Updated
          const processedHTML = processHTML(indexFile.content)
          openedWindow!.document.write(processedHTML)
        } else {
          openedWindow!.document.write(indexFile.content)
        }
        openedWindow!.document.close()
      } else {
        const newWindow = window.open()
        if (newWindow) {
          if (indexFile.name.endsWith(".html") || indexFile.name.endsWith(".svg")) {
            // Updated
            const processedHTML = processHTML(indexFile.content)
            newWindow.document.write(processedHTML)
          } else {
            newWindow.document.write(indexFile.content)
          }
          newWindow.document.close()
          setOpenedWindow(newWindow)
        }
      }
    }

    if (outputType === "Internal output" || outputType === "Both") {
      if (indexFile.name.endsWith(".html")) {
        const processedHTML = processHTML(indexFile.content)
        setOutput((prev) => prev + `Processed HTML:\n\n${processedHTML}\n`)
      } else {
        const capturedOutput: string[] = []
        const customConsole = {
          log: (...args: any[]) => {
            const output = args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
          error: (...args: any[]) => {
            const output = "Error: " + args.join(" ")
            capturedOutput.push(output)
            if (!newAbortController.signal.aborted) {
              setOutput((prev) => prev + output + "\n")
            }
          },
        }

        try {
          const func = new Function("console", indexFile.content)
          func(customConsole)

          if (assignment.rules.includes("outputs: y") && !newAbortController.signal.aborted) {
            const results = compareOutputs([capturedOutput.join("\n")], assignment.outputs)
            setTestResults(results)
          }
        } catch (error) {
          if (!newAbortController.signal.aborted) {
            setOutput(`Error executing JavaScript: ${error}\n`)
          }
        }
      }
    }

    if (!newAbortController.signal.aborted) {
      handleSetActiveTab("output") // Updated
    }
    setTimeout(() => setIsRunAppAnimating(false), 1000) // Stop animation after 1 second
  }

  const handleReturn = () => {
    if (isSandboxMode) {
      router.push("/") // Or wherever you want the sandbox to return to
    } else {
      const currentState = {
        codeFiles,
        output,
        testResults,
      }
      saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId) // Save state before returning
      router.back()
    }
  }

  const checkSubmissionConflicts = (): { fileName: string; reason: string }[] => {
    const conflicts: { fileName: string; reason: string }[] = []

    codeFiles.forEach((file) => {
      const fileLimit = getFileCharLimit(file.name, assignment.rules)
      if (file.content.length > fileLimit) {
        conflicts.push({
          fileName: file.name,
          reason: `Exceeds character limit of ${fileLimit}`,
        })
      }
    })

    if (codeFiles.length > maxFiles) {
      conflicts.push({
        fileName: "Project",
        reason: `Exceeds maximum number of files (${maxFiles})`,
      })
    }

    // Check for failed tests only if they have been viewed
    if (testResultsViewed) {
      const failedTestsCount = testResults.filter((result) => !result.passed).length
      setFailedTests(failedTestsCount)
      setTotalTests(testResults.length)

      if (failedTestsCount > 0) {
        conflicts.push({
          fileName: "Tests",
          reason: `${failedTestsCount} out of ${testResults.length} tests failed`,
        })
      }
    }

    return conflicts
  }

  const handleSubmit = () => {
    const conflicts = checkSubmissionConflicts()

    if (conflicts.length > 0) {
      setSubmissionConflicts(conflicts)
      setIsWarningDialogOpen(true)
    } else {
      setSubmissionData((prev) => ({
        ...prev,
        testResults,
        output: output,
      }))
      setIsConfirmDialogOpen(true)
    }
  }

  const handleConfirmSubmit = () => {
    setIsConfirmDialogOpen(false)

    // Derive submittedCode from globalCodeFiles
    const submittedCode = globalCodeFiles.map((file) => ({
      id: file.id,
      content: file.content,
    }))

    // Derive codeName from globalCodeFiles
    const codeName = globalCodeFiles.map((file) => ({ id: file.id, name: file.name }))
    onSubmit({
      ...submissionData,
      output: output,
      submittedCode,
      codeName, // Include the derived codeName in the submission
    } as unknown as CodeQuestionSubmissionv1_0_0)

    router.push(`/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`)
  }

  const handleCloseWarningDialog = () => {
    setIsWarningDialogOpen(false)
  }

  const handleNewFile = (name: string, language: string) => {
    if (globalCodeFiles.length >= maxFiles) {
      toast({
        title: "File limit reached",
        description: `You have reached the maximum number of files (${maxFiles}). Please delete a file before creating a new one.`,
        variant: "destructive",
      })
      return
    }
    const newId = `file${globalCodeFiles.length + 1}`
    const newFile: CodeFile = {
      id: newId,
      name,
      language,
      content: "",
      history: { past: [], future: [] },
    }
    const updatedGlobalCodeFiles = [...globalCodeFiles, newFile]
    setGlobalCodeFiles(updatedGlobalCodeFiles)
    setSubmissionData((prev) => ({
      ...prev,
      submittedCode: updatedGlobalCodeFiles.map((file) => file.content),
      codeName: [...(prev.codeName || []), { id: newId, name }],
    }))
  }

  const onRenameFile = (oldName: string, newName: string) => {
    const updatedCodeFiles = codeFiles.map(
      (file) =>
        file.name === oldName ? { ...file, name: newName, language: getLanguageFromExtension(newName) } : file, // Updatelanguage here
    )
    setCodeFiles(updatedCodeFiles)

    const updatedGlobalCodeFiles = globalCodeFiles.map(
      (file) =>
        file.name === oldName ? { ...file, name: newName, language: getLanguageFromExtension(newName) } : file, // Update language here
    )
    setGlobalCodeFiles(updatedGlobalCodeFiles)

    setActiveFileIndex(updatedCodeFiles.findIndex((file) => file.name === newName))
    setSubmissionData((prev) => ({
      ...prev,
      codeName: updatedCodeFiles.map((file) => ({ id: file.id, name: file.name })),
    }))
  }

  const onDeleteFile = (name: string) => {
    const fileIndex = codeFiles.findIndex((file) => file.name === name)
    if (fileIndex === -1) return

    const updatedCodeFiles = codeFiles.filter((file) => file.name !== name)
    setCodeFiles(updatedCodeFiles)

    const updatedGlobalCodeFiles = globalCodeFiles.filter((file) => file.name !== name)
    setGlobalCodeFiles([...updatedGlobalCodeFiles]) // Create a new array

    if (activeFileIndex >= updatedCodeFiles.length) {
      setActiveFileIndex(updatedCodeFiles.length - 1)
    } else if (activeFileIndex === fileIndex) {
      setActiveFileIndex(fileIndex > 0 ? fileIndex - 1 : 0)
    }

    setSubmissionData((prev) => ({
      ...prev,
      submittedCode: updatedCodeFiles.map((file) => file.content),
      codeName: updatedCodeFiles.map((file) => ({ id: file.id, name: file.name })),
    }))

    // Trigger an immediate save after deleting a file
    const currentState = {
      assignment,
      codeFiles: updatedCodeFiles,
      globalCodeFiles: updatedGlobalCodeFiles,
      output,
      testResults,
      activeFileIndex,
    }
    saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId)
  }

  const onImportFile = async (file: File) => {
    if (globalCodeFiles.length >= maxFiles) {
      toast({
        title: "File limit reached",
        description: `You have reached the maximum number of files (${maxFiles}). Please delete a file before importing a new one.`,
        variant: "destructive",
      })
      return
    }
    try {
      const content = await file.text()
      const fileLimit = getFileCharLimit(file.name, assignment.rules)
      const effectiveLimit = fileLimit === 0 ? maxChars : fileLimit
      if (content.length > effectiveLimit) {
        toast({
          title: "Character limit exceeded",
          description: `Importing this file would exceed the maximum character limit of ${effectiveLimit} for this file. Please reduce the content before importing.`,
          variant: "destructive",
        })
        return
      }
      const newId = `file${globalCodeFiles.length + 1}`
      const newFile: CodeFile = {
        id: newId,
        name: file.name,
        language: getLanguageFromExtension(file.name),
        content: content,
        history: { past: [], future: [] },
      }

      // Update only globalCodeFiles, not codeFiles
      setGlobalCodeFiles((prevFiles) => [...prevFiles, newFile])

      setSubmissionData((prev) => ({
        ...prev,
        submittedCode: [...prev.submittedCode, content],
        codeName: [...(prev.codeName || []), { id: newId, name: file.name }],
      }))

      toast({
        title: "File imported",
        description: `${file.name} has been imported successfully.`,
      })
    } catch (error) {
      console.error("Error reading file:", error)
      toast({
        title: "Error importing file",
        description: "An error occurred while importing the file. Please try again.",
        variant: "destructive",
      })
    }
  }

  const handleExportOpenFiles = async () => {
    const zip = new JSZip()
    const folder = zip.folder(assignment.title)

    if (folder) {
      codeFiles.forEach((file) => {
        folder.file(file.name, file.content)
      })

      const content = await zip.generateAsync({ type: "blob" })
      const url = window.URL.createObjectURL(content)
      const a = document.createElement("a")
      a.href = url
      a.download = `${assignment.title}_open_files.zip`
      a.click()
      window.URL.revokeObjectURL(url)
    }
  }

  const handleExportFiles = async (exportAll: boolean, singleFileName?: string) => {
    const zip = new JSZip()
    const folder = zip.folder(assignment.title)

    if (folder) {
      if (exportAll) {
        globalCodeFiles.forEach((file) => {
          folder.file(file.name, file.content)
        })
      } else if (singleFileName) {
        const fileToExport = codeFiles.find((file) => file.name === singleFileName)
        if (fileToExport) {
          folder.file(fileToExport.name, fileToExport.content)
        }
      }

      const content = await zip.generateAsync({ type: "blob" })
      const url = window.URL.createObjectURL(content)
      const a = document.createElement("a")
      a.href = url
      a.download = exportAll ? `${assignment.title}_all_files.zip` : `${assignment.title}_${singleFileName}.zip`
      a.click()
      window.URL.revokeObjectURL(url)
    }
  }

  const renderBottomPanel = () => {
    if (!activeTab) return null
    const hideTestOutputs = assignment.rules.includes("tests-outputs: n")
    return (
      <div
        className={`h-full overflow-auto p-4 ${
          mode === "light"
            ? "bg-white text-gray-800"
            : mode === "dark"
              ? "bg-gray-800 text-gray-200"
              : "bg-black text-white"
        }`}
      >
        {activeTab === "problems" && <ProblemsTab assignment={assignment} mode={mode} />}
        {activeTab === "output" && <OutputTab output={output} mode={mode} />}
        {activeTab === "testResults" && (
          <TestResultsTab
            testResults={testResults}
            mode={mode}
            hideTestOutputs={hideTestOutputs}
            //codeFiles={codeFiles}
          />
        )}
      </div>
    )
  }

  const handleReset = () => {
    if (assignment && courseId && moduleId && assessmentId) {
      // Check if IDs are available
      localStorage.removeItem(`assignmentState_${courseId}_${moduleId}_${assessmentId}_${assignment.id}`)
    }
    window.location.reload()
  }

  const handlePanelLayout = useCallback(
    (sizes: number[]) => {
      const lessonPanelSize = sizes[0]
      const totalWidth = window.innerWidth
      const lessonPanelWidth = (lessonPanelSize / 100) * totalWidth
      const minCodeEditorWidth = 300 // Minimum width for the code editor panel

      if (lessonPanelWidth > totalWidth - minCodeEditorWidth) {
        const newLessonPanelSize = ((totalWidth - minCodeEditorWidth) / totalWidth) * 100
        lessonPanelRef.current?.resize(newLessonPanelSize)
      } else if (lessonPanelWidth < 50 && !isLessonCollapsed) {
        setIsLessonCollapsed(true)
        setPrevLessonPanelSize(lessonPanelSize)
        lessonPanelRef.current?.collapse()
      } else if (lessonPanelWidth >= 50 && isLessonCollapsed) {
        setIsLessonCollapsed(false)
      }
    },
    [isLessonCollapsed],
  )

  const handleResizeHandleDoubleClick = useCallback(() => {
    if (isLessonCollapsed) {
      const totalWidth = window.innerWidth
      const newSize = (300 / totalWidth) * 100
      lessonPanelRef.current?.resize(newSize)
      setIsLessonCollapsed(false)
    } else {
      setPrevLessonPanelSize(lessonPanelRef.current?.getSize() || 30)
      lessonPanelRef.current?.collapse()
      setIsLessonCollapsed(true)
    }
  }, [isLessonCollapsed])

  const handleResizeHandleClick = useCallback(() => {
    if (isLessonCollapsed) {
      const totalWidth = window.innerWidth
      const newSize = (300 / totalWidth) * 100
      lessonPanelRef.current?.resize(newSize)
      setIsLessonCollapsed(false)
    }
  }, [isLessonCollapsed])

  const updateFileHistory = (fileIndex: number, newContent: string) => {
    const updatedFiles = [...codeFiles]
    const currentFile = updatedFiles[fileIndex]

    // Only update history if the content has changed
    if (currentFile.content !== newContent) {
      currentFile.history.past.push(currentFile.content)
      currentFile.history.future = []
      currentFile.content = newContent
      setCodeFiles(updatedFiles)
    }
  }

  const undo = useCallback(() => {
    const updatedFiles = [...codeFiles]
    const currentFile = updatedFiles[activeFileIndex]

    if (currentFile.history.past.length > 0) {
      const previousContent = currentFile.history.past.pop()!
      currentFile.history.future.push(currentFile.content)
      currentFile.content = previousContent
      setCodeFiles(updatedFiles)

      // Update Monaco Editor content
      if (editorRef.current) {
        editorRef.current.setValue(previousContent)
      }
    }
  }, [codeFiles, activeFileIndex])

  const redo = useCallback(() => {
    const updatedFiles = [...codeFiles]
    const currentFile = updatedFiles[activeFileIndex]

    if (currentFile.history.future.length > 0) {
      const nextContent = currentFile.history.future.pop()!
      currentFile.history.past.push(currentFile.content)
      currentFile.content = nextContent
      setCodeFiles(updatedFiles)

      // Update Monaco Editor content
      if (editorRef.current) {
        editorRef.current.setValue(nextContent)
      }
    }
  }, [codeFiles, activeFileIndex])

  const startContinuousUndo = useCallback(() => {
    setIsUndoing(true)
    const performUndo = () => {
      undo()
      undoTimerRef.current = setTimeout(performUndo, 25)
    }
    undoTimerRef.current = setTimeout(performUndo, 1000)
  }, [undo])

  const startContinuousRedo = useCallback(() => {
    setIsRedoing(true)
    const performRedo = () => {
      redo()
      redoTimerRef.current = setTimeout(performRedo, 25)
    }
    redoTimerRef.current = setTimeout(performRedo, 1000)
  }, [redo])

  const stopContinuousUndo = useCallback(() => {
    setIsUndoing(false)
    if (undoTimerRef.current) {
      clearTimeout(undoTimerRef.current)
    }
  }, [])

  const stopContinuousRedo = useCallback(() => {
    setIsRedoing(false)
    if (redoTimerRef.current) {
      clearTimeout(redoTimerRef.current)
    }
  }, [])

  const handleOpenSettings = () => {
    setIsSettingsOpen(true)
  }

  const handleCloseSettings = () => {
    setIsSettingsOpen(false)
  }

  const handleConfirmSettings = (newSettings: Settings) => {
    setSettings(newSettings)
    setIsSettingsOpen(false)
  }

  const executeJavaScript = (code: string) => {
    try {
      // Criar um contexto isolado para execução
      const context = {
        console: {
          log: (...args: any[]) => setOutput((prev) => prev + args.join(" ") + "\n"),
          error: (...args: any[]) => setOutput((prev) => prev + "Error: " + args.join(" ") + "\n"),
        },
      }
      const func = new Function("context", `with(context){${code}}`)
      func(context)
    } catch (error) {
      setOutput(`Error executing JavaScript: ${error}\n`)
    }
  }

  useEffect(() => {
    return () => {
      if (abortController) {
        abortController.abort()
      }
    }
  }, [abortController])

  useEffect(() => {
    if (!assignment) return

    const autoSaveTimer = setInterval(() => {
      const currentState = {
        assignment,
        codeFiles,
        globalCodeFiles, // Include globalCodeFiles in the state
        output,
        testResults,
        activeFileIndex,
      }
      saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId)
    }, 3000) // Auto-save every 3 seconds

    return () => clearInterval(autoSaveTimer)
  }, [assignment, codeFiles, globalCodeFiles, output, testResults, activeFileIndex, courseId, moduleId, assessmentId])

  const handleToggleSplitView = () => {
    if (isSplitViewActive) {
      setIsSplitViewActive(false)
      setSplitViewFileIndex(null) // Updated
    } else {
      setIsFileSelectorOpen(true)
    }
  }

  const handleSelectSplitViewFile = (fileName: string) => {
    const fileIndex = codeFiles.findIndex((file) => file.name === fileName)
    if (fileIndex !== -1) {
      setSplitViewFileIndex(fileIndex)
      setIsSplitViewActive(true)
    }
    setIsFileSelectorOpen(false)
  }

  const handleSplitViewEditorChange = (value: string | undefined) => {
    if (value !== undefined && splitViewFileIndex !== null) {
      const updatedCodeFiles = [...codeFiles]
      updatedCodeFiles[splitViewFileIndex].content = value
      setCodeFiles(updatedCodeFiles)
    }
  }

  const handleSetActiveTab = (tab: "problems" | "output" | "testResults" | null) => {
    setActiveTab(tab)
    if (tab === "testResults") {
      setTestResultsViewed(true)
    }
  }

  const handleOpenFile = (fileName: string) => {
    // Check if the file is already open
    const fileIndex = codeFiles.findIndex((file) => file.name.split("/").pop() === fileName.split("/").pop())
    if (fileIndex !== -1) {
      // If the file is already open, just set it as active
      setActiveFileIndex(fileIndex)
      return
    }

    // If the file is not open, find it in globalCodeFiles and add it to codeFiles
    const globalFile = globalCodeFiles.find((file) => file.name.split("/").pop() === fileName.split("/").pop())
    if (globalFile) {
      setCodeFiles((prev) => {
        const updatedFiles = [...prev, globalFile]
        setActiveFileIndex(updatedFiles.length - 1)
        return updatedFiles
      })
    }
  }

  // Update the handleCloseTab function
  const handleCloseTab = (index: number) => {
    const updatedCodeFiles = codeFiles.filter((_, i) => i !== index)
    setCodeFiles(updatedCodeFiles)

    // Update global code files
    const closedFile = codeFiles[index]
    const updatedGlobalCodeFiles = globalCodeFiles.map((file) =>
      file.id === closedFile.id ? { ...file, content: closedFile.content } : file,
    )
    setGlobalCodeFiles(updatedGlobalCodeFiles)

    if (updatedCodeFiles.length === 0) {
      setActiveFileIndex(-1)
    } else if (activeFileIndex >= updatedCodeFiles.length) {
      setActiveFileIndex(updatedCodeFiles.length - 1)
    } else if (activeFileIndex === index) {
      setActiveFileIndex(index > 0 ? index - 1 : 0)
    }

    // Save the updated state
    const currentState = {
      assignment,
      codeFiles: updatedCodeFiles,
      output,
      testResults,
      activeFileIndex: updatedCodeFiles.length > 0 ? (index > 0 ? index - 1 : 0) : -1,
      globalCodeFiles: updatedGlobalCodeFiles,
    }
    saveStateToLocalStorage(currentState, courseId, moduleId, assessmentId)
  }

  const onImportFolder = () => {
    const input = document.createElement("input")
    input.type = "file"
    input.webkitdirectory = true
    input.multiple = true
    input.style.display = "none"
    document.body.appendChild(input)

    input.onchange = async (e: Event) => {
      const files = (e.target as HTMLInputElement).files
      if (files) {
        const newFiles: CodeFile[] = []
        for (let i = 0; i < files.length; i++) {
          const file = files[i]
          const content = await file.text()
          const relativePath = file.webkitRelativePath
          const newFile: CodeFile = {
            id: `imported_${Date.now()}_${i}`,
            name: relativePath,
            language: getLanguageFromExtension(file.name),
            content: content,
            history: { past: [], future: [] },
          }
          newFiles.push(newFile)
        }

        // Update only globalCodeFiles
        setGlobalCodeFiles((prev) => [...prev, ...newFiles])

        // Update submission data
        setSubmissionData((prev) => ({
          ...prev,
          submittedCode: [...prev.submittedCode, ...newFiles.map((file) => file.content)],
          codeName: [...(prev.codeName || []), ...newFiles.map((file) => ({ id: file.id, name: file.name }))],
        }))

        toast({
          title: "Folder imported",
          description: `Imported ${newFiles.length} files`,
        })
      }
      document.body.removeChild(input)
    }

    input.click()
  }

  return (
    <div
      className={`h-screen ${
        mode === "light"
          ? "bg-gray-100 text-gray-800"
          : mode === "dark"
            ? "bg-gray-900 text-gray-200"
            : "bg-black text-white"
      }`}
    >
      <PanelGroup direction="horizontal" onLayout={handlePanelLayout}>
            <Panel
              ref={lessonPanelRef}
              defaultSize={20}
              minSize={0}
              maxSize={70}
              collapsible={true}
              onCollapse={() => setIsLessonCollapsed(true)}
              onExpand={() => setIsLessonCollapsed(false)}
            >
              <div className="flex flex-col h-full">
                <LessonPanel
                  assignment={assignment}
                  onReturn={handleReturn}
                  onSubmit={handleSubmit}
                  onReset={handleReset}
                  mode={mode}
                  isSubmitDisabled={isSubmitDisabled}
                  isSubmitHidden={assignment.rules.includes("submit: n")}
                  hierarchy={hierarchy}
                  globalCodeFiles={globalCodeFiles}
                  handleOpenFile={handleOpenFile}
                  codeFiles={codeFiles}
                />
              </div>
            </Panel>
            <PanelResizeHandle
              className={`transition-colors ${
                mode === "light"
                  ? "bg-gray-300 hover:bg-gray-400"
                  : mode === "dark"
                    ? "bg-gray-700 hover:bg-gray-600"
                    : "bg-yellow-500 hover:bg-yellow-400"
              } ${isLessonCollapsed ? "w-4 cursor-pointer" : "w-2"}`}
              onDoubleClick={handleResizeHandleDoubleClick}
              onClick={handleResizeHandleClick}
            />
        <Panel minSize={30}>
          <PanelGroup direction="vertical">
            <Panel minSize={50}>
              <div className="flex flex-col h-full">
                <TopMenu
                  mode={mode}
                  setMode={setMode}
                  onRunFile={runFile}
                  onRunApplication={runApplication}
                  onNewFile={handleNewFile}
                  onRenameFile={onRenameFile}
                  onDeleteFile={onDeleteFile}
                  onImportFile={onImportFile}
                  onExportFiles={handleExportFiles}
                  onExportOpenFiles={handleExportOpenFiles}
                  activeFileName={codeFiles[activeFileIndex]?.name || ""}
                  currentFileCount={globalCodeFiles.length}
                  maxFiles={maxFiles}
                  onUndo={undo}
                  onRedo={redo}
                  onUndoStart={startContinuousUndo}
                  onUndoStop={stopContinuousUndo}
                  onRedoStart={startContinuousRedo}
                  onRedoStop={stopContinuousRedo}
                  isUndoing={isUndoing}
                  isRedoing={isRedoing}
                  canUndo={codeFiles[activeFileIndex]?.history.past.length > 0}
                  canRedo={codeFiles[activeFileIndex]?.history.future.length > 0}
                  onOpenSettings={handleOpenSettings}
                  onRunThis={runThis}
                  onToggleSplitView={handleToggleSplitView}
                  isSplitViewActive={isSplitViewActive}
                  isRunAnimating={isRunAnimating}
                  isRunThisAnimating={isRunThisAnimating}
                  isRunAppAnimating={isRunAppAnimating}
                  activeFile={codeFiles[activeFileIndex]}
                  onReset={handleReset}
                  onImportFolder={onImportFolder}
                />
                <div className="flex-grow flex flex-col overflow-hidden">
                  <CodeTabs
                    files={codeFiles}
                    activeIndex={activeFileIndex}
                    onTabChange={handleTabChange}
                    mode={mode}
                    onCloseTab={handleCloseTab}
                  />
                  <div className="flex-grow overflow-hidden">
                    {codeFiles.length > 0 ? (
                      <div className={`flex h-full ${isSplitViewActive ? "space-x-2" : ""}`}>
                        <div className={`${isSplitViewActive ? "w-1/2" : "w-full"}`}>
                          <MonacoEditor
                            height="100%"
                            language={codeFiles[activeFileIndex]?.language || "plaintext"}
                            value={codeFiles[activeFileIndex]?.content || ""}
                            theme={mode === "light" ? "vs-light" : mode === "dark" ? "vs-dark" : "hc-black"}
                            onChange={handleEditorChange}
                            onMount={(editor, monaco) => {
                              editorRef.current = editor
                              // Disable undo/redo keyboard shortcuts
                              editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyZ, () => null)
                              editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyY, () => null)
                              editor.addCommand(
                                monaco.KeyMod.CtrlCmd | monaco.KeyMod.Shift | monaco.KeyCode.KeyZ,
                                () => null,
                              )

                              // Add HTML-specific features
                              if (codeFiles[activeFileIndex]?.language === "html") {
                                monaco.languages.html.htmlDefaults.setOptions({
                                  format: {
                                    tabSize: 2,
                                    insertSpaces: true,
                                    wrapLineLength: 0,
                                    unformatted: "",
                                    contentUnformatted: "",
                                    indentInnerHtml: false,
                                    preserveNewLines: false,
                                    maxPreserveNewLines: 0,
                                    indentHandlebars: false,
                                    endWithNewline: false,
                                    extraLiners: "",
                                    wrapAttributes: "auto"
                                  },
                                  suggest: {
                                    html5: true,
                                  },
                                })
                              }
                            }}
                            options={{
                              minimap: { enabled: false },
                              fontSize: 14,
                              scrollBeyondLastLine: false,
                              automaticLayout: true,
                              //undoStop: false, // Builda, mas nao sei se implica em outro erro menor
                              wordWrap: "on",
                              autoClosingBrackets: "always",
                              autoClosingQuotes: "always",
                              formatOnPaste: true,
                              formatOnType: true,
                            }}
                          />
                        </div>
                        {isSplitViewActive && splitViewFileIndex !== null && (
                          <div className="w-1/2">
                            <MonacoEditor
                              height="100%"
                              language={codeFiles[splitViewFileIndex].language}
                              value={codeFiles[splitViewFileIndex].content}
                              onChange={handleSplitViewEditorChange}
                              theme={mode === "light" ? "vs-light" : mode === "dark" ? "vs-dark" : "hc-black"}
                              options={{
                                minimap: { enabled: false },
                                fontSize: 14,
                                scrollBeyondLastLine: false,
                                automaticLayout: true,
                                wordWrap: "on",
                              }}
                            />
                          </div>
                        )}
                      </div>
                    ) : (
                      <div className="flex-grow overflow-auto flex items-center justify-center p-4">
                        <Alert
                          variant={mode === "high-contrast" ? "destructive" : "default"}
                          className={`w-full max-w-md ${
                            mode === "light"
                              ? "bg-white border-gray-200 text-gray-800"
                              : mode === "dark"
                                ? "bg-gray-800 border-gray-700 text-gray-200"
                                : "bg-black border-yellow-300 text-yellow-300"
                          }`}
                        >
                          <AlertCircle className={`h-4 w-4 ${mode === "high-contrast" ? "text-yellow-300" : ""}`} />
                          <AlertTitle className="mb-2">No open files</AlertTitle>
                          <AlertDescription>
                            No files are currently open. Select a file from the file explorer to start coding.
                          </AlertDescription>
                        </Alert>
                      </div>
                    )}
                  </div>
                  <CharacterCount
                    currentFileChars={currentFileChars}
                    maxFileChars={getFileCharLimit(codeFiles[activeFileIndex]?.name, assignment.rules) || maxChars}
                    totalChars={totalChars}
                    maxTotalChars={maxChars}
                    fileCount={globalCodeFiles.length}
                    maxFiles={maxFiles}
                    mode={mode}
                  />
                </div>
              </div>
            </Panel>
            <PanelResizeHandle
              className={`h-2 transition-colors ${
                mode === "light"
                  ? "bg-gray-300 hover:bg-gray-400"
                  : mode === "dark"
                    ? "bg-gray-700 hover:bg-gray-600"
                    : "bg-yellow-500 hover:bg-yellow-400"
              }`}
            />
            <Panel defaultSize={25} minSize={0}>
              {" "}
              {/* UPDATED LINE */}
              <div className="flex flex-col h-full">
                <BottomMenu
                  activeTab={activeTab}
                  setActiveTab={handleSetActiveTab}
                  mode={mode}
                  hideTestOutputs={assignment.rules.includes("tests-outputs: n")}
                />
                <div className="flex-grow overflow-auto">{renderBottomPanel()}</div>
              </div>
            </Panel>
          </PanelGroup>
        </Panel>
      </PanelGroup>
      <SubmissionWarningDialog
        isOpen={isWarningDialogOpen}
        onClose={handleCloseWarningDialog}
        onSubmit={handleConfirmSubmit}
        conflicts={submissionConflicts}
        failedTests={failedTests}
        totalTests={totalTests}
        mode={mode}
      />
      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={handleCloseSettings}
        onConfirm={handleConfirmSettings}
        mode={mode}
        initialSettings={settings}
      />
      <ConfirmSubmissionDialog
        isOpen={isConfirmDialogOpen}
        onClose={() => setIsConfirmDialogOpen(false)}
        onConfirm={handleConfirmSubmit}
        mode={mode}
      />
      <SplitViewFileSelector
        isOpen={isFileSelectorOpen}
        onClose={() => setIsFileSelectorOpen(false)}
        onSelectFile={handleSelectSplitViewFile}
        files={codeFiles}
        mode={mode}
      />
    </div>
  )
}

const ProblemsTab = ({ assignment, mode }: { assignment: CodeQuestionv1_0_0; mode: string }) => {
  const hideTestOutputs = assignment.rules.includes("tests-outputs: n")

  const renderInputs = () => {
    if (Array.isArray(assignment.inputs)) {
      return (
        <ul className="list-disc list-inside">
          {assignment.inputs.map((input, index) => (
            <li
              key={index}
              className={mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"}
            >
              {JSON.stringify(input)}
            </li>
          ))}
        </ul>
      )
    } else if (typeof assignment.inputs === "object") {
      return (
        <ul className="list-disc list-inside">
          {Object.entries(assignment.inputs).map(([key, value], index) => (
            <li
              key={index}
              className={mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"}
            >
              {key}: {JSON.stringify(value)}
            </li>
          ))}
        </ul>
      )
    } else {
      return (
        <p className={mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"}>
          No inputs available
        </p>
      )
    }
  }

  return (
    <div>
      <h2
        className={`text-xl font-bold mb-2 ${
          mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"
        }`}
      >
        Problem Details
      </h2>
      {assignment.rules.includes("inputs: y") && (
        <div className="mb-4">
          <h3
            className={`text-lg font-semibold ${
              mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"
            }`}
          >
            Inputs:
          </h3>
          {renderInputs()}
        </div>
      )}
      {assignment.rules.includes("outputs: y") && !hideTestOutputs && (
        <div>
          <h3
            className={`text-lg font-semibold ${
              mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"
            }`}
          >
            Expected Outputs:
          </h3>
          <ul className="list-disc list-inside">
            {Object.entries(assignment.outputs).map(([key, value], index) => (
              <li
                key={index}
                className={mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"}
              >
                {key}: {JSON.stringify(value)}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

const OutputTab = ({ output, mode }: { output: string; mode: string }) => {
  const [svgContent, setSvgContent] = useState<string | null>(null)
  const [bitmapContent, setBitmapContent] = useState<string | null>(null)

  useEffect(() => {
    // Extrair conteúdo SVG se presente na saída
    const svgMatch = output.match(/<svg.*?<\/svg>/g)
    if (svgMatch) {
      setSvgContent(svgMatch[0])
    } else {
      setSvgContent(null)
    }

    // Extrair conteúdo de imagem bitmap se presente na saída
    const bitmapMatch = output.match(/BITMAP_IMAGE:(.*)/)
    if (bitmapMatch) {
      setBitmapContent(bitmapMatch[1])
    } else {
      setBitmapContent(null)
    }
  }, [output])

  return (
    <div>
      <h2
        className={`text-xl font-bold mb-2 ${
          mode === "light" ? "text-gray-800" : mode === "dark" ? "text-gray-200" : "text-white"
        }`}
      >
        Output
      </h2>
      {bitmapContent ? (
        <div className="mt-4">
          <h3 className="text-lg font-semibold mb-2">Rendered Image:</h3>
          <Image src={bitmapContent || "/placeholder.svg"} alt="Bitmap Image" width={400} height={300} />
        </div>
      ) : svgContent ? (
        <div className="mt-4">
          <h3 className="text-lg font-semibold mb-2">Rendered SVG:</h3>
          <div dangerouslySetInnerHTML={{ __html: svgContent }} />
        </div>
      ) : (
        <pre
          className={`p-2 rounded ${
            mode === "light"
              ? "bg-gray-100 text-gray-800"
              : mode === "dark"
                ? "bg-gray-800 text-gray-200"
                : "bg-black text-white"
          }`}
        >
          {output || "No output yet. Run your code to see the results."}
        </pre>
      )}
    </div>
  )
}

interface TestResultsTabProps {
  testResults: any[]
  mode: string
  hideTestOutputs: boolean
  codeFiles: CodeFile[]
}

// Rename the TestResultsTab component here

