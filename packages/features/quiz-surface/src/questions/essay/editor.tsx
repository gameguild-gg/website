/**
 * Essay Editor
 * Configure essay question settings
 */

"use client"

import { useCallback } from "react"
import { useFormContext } from "react-hook-form"
import type { SerializedEditorState } from "lexical"
import { Label } from "@game-guild/ui/components/label"
import { Input } from "@game-guild/ui/components/input"
import { Switch } from "@game-guild/ui/components/switch"
import type { EssayEntry } from "@game-guild/quiz"
import { EssayLexicalEditor } from "./lexical-adapter"

function optionalNumberInput(value: unknown): number | undefined {
  return value === "" || value === null || value === undefined
    ? undefined
    : Number(value)
}

export function EssayEditor() {
  const { register, watch, setValue } = useFormContext<EssayEntry>()
  const showWordCount = watch("showWordCount")
  const correctAnswer = watch("correctAnswer")
  const requireFormatting = watch("requireFormatting")

  const handleCorrectAnswerChange = useCallback(
    (state: SerializedEditorState, plainText: string) => {
      setValue("correctAnswer", state as unknown as Record<string, unknown>)
      setValue("correctAnswerPlain", plainText)
    },
    [setValue],
  )

  return (
    <div className="space-y-4">
      <div className="text-sm text-gray-600 dark:text-gray-400 bg-blue-50 dark:bg-blue-950/30 p-3 rounded-lg border border-blue-200 dark:border-blue-800">
        <p>Essay questions require manual grading. Configure optional word limits below.</p>
      </div>

      <div className="space-y-2">
        <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
          Correct / Model Answer
        </Label>
        <div className="text-xs text-gray-500 dark:text-gray-400 bg-blue-50 dark:bg-blue-950/30 p-2 rounded border border-blue-200 dark:border-blue-800">
          Provide the expected answer. This will be used as a reference for grading.
        </div>
        <EssayLexicalEditor
          initialState={correctAnswer as SerializedEditorState | null | undefined}
          onChange={handleCorrectAnswerChange}
          placeholder="Write the correct/model answer..."
          minHeight="120px"
        />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label className="text-sm font-medium">Minimum Words</Label>
          <Input
            type="number"
            min={0}
            step={1}
            placeholder="Optional"
            {...register("minWordCount", { setValueAs: optionalNumberInput })}
            autoComplete="off"
            className="bg-white dark:bg-gray-800"
          />
        </div>
        <div className="space-y-2">
          <Label className="text-sm font-medium">Maximum Words</Label>
          <Input
            type="number"
            min={0}
            step={1}
            placeholder="Optional"
            {...register("maxWordCount", { setValueAs: optionalNumberInput })}
            autoComplete="off"
            className="bg-white dark:bg-gray-800"
          />
        </div>
      </div>

      <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg border">
        <div>
          <Label className="text-sm">Require Formatting</Label>
          <p className="text-xs text-gray-500 dark:text-gray-400">Answer must match bold, italic, headings, lists, etc.</p>
        </div>
        <Switch
          checked={requireFormatting}
          onCheckedChange={(checked) => setValue("requireFormatting", checked)}
        />
      </div>

      <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg border">
        <Label className="text-sm">Show Word Count</Label>
        <Switch
          checked={showWordCount}
          onCheckedChange={(checked) => setValue("showWordCount", checked)}
        />
      </div>
    </div>
  )
}
