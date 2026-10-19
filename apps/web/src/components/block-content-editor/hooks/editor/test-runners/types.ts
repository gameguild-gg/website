import type { CodeFile, JsonArray, ProgrammingLanguage } from "@/components/block-content-editor/extras/source-code/types"

export interface TestResult {
  passed: boolean
  actual: string
  expected: string
}

export type TestType = "custom" | "function" | "console"

export interface TestCase {
  type?: "custom" | "function" | "console"
  input?: string
  expectedOutput?: string
  args?: JsonArray
  expectedReturn?: JsonArray
}

export interface TestRunnerOptions {
  fileId: string
  file: CodeFile
  fileCases: TestCase[]
  files: CodeFile[]
  selectedLanguage: ProgrammingLanguage
  addOutput: (output: string | string[]) => void
  clearTerminal: () => void
  setIsExecuting: (isExecuting: boolean) => void
  setTestResults: (results: Record<string, { passed: boolean; actual: string; expected: string }[]> | ((prev: Record<string, { passed: boolean; actual: string; expected: string }[]>) => Record<string, { passed: boolean; actual: string; expected: string }[]>)) => void
  normalizeOutput: (output: string) => string
}

export type TestRunner = (options: TestRunnerOptions) => Promise<void>
