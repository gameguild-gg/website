/**
 * Fill in the Blank Editor
 * Configure expected answers for each blank with four input modes:
 * 1. Text - Type the answer
 * 2. Number - Type a numeric answer (with optional tolerance)
 * 3. Dropdown - Select from options (first is correct)
 * 4. Word Bank - Drag from shared pool (first is correct)
 */

"use client"

import { useEffect, useRef, useState } from "react"
import { useFieldArray, useFormContext } from "react-hook-form"
import { Plus, X, Type, Hash, ChevronDown, LayoutGrid } from "lucide-react"
import { Button } from "@game-guild/ui/components/button"
import { Checkbox } from "@game-guild/ui/components/checkbox"
import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select"
import type { FillInTheBlankEntry, FillBlankInput } from "@game-guild/quiz"
import { FillBlankInputType } from "@game-guild/quiz"

const UNIT_CATEGORIES = [
  {
    label: "Computing",
    units: ["b", "B", "KB", "MB", "GB", "TB", "PB", "Kbps", "Mbps", "Gbps", "Hz", "KHz", "MHz", "GHz", "ms", "μs", "ns", "FLOPS", "op/s", "iops"],
  },
  {
    label: "Design",
    units: ["px", "rem", "em", "vw", "vh", "vmin", "vmax", "%", "pt", "pc", "cm", "mm", "in", "ch", "ex", "fr", "dpi", "dpcm", "dppx", "svh"],
  },
  {
    label: "Distance",
    units: ["mm", "cm", "m", "km", "in", "ft", "yd", "mi", "nm", "μm", "Å", "au", "ly", "pc", "nmi", "fathom", "furlong", "league", "mil", "thou"],
  },
  {
    label: "Speed",
    units: ["m/s", "km/h", "mph", "kn", "ft/s", "cm/s", "mm/s", "Mach", "c", "km/s", "mi/s", "in/s", "yd/s", "m/min", "km/min", "ft/min", "rpm", "rad/s", "°/s", "rev/s"],
  },
  {
    label: "Volume",
    units: ["mL", "L", "m³", "cm³", "mm³", "gal", "qt", "pt", "cup", "fl oz", "tbsp", "tsp", "bbl", "ft³", "in³", "yd³", "dL", "hL", "kL", "μL"],
  },
  {
    label: "Weight",
    units: ["mg", "g", "kg", "t", "oz", "lb", "st", "μg", "ng", "ct", "grain", "cwt", "ton", "slug", "dram", "troy oz", "pennyweight", "Da", "kN", "N"],
  },
]

const INPUT_TYPE_OPTIONS = [
  { value: FillBlankInputType.Text, label: "Text Input", icon: Type, description: "Type the answer" },
  { value: FillBlankInputType.Number, label: "Number", icon: Hash, description: "Type a number" },
  { value: FillBlankInputType.Dropdown, label: "Dropdown", icon: ChevronDown, description: "Select from options" },
  { value: FillBlankInputType.WordBank, label: "Word Bank", icon: LayoutGrid, description: "Drag from word pool" },
]

