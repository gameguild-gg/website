/**
 * Ordering Renderer
 * Drag and drop items to arrange in correct order
 */

"use client"

import type { RendererAnswerState } from "../../player/renderer-answer-adapter"

import { useState, useEffect } from "react"
import type { OrderingEntry } from "@game-guild/quiz"
import type { OrderingLearnerEntry } from "@game-guild/quiz"

interface OrderingRendererProps {
  entry: OrderingEntry | OrderingLearnerEntry
  answerState: RendererAnswerState
  onAnswerChange: (updates: Partial<RendererAnswerState>) => void
  disabled?: boolean
  showFeedback?: boolean
}

export function OrderingRenderer({
  entry,
  answerState,
  onAnswerChange,
  disabled = false,
  showFeedback = false,
}: OrderingRendererProps) {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null)

  // Initialize ordering if empty
  useEffect(() => {
    if (answerState.ordering.length === 0 && entry.items.length > 0) {
      // Shuffle items for initial display
      const shuffled = [...entry.items]
        .sort(() => Math.random() - 0.5)
        .map((item) => item.id)
      onAnswerChange({ ordering: shuffled })
    }
  }, [entry.items, answerState.ordering.length, onAnswerChange])

  const currentOrder = answerState.ordering.length > 0
    ? answerState.ordering
    : entry.items.map((item) => item.id)

  const orderedItems = currentOrder.map(
    (id) => entry.items.find((item) => item.id === id)!
  ).filter(Boolean)

  const handleDragStart = (index: number) => {
    if (disabled || showFeedback) return
    setDraggedIndex(index)
  }

  const handleDragOver = (e: React.DragEvent, index: number) => {
    e.preventDefault()
    if (disabled || showFeedback || draggedIndex === null || draggedIndex === index) return

    const newOrder = [...currentOrder]
    const [draggedId] = newOrder.splice(draggedIndex, 1)
    newOrder.splice(index, 0, draggedId!)

    setDraggedIndex(index)
    onAnswerChange({ ordering: newOrder })
  }

  const handleDragEnd = () => {
    setDraggedIndex(null)
  }

  const handleMoveUp = (index: number) => {
    if (disabled || showFeedback || index === 0) return
    const newOrder = [...currentOrder]
    ;[newOrder[index - 1], newOrder[index]] = [newOrder[index]!, newOrder[index - 1]!]
    onAnswerChange({ ordering: newOrder })
  }

  const handleMoveDown = (index: number) => {
    if (disabled || showFeedback || index === currentOrder.length - 1) return
    const newOrder = [...currentOrder]
    ;[newOrder[index], newOrder[index + 1]] = [newOrder[index + 1]!, newOrder[index]!]
    onAnswerChange({ ordering: newOrder })
  }

  return (
    <div className="space-y-2">
      <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
        Drag items to reorder or use the arrows
      </p>

      {orderedItems.map((item, index) => (
        <div
          key={item.id}
          data-testid={`ordering-item-${item.id}`}
          draggable={!disabled && !showFeedback}
          onDragStart={() => handleDragStart(index)}
          onDragOver={(e) => handleDragOver(e, index)}
          onDragEnd={handleDragEnd}
          className={`
            flex items-center gap-3 p-4 rounded-lg border-2 transition-all
            ${draggedIndex === index ? "opacity-50 border-blue-500" : "border-gray-200 dark:border-gray-700"}
            ${!disabled && !showFeedback ? "cursor-move hover:border-gray-300 dark:hover:border-gray-600" : "cursor-not-allowed"}
          `}
        >
          <span className="flex items-center justify-center w-8 h-8 rounded-full bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400 font-medium text-sm">
            {index + 1}
          </span>

          <span className="flex-1 font-medium">{item.text}</span>

          {!disabled && !showFeedback && (
            <div className="flex flex-col gap-1">
              <button
                type="button"
                aria-label={`Move ${item.text} up`}
                title={`Move ${item.text} up`}
                onClick={() => handleMoveUp(index)}
                disabled={index === 0}
                className={`
                  p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors
                  ${index === 0 ? "opacity-30 cursor-not-allowed" : ""}
                `}
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 15l7-7 7 7" />
                </svg>
              </button>
              <button
                type="button"
                aria-label={`Move ${item.text} down`}
                title={`Move ${item.text} down`}
                onClick={() => handleMoveDown(index)}
                disabled={index === orderedItems.length - 1}
                className={`
                  p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors
                  ${index === orderedItems.length - 1 ? "opacity-30 cursor-not-allowed" : ""}
                `}
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>
            </div>
          )}
        </div>
      ))}
    </div>
  )
}
