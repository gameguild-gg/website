/**
 * Fill in the Blank Renderer
 * Renders questions with inline input fields for blanks
 * Supports three modes: Text, Dropdown, and Word Bank (drag-drop)
 */

"use client"

import type { RendererAnswerState } from "../../player/renderer-answer-adapter"

import { useMemo, useState, useCallback } from "react"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@game-guild/ui/components/select"
import type { FillBlankField, FillInTheBlankEntry } from "@game-guild/quiz"
import { FillBlankInputType } from "@game-guild/quiz"
import type { FillBlankLearnerField, FillInTheBlankLearnerEntry } from "@game-guild/quiz"

interface FillBlankRendererProps {
  entry: FillInTheBlankEntry | FillInTheBlankLearnerEntry
  answerState: RendererAnswerState
  onAnswerChange: (updates: Partial<RendererAnswerState>) => void
  disabled?: boolean
  showFeedback?: boolean
}

// Utility to shuffle array
function shuffleArray<T>(array: T[]): T[] {
  const shuffled = [...array]
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    const temp = shuffled[i]
    shuffled[i] = shuffled[j]!
    shuffled[j] = temp!
  }
  return shuffled
}

export function FillBlankRenderer({
  entry,
  answerState,
  onAnswerChange,
  disabled = false,
  showFeedback = false,
}: FillBlankRendererProps) {
  // Split stem by blank markers (___ or _word_)
  const parts = entry.stem.split(/___|\b_[^_]+_\b/)

  // Track which word is being dragged (by unique index)
  const [draggedWordIndex, setDraggedWordIndex] = useState<number | null>(null)

  // Serialize blanks for dependency tracking (detects deep changes)
  const blanksKey = JSON.stringify(entry.blanks)

  // Collect all words from Word Bank blanks and shuffle them (with unique index)
  const wordBankWords = useMemo(() => {
    const words: { word: string; blankId: string; uniqueIndex: number }[] = []
    let idx = 0
    entry.blanks.forEach((blank) => {
      if (blank.input.type === FillBlankInputType.WordBank) {
        blank.input.words.filter(w => w.trim()).forEach((word) => {
          words.push({ word, blankId: blank.id, uniqueIndex: idx++ })
        })
      }
    })
    return shuffleArray(words)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blanksKey])

  // Track which unique indices have been used (stored as "blankId:uniqueIndex")
  const usedWordIndices = useMemo(() => {
    const used = new Set<number>()
    // Parse from textAnswers which stores "word|uniqueIndex"
    entry.blanks.forEach((blank) => {
      if (blank.input.type === FillBlankInputType.WordBank) {
        const answer = answerState.textAnswers[blank.id]
        if (answer && answer.includes("|")) {
          const idx = parseInt(answer.split("|")[1] || "", 10)
          if (!isNaN(idx)) used.add(idx)
        }
      }
    })
    return used
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blanksKey, answerState.textAnswers])

  // Available words (not yet used)
  const availableWords = wordBankWords.filter(({ uniqueIndex }) => !usedWordIndices.has(uniqueIndex))

  const handleInputChange = useCallback((blankId: string, value: string) => {
    if (disabled || showFeedback) return
    onAnswerChange({
      textAnswers: {
        ...answerState.textAnswers,
        [blankId]: value,
      },
    })
  }, [disabled, showFeedback, answerState.textAnswers, onAnswerChange])

  const handleDragStart = useCallback((uniqueIndex: number) => {
    if (disabled || showFeedback) return
    setDraggedWordIndex(uniqueIndex)
  }, [disabled, showFeedback])

  const handleDragEnd = useCallback(() => {
    setDraggedWordIndex(null)
  }, [])

  const handleDrop = useCallback((blankId: string) => {
    if (draggedWordIndex !== null && !disabled && !showFeedback) {
      // Find the word by unique index
      const wordItem = wordBankWords.find(w => w.uniqueIndex === draggedWordIndex)
      if (wordItem) {
        // Store as "word|uniqueIndex" to track which specific instance was used
        handleInputChange(blankId, `${wordItem.word}|${wordItem.uniqueIndex}`)
      }
      setDraggedWordIndex(null)
    }
  }, [draggedWordIndex, disabled, showFeedback, handleInputChange, wordBankWords])

  const handleRemoveFromBlank = useCallback((blankId: string) => {
    if (disabled || showFeedback) return
    handleInputChange(blankId, "")
  }, [disabled, showFeedback, handleInputChange])

  // Pre-shuffle dropdown options (memoized per blank)
  const shuffledDropdownOptions = useMemo(() => {
    const map: Record<string, string[]> = {}
    entry.blanks.forEach((blank) => {
      if (blank.input.type === FillBlankInputType.Dropdown) {
        map[blank.id] = shuffleArray(blank.input.options.filter(o => o.trim()))
      }
    })
    return map
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blanksKey])

  const renderBlankInput = (blank: FillBlankField | FillBlankLearnerField) => {
    const { input } = blank
    const currentValue = answerState.textAnswers[blank.id] || ""

    switch (input.type) {
      case FillBlankInputType.Text:
        return (
          <input
            type="text"
            className="inline-block w-40 mx-2 px-3 py-2 border-2 border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:border-blue-500 focus:outline-none transition-colors"
            placeholder="..."
            value={currentValue}
            onChange={(e) => handleInputChange(blank.id, e.target.value)}
            disabled={disabled || showFeedback}
          />
        )

      case FillBlankInputType.Number:
        return (
          <span className="inline-flex items-center mx-2">
            <input
              type={input.requireUnit ? "text" : "number"}
              step="any"
              className="inline-block w-40 px-3 py-2 border-2 border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:border-blue-500 focus:outline-none transition-colors [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
              placeholder={input.requiredPrecision ? `0.${"0".repeat(input.requiredPrecision)}` : "0"}
              value={currentValue}
              onChange={(e) => handleInputChange(blank.id, e.target.value)}
              disabled={disabled || showFeedback}
            />
            {input.unit && !input.requireUnit && (
              <span className="ml-1 text-sm text-gray-500 dark:text-gray-400">{input.unit}</span>
            )}
          </span>
        )

      case FillBlankInputType.Dropdown: {
        const options = shuffledDropdownOptions[blank.id] || []
        return (
          <Select
            value={currentValue}
            onValueChange={(value) => handleInputChange(blank.id, value ?? "")}
            disabled={disabled || showFeedback}
          >
            <SelectTrigger className="inline-flex w-40 mx-2 bg-white dark:bg-gray-800 border-2 border-gray-300 dark:border-gray-600">
              <SelectValue placeholder="Select..." />
            </SelectTrigger>
            <SelectContent>
              {options.map((option, idx) => (
                <SelectItem key={idx} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )

      }
      case FillBlankInputType.WordBank:
        return (
          <span
            className={`inline-flex items-center justify-center min-w-[8rem] h-10 mx-2 px-3 py-2 border-2 border-dashed rounded-lg transition-all ${
              currentValue
                ? "border-purple-500 bg-purple-50 dark:bg-purple-950/50"
                : "border-gray-400 dark:border-gray-500 bg-gray-50 dark:bg-gray-800"
            } ${!disabled && !showFeedback ? "cursor-pointer" : ""}`}
            onDragOver={(e) => {
              e.preventDefault()
              e.currentTarget.classList.add("border-purple-500", "bg-purple-100", "dark:bg-purple-900/50")
            }}
            onDragLeave={(e) => {
              e.currentTarget.classList.remove("border-purple-500", "bg-purple-100", "dark:bg-purple-900/50")
            }}
            onDrop={(e) => {
              e.preventDefault()
              e.currentTarget.classList.remove("border-purple-500", "bg-purple-100", "dark:bg-purple-900/50")
              handleDrop(blank.id)
            }}
            onClick={() => currentValue && handleRemoveFromBlank(blank.id)}
            title={currentValue ? "Click to remove" : "Drag a word here"}
          >
            {currentValue ? (
              <span className="text-purple-700 dark:text-purple-300 font-medium">
                {currentValue.includes("|") ? currentValue.split("|")[0] : currentValue}
              </span>
            ) : (
              <span className="text-gray-400 dark:text-gray-500 text-sm">Drop here</span>
            )}
          </span>
        )
    }
  }

  const hasWordBankBlanks = entry.blanks.some(b => b.input.type === FillBlankInputType.WordBank)

  return (
    <div className="space-y-6">
      {/* Question with inline blanks */}
      <div className="text-lg leading-relaxed flex flex-wrap items-center">
        {parts.map((part, index) => (
          <span key={index} className="flex items-center">
            <span>{part}</span>
            {index < parts.length - 1 && entry.blanks[index] && renderBlankInput(entry.blanks[index])}
          </span>
        ))}
      </div>

      {/* Word Bank Pool */}
      {hasWordBankBlanks && (
        <div className="border-2 border-dashed border-purple-300 dark:border-purple-700 rounded-xl p-4 bg-purple-50/50 dark:bg-purple-950/20">
          <p className="text-sm font-medium text-purple-700 dark:text-purple-300 mb-3">
            📦 Word Bank - Drag words to the blanks
          </p>
          {availableWords.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {availableWords.map(({ word, uniqueIndex }) => (
                <span
                  key={uniqueIndex}
                  draggable={!disabled && !showFeedback}
                  onDragStart={() => handleDragStart(uniqueIndex)}
                  onDragEnd={handleDragEnd}
                  className={`px-4 py-2 bg-white dark:bg-gray-800 border-2 border-purple-300 dark:border-purple-600 rounded-lg text-gray-700 dark:text-gray-300 shadow-sm transition-all ${
                    !disabled && !showFeedback
                      ? "cursor-grab hover:border-purple-500 hover:shadow-md active:cursor-grabbing"
                      : "opacity-70"
                  } ${draggedWordIndex === uniqueIndex ? "opacity-50 scale-95" : ""}`}
                >
                  {word}
                </span>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-2">
              {usedWordIndices.size > 0
                ? "All words have been placed. Click a filled blank to remove."
                : "Add words in the configuration to see them here."}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
