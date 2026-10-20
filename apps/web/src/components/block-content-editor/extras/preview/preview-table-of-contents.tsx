"use client"

import type React from "react"

import { ScrollArea } from "@game-guild/ui/components/scroll-area"
import { ChevronDown, ChevronRight, List, Settings } from "lucide-react"
import type { SerializedEditorState, SerializedLexicalNode } from "lexical"
import { useEffect, useState } from "react"

interface OutlineNode extends SerializedLexicalNode {
  tag?: string
  text?: string
  children?: OutlineNode[]
}

interface HeadingItem {
  id: string
  text: string
  level: number
  element?: HTMLElement
}

interface PreviewTableOfContentsProps {
  serializedState: SerializedEditorState
  className?: string
}

type DisplayMode = "h1h2" | "h1h2h3" | "all"

export function PreviewTableOfContents({ serializedState, className = "" }: PreviewTableOfContentsProps) {
  const [headings, setHeadings] = useState<HeadingItem[]>([])
  const [activeHeading, setActiveHeading] = useState<string>("")
  const [displayMode, setDisplayMode] = useState<DisplayMode>("h1h2h3") // Default to option 2
  const [expandedHeadings, setExpandedHeadings] = useState<Set<string>>(new Set())
  const [showModeSelector, setShowModeSelector] = useState(false)

  // Extract headings from lexical data
  useEffect(() => {
    if (!serializedState?.root?.children) {
      setHeadings([])
      return
    }

    const extractHeadings = (nodes: OutlineNode[], level = 0): HeadingItem[] => {
      const headingItems: HeadingItem[] = []
      let headingIndex = 0 // Use proper counter starting from 0

      const processNode = (node: OutlineNode) => {
        if (node.type === "heading" && node.tag) {
          const headingLevel = Number.parseInt(node.tag.replace("h", ""))
          const text = extractTextFromNode(node)
          if (text.trim()) {
            const id = `heading-${headingIndex}`
            console.log("[v0] TOC - Generated heading ID:", id)
            console.log("[v0] TOC - Heading index:", headingIndex)
            console.log("[v0] TOC - Heading text:", text.trim())
            headingItems.push({
              id,
              text: text.trim(),
              level: headingLevel,
            })
            headingIndex++ // Increment after creating the heading
          }
        }

        // Recursively process children
        if (node.children && Array.isArray(node.children)) {
          node.children.forEach(processNode)
        }
      }

      serializedState.root.children.forEach(processNode)
      return headingItems
    }

    const extractTextFromNode = (node: OutlineNode): string => {
      if (node.type === "text") {
        return node.text || ""
      }

      if (node.children && Array.isArray(node.children)) {
        return node.children.map(extractTextFromNode).join("")
      }

      return ""
    }

    const extractedHeadings = extractHeadings(serializedState.root.children)
    setHeadings(extractedHeadings)
  }, [serializedState])

  const scrollToHeading = (headingId: string) => {
    console.log("[v0] Attempting to scroll to heading:", headingId)

    setActiveHeading(headingId)

    const element = document.querySelector(`[data-heading-id="${headingId}"]`)
    console.log("[v0] Found element:", element)

    if (element) {
      console.log("[v0] Scrolling to element")
      const elementRect = element.getBoundingClientRect()
      const absoluteElementTop = elementRect.top + window.pageYOffset
      const offset = 80 // Account for any fixed headers or padding

      window.scrollTo({
        top: absoluteElementTop - offset,
        behavior: "smooth",
      })
    } else {
      console.log("[v0] Element not found, checking all heading elements:")
      const allHeadings = document.querySelectorAll("[data-heading-id]")
      allHeadings.forEach((el, index) => {
        console.log(`[v0] Heading ${index}:`, el.getAttribute("data-heading-id"))
      })
    }
  }

  const shouldShowHeading = (heading: HeadingItem, index: number): boolean => {
    if (displayMode === "all") return true

    const maxLevel = displayMode === "h1h2" ? 2 : 3

    if (heading.level <= maxLevel) return true

    // Check if any parent heading is expanded
    for (let i = index - 1; i >= 0; i--) {
      const parentHeading = headings[i]
      if (parentHeading && parentHeading.level < heading.level) {
        if (parentHeading.level <= maxLevel && expandedHeadings.has(parentHeading.id)) {
          return true
        }
        break
      }
    }

    return false
  }

  const hasExpandableChildren = (heading: HeadingItem, index: number): boolean => {
    if (displayMode === "all") return false

    const maxLevel = displayMode === "h1h2" ? 2 : 3
    if (heading.level > maxLevel) return false

    for (let i = index + 1; i < headings.length; i++) {
      const nextHeading = headings[i]
      if (!nextHeading) break
      if (nextHeading.level <= heading.level) break
      if (nextHeading.level > maxLevel) return true
    }

    return false
  }

  const toggleExpanded = (headingId: string, event: React.MouseEvent) => {
    event.stopPropagation()
    const newExpanded = new Set(expandedHeadings)
    if (newExpanded.has(headingId)) {
      newExpanded.delete(headingId)
    } else {
      newExpanded.add(headingId)
    }
    setExpandedHeadings(newExpanded)
  }

  if (headings.length === 0) {
    return (
      <div
        className={`bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 shadow-sm max-h-[calc(100vh-7rem)] flex flex-col ${className}`}
      >
        <div className="p-6">
          <div className="flex items-center gap-2 mb-4">
            <List className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <h3 className="font-semibold text-gray-900 dark:text-gray-100">Table of Contents</h3>
          </div>
          <p className="text-sm text-gray-500 dark:text-gray-400">No headings found in this document</p>
        </div>
      </div>
    )
  }

  return (
    <div
      className={`bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 shadow-sm max-h-[calc(100vh-7rem)] flex flex-col ${className}`}
    >
      {/* Header */}
      <div className="p-6 border-b border-gray-200 dark:border-gray-700 shrink-0 bg-gray-50 dark:bg-gray-900">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <List className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <h3 className="font-semibold text-gray-900 dark:text-gray-100">Table of Contents</h3>
          </div>

          <div className="relative">
            <button
              onClick={() => setShowModeSelector(!showModeSelector)}
              className="p-1 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
              title="Display options"
            >
              <Settings className="w-4 h-4 text-gray-500 dark:text-gray-400" />
            </button>

            {showModeSelector && (
              <div className="absolute right-0 top-8 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 shadow-lg z-10 min-w-48">
                <div className="p-2 space-y-1">
                  <button
                    onClick={() => {
                      setDisplayMode("h1h2")
                      setShowModeSelector(false)
                      setExpandedHeadings(new Set())
                    }}
                    className={`w-full text-left px-3 py-2 text-sm transition-colors ${displayMode === "h1h2"
                        ? "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300"
                        : "hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-700 dark:text-gray-300"
                      }`}
                  >
                    H1 & H2 only
                  </button>
                  <button
                    onClick={() => {
                      setDisplayMode("h1h2h3")
                      setShowModeSelector(false)
                      setExpandedHeadings(new Set())
                    }}
                    className={`w-full text-left px-3 py-2 text-sm transition-colors ${displayMode === "h1h2h3"
                        ? "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300"
                        : "hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-700 dark:text-gray-300"
                      }`}
                  >
                    H1, H2 & H3
                  </button>
                  <button
                    onClick={() => {
                      setDisplayMode("all")
                      setShowModeSelector(false)
                      setExpandedHeadings(new Set())
                    }}
                    className={`w-full text-left px-3 py-2 text-sm transition-colors ${displayMode === "all"
                        ? "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300"
                        : "hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-700 dark:text-gray-300"
                      }`}
                  >
                    Show all
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Scrollable Navigation */}
      <ScrollArea className="flex-1 p-6 overflow-auto">
        <nav className="space-y-1">
          {headings.map((heading, index) => {
            if (!shouldShowHeading(heading, index)) return null

            const hasChildren = hasExpandableChildren(heading, index)
            const isExpanded = expandedHeadings.has(heading.id)

            return (
              <div key={heading.id} className="relative">
                <button
                  onClick={() => scrollToHeading(heading.id)}
                  className={`
                    w-full text-left px-3 py-2 rounded-lg text-sm transition-colors
                    hover:bg-gray-100 dark:hover:bg-gray-800
                    ${activeHeading === heading.id
                      ? "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border-l-2 border-blue-500"
                      : "text-gray-700 dark:text-gray-300"
                    }
                  `}
                  style={{
                    paddingLeft: `${Math.max(0.75, heading.level * 0.5)}rem`,
                    marginLeft: `${(heading.level - 1) * 0.5}rem`,
                  }}
                >
                  <div className="flex items-center gap-2">
                    {hasChildren ? (
                      <div
                        onClick={(e) => toggleExpanded(heading.id, e)}
                        className="p-0.5 hover:bg-gray-200 dark:hover:bg-gray-700 rounded transition-colors cursor-pointer"
                      >
                        {isExpanded ? (
                          <ChevronDown className="w-3 h-3 text-gray-400 dark:text-gray-500" />
                        ) : (
                          <ChevronRight className="w-3 h-3 text-gray-400 dark:text-gray-500" />
                        )}
                      </div>
                    ) : heading.level > 1 ? (
                      <ChevronRight className="w-3 h-3 text-gray-400 dark:text-gray-500 shrink-0" />
                    ) : null}
                    <span className="truncate">{heading.text}</span>
                  </div>
                </button>
              </div>
            )
          })}
        </nav>
      </ScrollArea>
    </div>
  )
}
