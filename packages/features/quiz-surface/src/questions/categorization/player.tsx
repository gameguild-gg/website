/**
 * Categorization Renderer
 * Drag and drop items into categories
 */

"use client"

import type { RendererAnswerState } from "../../player/renderer-answer-adapter"

import { useState } from "react"
import type { DragEvent } from "react"
import type { CategorizationEntry } from "@game-guild/quiz"
import type { CategorizationLearnerEntry } from "@game-guild/quiz"

interface CategorizationRendererProps {
  entry: CategorizationEntry | CategorizationLearnerEntry
  answerState: RendererAnswerState
  onAnswerChange: (updates: Partial<RendererAnswerState>) => void
  disabled?: boolean
  showFeedback?: boolean
}

export function CategorizationRenderer({
  entry,
  answerState,
  onAnswerChange,
  disabled = false,
  showFeedback = false,
}: CategorizationRendererProps) {
  const [draggedItemId, setDraggedItemId] = useState<string | null>(null)

  // Get items that are not yet assigned to any category
  const unassignedItems = entry.items.filter(
    (item) => !answerState.categorizations[item.id] || answerState.categorizations[item.id]!.length === 0
  )

  const handleDragStart = (event: DragEvent<HTMLDivElement>, itemId: string) => {
    if (disabled || showFeedback) return
    event.dataTransfer.setData("text/plain", itemId)
    event.dataTransfer.effectAllowed = "move"
    setDraggedItemId(itemId)
  }

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    e.currentTarget.classList.add("bg-blue-100", "border-blue-500")
  }

  const handleDragLeave = (e: DragEvent<HTMLDivElement>) => {
    e.currentTarget.classList.remove("bg-blue-100", "border-blue-500")
  }

  const handleDrop = (e: DragEvent<HTMLDivElement>, categoryId: string) => {
    e.preventDefault()
    e.currentTarget.classList.remove("bg-blue-100", "border-blue-500")

    const itemId = e.dataTransfer.getData("text/plain") || draggedItemId
    if (!itemId || disabled || showFeedback) return

    const newCategorizations = { ...answerState.categorizations }
    const currentCategories = newCategorizations[itemId] || []

    if (!currentCategories.includes(categoryId)) {
      newCategorizations[itemId] = [...currentCategories, categoryId]
    }

    onAnswerChange({ categorizations: newCategorizations })
    setDraggedItemId(null)
  }

  const handleRemoveFromCategory = (itemId: string, categoryId: string) => {
    if (disabled || showFeedback) return

    const newCategorizations = { ...answerState.categorizations }
    const currentCategories = newCategorizations[itemId] || []
    newCategorizations[itemId] = currentCategories.filter((id) => id !== categoryId)

    onAnswerChange({ categorizations: newCategorizations })
  }

  const getItemsInCategory = (categoryId: string) => {
    return entry.items.filter((item) => {
      const assignedCategories = answerState.categorizations[item.id] || []
      return assignedCategories.includes(categoryId)
    })
  }

  return (
    <div className="space-y-6">
      {/* Unassigned Items Pool */}
      {unassignedItems.length > 0 && (
        <div className="border rounded-lg p-4 bg-gray-50 dark:bg-gray-800/50">
          <h4 className="text-sm font-medium mb-3 text-gray-700 dark:text-gray-300">
            Items to Categorize
          </h4>
          <div className="flex flex-wrap gap-2">
            {unassignedItems.map((item) => (
              <div
                key={item.id}
                data-testid={`categorization-item-${item.id}`}
                draggable={!disabled && !showFeedback}
                onDragStart={(event) => handleDragStart(event, item.id)}
                onDragEnd={() => setDraggedItemId(null)}
                className={`
                  px-4 py-2 rounded border text-sm font-medium transition-all
                  ${draggedItemId === item.id ? "bg-blue-200 border-blue-400 opacity-50" : "bg-white dark:bg-gray-900 border-gray-300 dark:border-gray-700"}
                  ${!disabled && !showFeedback ? "cursor-move hover:border-blue-400" : "cursor-not-allowed opacity-50"}
                `}
              >
                {item.text}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Categories */}
      <div className="grid gap-4">
        {entry.categories.map((category) => {
          const itemsInCategory = getItemsInCategory(category.id)

          return (
            <div
              key={category.id}
              className="border rounded-lg overflow-hidden bg-white dark:bg-gray-900"
            >
              {/* Category Header */}
              <div className="bg-gray-100 dark:bg-gray-800 p-4 border-b">
                <h4 className="font-medium text-sm text-gray-900 dark:text-gray-100">
                  {category.name}
                </h4>
                {category.description && (
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-1">
                    {category.description}
                  </p>
                )}
              </div>

              {/* Drop Zone */}
              <div
                data-testid={`categorization-drop-${category.id}`}
                className="min-h-24 p-4 border-2 border-dashed border-gray-300 dark:border-gray-600 transition-colors"
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={(e) => handleDrop(e, category.id)}
              >
                {itemsInCategory.length === 0 ? (
                  <div className="text-sm text-gray-400 dark:text-gray-500 text-center py-6">
                    {disabled ? "No items" : "Drag items here"}
                  </div>
                ) : (
                  <div className="space-y-2">
                    {itemsInCategory.map((item) => (
                      <div
                        key={item.id}
                        className="flex items-center justify-between gap-2 px-4 py-2 bg-blue-50 dark:bg-blue-900/20 rounded border border-blue-300 dark:border-blue-700 text-sm"
                      >
                        <span className="font-medium text-gray-900 dark:text-gray-100">
                          {item.text}
                        </span>
                        {!disabled && !showFeedback && (
                          <button
                            onClick={() => handleRemoveFromCategory(item.id, category.id)}
                            className="text-red-600 hover:text-red-700 dark:text-red-400"
                            title="Remove"
                          >
                            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