function UnitPicker({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const [open, setOpen] = useState(false)
  const [activeCategory, setActiveCategory] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener("mousedown", handleClickOutside)
    return () => document.removeEventListener("mousedown", handleClickOutside)
  }, [open])

  return (
    <div className="relative" ref={containerRef}>
      <div className="flex gap-1">
        <Input
          type="text"
          placeholder="e.g. kg"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 flex-1 min-w-0"
        />
        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={() => setOpen((prev) => !prev)}
          className="shrink-0 h-9 w-9 border-gray-300 dark:border-gray-600"
        >
          <ChevronDown className="h-3.5 w-3.5" />
        </Button>
      </div>
      {open && (
        <div className="absolute right-0 top-full mt-1 z-50 w-[480px] rounded-md border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-lg">
          <div className="flex border-b border-gray-200 dark:border-gray-700">
            {UNIT_CATEGORIES.map((cat, idx) => (
              <button
                key={cat.label}
                type="button"
                onClick={() => setActiveCategory(idx)}
                className={`flex-1 px-2 py-2 text-xs font-medium transition-colors ${
                  activeCategory === idx
                    ? "text-blue-600 dark:text-blue-400 border-b-2 border-blue-600 dark:border-blue-400"
                    : "text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300"
                }`}
              >
                {cat.label}
              </button>
            ))}
          </div>
          <div className="p-2">
            <div className="grid grid-cols-5 gap-1">
              {UNIT_CATEGORIES[activeCategory]!.units.map((unit) => (
                <button
                  key={unit}
                  type="button"
                  onClick={() => {
                    onChange(unit)
                    setOpen(false)
                  }}
                  className={`px-2 py-1.5 text-xs rounded-md transition-colors text-center ${
                    value === unit
                      ? "bg-blue-100 dark:bg-blue-900/50 text-blue-700 dark:text-blue-300 font-medium"
                      : "bg-gray-50 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                  }`}
                >
                  {unit}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function createDefaultInput(type: FillBlankInputType): FillBlankInput {
  switch (type) {
    case FillBlankInputType.Text:
      return { type: FillBlankInputType.Text, acceptedAnswers: [""] }
    case FillBlankInputType.Number:
      return { type: FillBlankInputType.Number, correctValue: 0 }
    case FillBlankInputType.Dropdown:
      return { type: FillBlankInputType.Dropdown, options: ["", ""] }
    case FillBlankInputType.WordBank:
      return { type: FillBlankInputType.WordBank, words: ["", ""] }
  }
}

export function FillBlankEditor() {
  const { control, watch, setValue, getValues } = useFormContext<FillInTheBlankEntry>()
  const { fields, replace } = useFieldArray({
    control,
    name: "blanks",
  })

  const stem = watch("stem")
  const blanks = watch("blanks") || []

  // Parse blanks from stem - supports both ___ and _word_
  // ___ = empty blank, _word_ = blank with preset correct answer
  const parsedBlanks = (stem || "").match(/___|\b_[^_]+_\b/g) || []
  const blankCount = parsedBlanks.length

  // Extract correct answers from _word_ patterns
  const extractedAnswers = parsedBlanks.map((match) => {
    if (match === "___") return null
    // Extract word from _word_ pattern
    return match.slice(1, -1) // Remove surrounding underscores
  })

  // Auto-sync blanks with patterns in stem
  useEffect(() => {
    const currentBlanks = getValues("blanks") || []

    // Helper to update input with locked answer
    const updateInputWithLockedAnswer = (input: FillBlankInput, lockedAnswer: string): FillBlankInput => {
      switch (input.type) {
        case FillBlankInputType.Text:
          if (input.acceptedAnswers[0] !== lockedAnswer) {
            return { ...input, acceptedAnswers: [lockedAnswer, ...input.acceptedAnswers.slice(1)] }
          }
          break
        case FillBlankInputType.Number: {
          const num = parseFloat(lockedAnswer)
          if (!isNaN(num) && input.correctValue !== num) {
            return { ...input, correctValue: num }
          }
          break
        }
        case FillBlankInputType.Dropdown:
          if (input.options[0] !== lockedAnswer) {
            return { ...input, options: [lockedAnswer, ...input.options.slice(1)] }
          }
          break
        case FillBlankInputType.WordBank:
          if (input.words[0] !== lockedAnswer) {
            return { ...input, words: [lockedAnswer, ...input.words.slice(1)] }
          }
          break
      }
      return input
    }

    if (blankCount !== currentBlanks.length) {
      const newBlanks = []
      for (let i = 0; i < blankCount; i++) {
        const existingBlank = currentBlanks[i]
        const extractedAnswer = extractedAnswers[i]

        if (existingBlank) {
          // Update existing blank with extracted answer if available
          if (extractedAnswer) {
            const updatedInput = updateInputWithLockedAnswer(existingBlank.input, extractedAnswer)
            if (updatedInput !== existingBlank.input) {
              newBlanks.push({
                ...existingBlank,
                input: updatedInput,
              })
              continue
            }
          }
          newBlanks.push(existingBlank)
        } else {
          // Create new blank
          const defaultInput = createDefaultInput(FillBlankInputType.Text)
          if (extractedAnswer && defaultInput.type === FillBlankInputType.Text) {
            defaultInput.acceptedAnswers[0] = extractedAnswer
          }
          newBlanks.push({
            id: crypto.randomUUID(),
            position: i,
            input: defaultInput,
          })
        }
      }
      replace(newBlanks)
    } else {
      // Check if any extracted answers changed
      let needsUpdate = false
      const updatedBlanks = currentBlanks.map((blank, i) => {
        const extractedAnswer = extractedAnswers[i]
        if (extractedAnswer) {
          const updatedInput = updateInputWithLockedAnswer(blank.input, extractedAnswer)
          if (updatedInput !== blank.input) {
            needsUpdate = true
            return { ...blank, input: updatedInput }
          }
        }
        return blank
      })
      if (needsUpdate) {
        replace(updatedBlanks)
      }
    }
  }, [blankCount, extractedAnswers.join(","), getValues, replace])

  const changeInputType = (blankIndex: number, newType: FillBlankInputType) => {
    const currentInput = getValues(`blanks.${blankIndex}.input`)
    const lockedAnswer = extractedAnswers[blankIndex]
    
    // Extract current values from the existing input
    let existingValues: string[] = []
    switch (currentInput.type) {
      case FillBlankInputType.Text:
        existingValues = currentInput.acceptedAnswers.filter((v: string) => v.trim() !== "")
        break
      case FillBlankInputType.Number:
        existingValues = [String(currentInput.correctValue)]
        break
      case FillBlankInputType.Dropdown:
        existingValues = currentInput.options.filter((v: string) => v.trim() !== "")
        break
      case FillBlankInputType.WordBank:
        existingValues = currentInput.words.filter((v: string) => v.trim() !== "")
        break
    }
    
    // Ensure at least one value (locked or empty placeholder)
    if (existingValues.length === 0) {
      existingValues = lockedAnswer ? [lockedAnswer] : [""]
    } else if (lockedAnswer && existingValues[0] !== lockedAnswer) {
      // Ensure locked answer is first
      existingValues = [lockedAnswer, ...existingValues.filter(v => v !== lockedAnswer)]
    }
    
    // Create new input with preserved values
    let newInput: FillBlankInput
    switch (newType) {
      case FillBlankInputType.Text:
        newInput = { 
          type: FillBlankInputType.Text, 
          acceptedAnswers: existingValues.length > 0 ? existingValues : [""] 
        }
        break
      case FillBlankInputType.Number: {
        const numVal = parseFloat(existingValues[0] || "0")
        newInput = {
          type: FillBlankInputType.Number,
          correctValue: isNaN(numVal) ? 0 : numVal,
        }
        break
      }
      case FillBlankInputType.Dropdown: {
        // Dropdown needs at least 2 options
        const dropdownOptions = existingValues.length >= 2 ? existingValues : [...existingValues, ""]
        newInput = { 
          type: FillBlankInputType.Dropdown, 
          options: dropdownOptions 
        }
        break
      }
      case FillBlankInputType.WordBank: {
        // Word bank needs at least 2 words
        const wordBankWords = existingValues.length >= 2 ? existingValues : [...existingValues, ""]
        newInput = { 
          type: FillBlankInputType.WordBank, 
          words: wordBankWords 
        }
        break
      }
    }
    
    setValue(`blanks.${blankIndex}.input`, newInput)
  }

  // ============================================================================
  // Text Input Handlers
  // ============================================================================

  const addTextAnswer = (blankIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Text) {
      setValue(`blanks.${blankIndex}.input.acceptedAnswers`, [...input.acceptedAnswers, ""])
    }
  }

  const removeTextAnswer = (blankIndex: number, answerIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Text && input.acceptedAnswers.length > 1) {
      setValue(
        `blanks.${blankIndex}.input.acceptedAnswers`,
        input.acceptedAnswers.filter((_: string, i: number) => i !== answerIndex)
      )
    }
  }

  const updateTextAnswer = (blankIndex: number, answerIndex: number, value: string) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Text) {
      const newAnswers = [...input.acceptedAnswers]
      newAnswers[answerIndex] = value
      setValue(`blanks.${blankIndex}.input.acceptedAnswers`, newAnswers)
    }
  }

  // ============================================================================
  // Number Input Handlers
  // ============================================================================

  const updateNumberValue = (blankIndex: number, value: string) => {
    const num = parseFloat(value)
    if (!isNaN(num)) {
      setValue(`blanks.${blankIndex}.input.correctValue`, num)
    }
  }

  const updateNumberTolerance = (blankIndex: number, value: string) => {
    if (value === "") {
      setValue(`blanks.${blankIndex}.input.tolerance`, undefined)
      return
    }
    const num = parseFloat(value)
    if (!isNaN(num) && num >= 0) {
      setValue(`blanks.${blankIndex}.input.tolerance`, num)
    }
  }

  const updateNumberPrecision = (blankIndex: number, value: string) => {
    if (value === "") {
      setValue(`blanks.${blankIndex}.input.requiredPrecision`, undefined)
      return
    }
    const num = parseInt(value, 10)
    if (!isNaN(num) && num >= 0 && num <= 10) {
      setValue(`blanks.${blankIndex}.input.requiredPrecision`, num)
    }
  }

  const updateNumberUnit = (blankIndex: number, value: string) => {
    if (value === "") {
      setValue(`blanks.${blankIndex}.input.unit`, undefined)
      setValue(`blanks.${blankIndex}.input.requireUnit`, undefined)
      return
    }
    setValue(`blanks.${blankIndex}.input.unit`, value)
  }

  // ============================================================================
  // Dropdown Handlers
  // ============================================================================

  const addDropdownOption = (blankIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Dropdown) {
      setValue(`blanks.${blankIndex}.input.options`, [...input.options, ""])
    }
  }

  const removeDropdownOption = (blankIndex: number, optionIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Dropdown && input.options.length > 2) {
      setValue(
        `blanks.${blankIndex}.input.options`,
        input.options.filter((_: string, i: number) => i !== optionIndex)
      )
    }
  }

  const updateDropdownOption = (blankIndex: number, optionIndex: number, value: string) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.Dropdown) {
      const newOptions = [...input.options]
      newOptions[optionIndex] = value
      setValue(`blanks.${blankIndex}.input.options`, newOptions)
    }
  }

  // ============================================================================
  // Word Bank Handlers
  // ============================================================================

  const addWordBankWord = (blankIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.WordBank) {
      setValue(`blanks.${blankIndex}.input.words`, [...input.words, ""])
    }
  }

  const removeWordBankWord = (blankIndex: number, wordIndex: number) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.WordBank && input.words.length > 2) {
      setValue(
        `blanks.${blankIndex}.input.words`,
        input.words.filter((_: string, i: number) => i !== wordIndex)
      )
    }
  }

  const updateWordBankWord = (blankIndex: number, wordIndex: number, value: string) => {
    const input = getValues(`blanks.${blankIndex}.input`)
    if (input.type === FillBlankInputType.WordBank) {
      const newWords = [...input.words]
      newWords[wordIndex] = value
      setValue(`blanks.${blankIndex}.input.words`, newWords)
    }
  }

  // Check if any blank uses Word Bank
  const hasWordBankBlanks = blanks.some(b => b.input.type === FillBlankInputType.WordBank)

  // Collect all Word Bank words for preview
  const allWordBankWords = blanks
    .filter(b => b.input.type === FillBlankInputType.WordBank)
    .flatMap(b => (b.input as { words: string[] }).words)
    .filter(w => w.trim() !== "")

  return (
    <div className="space-y-4">
      <div className="text-sm text-gray-600 dark:text-gray-400 bg-blue-50 dark:bg-blue-950/30 p-3 rounded-lg border border-blue-200 dark:border-blue-800">
        <p className="font-medium mb-2">💡 Tip for Fill-in-the-Blank:</p>
        <p>
          Use <code className="bg-gray-200 dark:bg-gray-700 px-1 rounded">___</code> to create empty blanks.
        </p>
        <p className="mt-1">
          Use <code className="bg-gray-200 dark:bg-gray-700 px-1 rounded">_answer_</code> to create a blank with a preset correct answer.
        </p>
        <p className="mt-1 text-xs text-gray-500 dark:text-gray-500">
          Example: &quot;The _Jupiter_ is the largest planet in our _solar_ system.&quot;
        </p>
      </div>

      <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
        Blank Configuration ({blankCount} blank{blankCount !== 1 ? "s" : ""} detected)
      </Label>

      {fields.length === 0 && (
        <div className="text-center py-8 text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
          <p>No blanks detected yet.</p>
          <p className="text-sm mt-1">
            Add <code className="bg-gray-200 dark:bg-gray-700 px-1 rounded">___</code> in your question above to create blanks.
          </p>
        </div>
      )}

      {fields.map((field, blankIndex) => {
        const input = blanks[blankIndex]?.input
        const lockedAnswer = extractedAnswers[blankIndex] // Derived from stem at runtime
        if (!input) return null

        return (
          <div
            key={field.id}
            className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 space-y-4 bg-gray-50 dark:bg-gray-800/30"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Label className="text-sm font-medium text-gray-700 dark:text-gray-300">
                  Blank #{blankIndex + 1}
                </Label>
                {lockedAnswer && (
                  <span className="text-xs bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 px-2 py-0.5 rounded-full">
                    🔒 From source
                  </span>
                )}
              </div>
              <Select
                value={input.type}
                onValueChange={(value) => changeInputType(blankIndex, value as FillBlankInputType)}
              >
                <SelectTrigger className="w-40 h-8 text-xs bg-white dark:bg-gray-800">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INPUT_TYPE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      <div className="flex items-center gap-2">
                        <option.icon className="h-3 w-3" />
                        <span>{option.label}</span>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Text Input Mode */}
            {input.type === FillBlankInputType.Text && (
              <div className="space-y-2">
                <Label className="text-xs text-gray-600 dark:text-gray-400">
                  Accepted Answers (any of these will be correct)
                </Label>
                <div className="space-y-2">
                  {input.acceptedAnswers.map((answer: string, answerIndex: number) => {
                    const isLocked = answerIndex === 0 && !!lockedAnswer
                    return (
                      <div key={answerIndex} className="flex items-center gap-2">
                        <div className="relative flex-1">
                          <Input
                            placeholder={answerIndex === 0 ? "✓ Correct answer" : `Alternative ${answerIndex}`}
                            value={answer}
                            onChange={(e) => updateTextAnswer(blankIndex, answerIndex, e.target.value)}
                            disabled={isLocked}
                            autoComplete="off"
                            className={`bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 ${
                              isLocked ? "bg-gray-100 dark:bg-gray-700 cursor-not-allowed" : ""
                            }`}
                          />
                          {isLocked && (
                            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-gray-500 dark:text-gray-400">
                              🔒 Edit in source
                            </span>
                          )}
                        </div>
                        {input.acceptedAnswers.length > 1 && !isLocked && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            onClick={() => removeTextAnswer(blankIndex, answerIndex)}
                            className="text-gray-500 hover:text-red-600 dark:text-gray-400 dark:hover:text-red-400 h-9 w-9 shrink-0"
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    )
                  })}
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => addTextAnswer(blankIndex)}
                  className="w-full bg-transparent border-dashed border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add Alternative Answer
                </Button>
                <div className="flex items-center gap-2 pt-1">
                  <Checkbox
                    id={`case-sensitive-${blankIndex}`}
                    checked={input.caseSensitive ?? false}
                    onCheckedChange={(checked) =>
                      setValue(`blanks.${blankIndex}.input.caseSensitive`, !!checked)
                    }
                  />
                  <Label
                    htmlFor={`case-sensitive-${blankIndex}`}
                    className="text-xs text-gray-600 dark:text-gray-400 cursor-pointer"
                  >
                    Case-sensitive
                  </Label>
                </div>
              </div>
            )}

            {/* Number Input Mode */}
            {input.type === FillBlankInputType.Number && (
              <div className="space-y-3">
                <div className="space-y-2">
                  <Label className="text-xs text-gray-600 dark:text-gray-400">
                    Correct Value
                  </Label>
                  <div className="relative">
                    <Input
                      type="number"
                      step="any"
                      placeholder="e.g. 42"
                      value={input.correctValue}
                      onChange={(e) => updateNumberValue(blankIndex, e.target.value)}
                      disabled={!!lockedAnswer}
                      autoComplete="off"
                      className={`bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 ${
                        lockedAnswer ? "bg-gray-100 dark:bg-gray-700 cursor-not-allowed" : ""
                      }`}
                    />
                    {lockedAnswer && (
                      <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-gray-500 dark:text-gray-400">
                        🔒 Edit in source
                      </span>
                    )}
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-2">
                    <Label className="text-xs text-gray-600 dark:text-gray-400">
                      Tolerance (±)
                    </Label>
                    <Input
                      type="number"
                      step="any"
                      min="0"
                      placeholder="exact if empty"
                      value={input.tolerance ?? ""}
                      onChange={(e) => updateNumberTolerance(blankIndex, e.target.value)}
                      autoComplete="off"
                      className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600"
                    />
                    {input.tolerance !== undefined && input.tolerance > 0 && (
                      <p className="text-xs text-gray-500 dark:text-gray-400">
                        {input.correctValue - input.tolerance} to {input.correctValue + input.tolerance}
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label className="text-xs text-gray-600 dark:text-gray-400">
                      Decimal Places
                    </Label>
                    <Input
                      type="number"
                      step="1"
                      min="0"
                      max="10"
                      placeholder="any if empty"
                      value={input.requiredPrecision ?? ""}
                      onChange={(e) => updateNumberPrecision(blankIndex, e.target.value)}
                      autoComplete="off"
                      className="bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600"
                    />
                    {input.requiredPrecision !== undefined && input.requiredPrecision > 0 && (
                      <p className="text-xs text-gray-500 dark:text-gray-400">
                        e.g. {input.correctValue.toFixed(input.requiredPrecision)}
                      </p>
                    )}
                  </div>
                  <div className="space-y-2">
                    <Label className="text-xs text-gray-600 dark:text-gray-400">
                      Unit
                    </Label>
                    <UnitPicker
                      value={input.unit ?? ""}
                      onChange={(value) => updateNumberUnit(blankIndex, value)}
                    />
                  </div>
                </div>
                <div className="flex items-center gap-4 pt-1">
                  <div className="flex items-center gap-2">
                    <Checkbox
                      id={`allow-negative-${blankIndex}`}
                      checked={input.allowNegative ?? true}
                      onCheckedChange={(checked) =>
                        setValue(`blanks.${blankIndex}.input.allowNegative`, !!checked)
                      }
                    />
                    <Label
                      htmlFor={`allow-negative-${blankIndex}`}
                      className="text-xs text-gray-600 dark:text-gray-400 cursor-pointer"
                    >
                      Allow negative
                    </Label>
                  </div>
                  {input.unit && (
                    <div className="flex items-center gap-2">
                      <Checkbox
                        id={`require-unit-${blankIndex}`}
                        checked={input.requireUnit ?? false}
                        onCheckedChange={(checked) =>
                          setValue(`blanks.${blankIndex}.input.requireUnit`, !!checked)
                        }
                      />
                      <Label
                        htmlFor={`require-unit-${blankIndex}`}
                        className="text-xs text-gray-600 dark:text-gray-400 cursor-pointer"
                      >
                        Require unit in answer
                      </Label>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Dropdown Mode */}
            {input.type === FillBlankInputType.Dropdown && (
              <div className="space-y-2">
                <Label className="text-xs text-gray-600 dark:text-gray-400">
                  Options (first is the correct answer)
                </Label>
                <div className="space-y-2">
                  {input.options.map((option: string, optionIndex: number) => {
                    const isLocked = optionIndex === 0 && !!lockedAnswer
                    return (
                      <div key={optionIndex} className="flex items-center gap-2">
                        <div className="relative flex-1">
                          <Input
                            placeholder={optionIndex === 0 ? "✓ Correct answer" : `Distractor ${optionIndex}`}
                            value={option}
                            onChange={(e) => updateDropdownOption(blankIndex, optionIndex, e.target.value)}
                            disabled={isLocked}
                            className={`bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 ${
                              optionIndex === 0 ? "border-green-500 dark:border-green-600 ring-1 ring-green-500/20" : ""
                            } ${isLocked ? "bg-gray-100 dark:bg-gray-700 cursor-not-allowed" : ""}`}
                          />
                          {isLocked ? (
                            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-gray-500 dark:text-gray-400">
                              🔒 Edit in source
                            </span>
                          ) : optionIndex === 0 ? (
                            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-green-600 dark:text-green-400 font-medium">
                              Correct
                            </span>
                          ) : null}
                        </div>
                        {input.options.length > 2 && !isLocked && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            onClick={() => removeDropdownOption(blankIndex, optionIndex)}
                            className="text-gray-500 hover:text-red-600 dark:text-gray-400 dark:hover:text-red-400 h-9 w-9 shrink-0"
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    )
                  })}
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => addDropdownOption(blankIndex)}
                  className="w-full bg-transparent border-dashed border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add Distractor
                </Button>
              </div>
            )}

            {/* Word Bank Mode */}
            {input.type === FillBlankInputType.WordBank && (
              <div className="space-y-2">
                <Label className="text-xs text-gray-600 dark:text-gray-400">
                  Words (first is the correct answer)
                </Label>
                <div className="space-y-2">
                  {input.words.map((word: string, wordIndex: number) => {
                    const isLocked = wordIndex === 0 && !!lockedAnswer
                    return (
                      <div key={wordIndex} className="flex items-center gap-2">
                        <div className="relative flex-1">
                          <Input
                            placeholder={wordIndex === 0 ? "✓ Correct word" : `Distractor ${wordIndex}`}
                            value={word}
                            onChange={(e) => updateWordBankWord(blankIndex, wordIndex, e.target.value)}
                            disabled={isLocked}
                            className={`bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-600 ${
                              wordIndex === 0 ? "border-green-500 dark:border-green-600 ring-1 ring-green-500/20" : ""
                            } ${isLocked ? "bg-gray-100 dark:bg-gray-700 cursor-not-allowed" : ""}`}
                          />
                          {isLocked ? (
                            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-gray-500 dark:text-gray-400">
                              🔒 Edit in source
                            </span>
                          ) : wordIndex === 0 ? (
                            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-green-600 dark:text-green-400 font-medium">
                              Correct
                            </span>
                          ) : null}
                        </div>
                        {input.words.length > 2 && !isLocked && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            onClick={() => removeWordBankWord(blankIndex, wordIndex)}
                            className="text-gray-500 hover:text-red-600 dark:text-gray-400 dark:hover:text-red-400 h-9 w-9 shrink-0"
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    )
                  })}
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => addWordBankWord(blankIndex)}
                  className="w-full bg-transparent border-dashed border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  <Plus className="h-4 w-4 mr-2" />
                  Add Distractor
                </Button>
              </div>
            )}
          </div>
        )
      })}

      {/* Word Bank Preview */}
      {hasWordBankBlanks && allWordBankWords.length > 0 && (
        <div className="border border-purple-200 dark:border-purple-800 rounded-lg p-4 bg-purple-50 dark:bg-purple-950/30">
          <Label className="text-sm font-medium text-purple-700 dark:text-purple-300 mb-3 block">
            📦 Word Bank Preview (shuffled in quiz)
          </Label>
          <div className="flex flex-wrap gap-2">
            {allWordBankWords.map((word, index) => (
              <span
                key={index}
                className="px-3 py-1.5 bg-white dark:bg-gray-800 border border-purple-300 dark:border-purple-700 rounded-lg text-sm text-gray-700 dark:text-gray-300 shadow-sm"
              >
                {word}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
