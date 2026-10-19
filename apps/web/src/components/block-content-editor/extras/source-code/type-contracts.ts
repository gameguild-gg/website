import type { ComponentProps } from "react"
import type { TestCase as RunnerTestCase } from "../../hooks/editor/test-runners/types"
import type { SourceCodeDialogs } from "./components/source-code-dialogs"
import type { SourceCodeEditorEdit } from "./components/source-code-editor-edit"
import type { SourceCodeEditorView } from "./components/source-code-editor-view"
import type { LanguageSettingsDialogProps } from "./dialogs/language-settings-dialog"
import type { Terminal } from "./terminal"
import type { JsonArray, ProgrammingLanguage, SourceCodeData, SourceCodeTestCase, SourceCodeTestCases } from "./types"

type Assignable<Source, Target> = [Source] extends [Target] ? true : false
type Assert<Value extends true> = Value
type SourceCodeUpdate = (data: Partial<SourceCodeData>) => void
type RuntimeTerminalProps = ComponentProps<typeof Terminal>
type EditProps = ComponentProps<typeof SourceCodeEditorEdit>
type ViewProps = ComponentProps<typeof SourceCodeEditorView>
type DialogProps = ComponentProps<typeof SourceCodeDialogs>

// These assertions are checked by the existing Web TypeScript gate. They emit
// no runtime statements and do not instantiate editor components or fixtures.
export type SourceCodeTypeContracts = [
  Assert<Assignable<RuntimeTerminalProps["testCases"], SourceCodeTestCases>>,
  Assert<Assignable<SourceCodeTestCases, RuntimeTerminalProps["testCases"]>>,
  Assert<Assignable<EditProps["updateTestCase"], RuntimeTerminalProps["updateTestCase"]>>,
  Assert<Assignable<ViewProps["updateTestCase"], RuntimeTerminalProps["updateTestCase"]>>,
  Assert<Assignable<SourceCodeUpdate, LanguageSettingsDialogProps["updateSourceCode"]>>,
  Assert<Assignable<SourceCodeUpdate, EditProps["updateSourceCode"]>>,
  Assert<Assignable<SourceCodeUpdate, ViewProps["updateSourceCode"]>>,
  Assert<Assignable<SourceCodeUpdate, DialogProps["updateSourceCode"]>>,
  Assert<Assignable<{ input: string; expectedOutput: string }, RunnerTestCase>>,
  Assert<Assignable<{
    type: "custom"
    customCode: string
    customCodeFirst: string | Record<ProgrammingLanguage, string>
    customCodeSecond: string | Record<ProgrammingLanguage, string>
    testSpecificFirst: string | Record<ProgrammingLanguage, string>
    testSpecificSecond: string | Record<ProgrammingLanguage, string>
    testInfo: string
  }, SourceCodeTestCase>>,
  Assert<Assignable<{
    selectedLanguage: "html"
    initialFileLanguage: "legacy-language"
    showTests: boolean
    output: string[]
    testCases: SourceCodeTestCases
    data: { hasConfiguredSettings: boolean; allowedLanguages: Record<string, boolean> }
  }, SourceCodeData>>,
  Assert<Assignable<[string, number, boolean, null, { nested: string[] }], JsonArray>>,
  Assert<Assignable<[() => void], JsonArray> extends false ? true : false>,
]
