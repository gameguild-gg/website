"use client"

/**
 * Markdown block editor.
 *
 * Layout: Monaco source on the left, live `MarkdownRenderer` preview on the right.
 *
 * Embed insertion (mirrors the Lexical surface UX):
 *   1. Header "Insert Block" button → opens the shared `BlockTypePicker`.
 *   2. Inline slash menu — typing `/` in Monaco at the start of a line or
 *      after whitespace opens a typeahead listing every embeddable block
 *      type. Filtering is live (typed text after `/`), arrow keys move
 *      the selection, Enter inserts, Esc closes.
 *
 * Selection replaces the `/query` range with a `<block-embed id="..."/>`
 * token and stores the freshly-created block in `embeds`. Unused embeds
 * are pruned on save.
 */

import { useCallback, useEffect, useRef, useState } from "react"
import { createPortal } from "react-dom"
import type * as MonacoNS from "monaco-editor"

import { Button } from "@game-guild/ui/components/button"
import { Save, FileText, Blocks } from "lucide-react"
import { useTheme } from "next-themes"

import { BaseMonacoEditor } from "@/components/block-content-editor/lib/monaco"
import type { MarkdownData } from "@/components/block-content-editor/nodes/markdown-node"
import type { Block } from "@/components/block-content-editor/lib/storage/editor/block-structure"
import { useEditorSettings } from "../settings-menu"
import { BlockEditorShell } from "@/components/block-content-editor/extras/block-editor-shell"
import { MarkdownRenderer, buildEmbedToken, pruneUnusedEmbeds } from "./markdown-renderer"
import { BlockTypePicker } from "@/components/block-content-editor/engines/blocks/block-type-picker"
import {
  EMBEDDABLE_BLOCK_TYPES,
  type EmbeddableBlock,
  type EmbeddableBlockData,
  type EmbeddableBlockType,
} from "@/components/block-content-editor/embed/types"
import { BLOCK_REGISTRY } from "@/components/block-content-editor/engines/blocks/block-component-registry"
import { BLOCK_EMBED_TOKEN_RE } from "./markdown-renderer"
import { cn } from "@/lib/utils"

// ---------------------------------------------------------------------------
// Slash menu types
// ---------------------------------------------------------------------------

type IStandaloneCodeEditor = MonacoNS.editor.IStandaloneCodeEditor

interface SlashMenuState {
  query: string
  anchor: { top: number; left: number }
  /** Range covering `/query` so we can replace it on accept. */
  range: MonacoNS.IRange
}

/** Match `/foo` at end of the line, preceded by start-of-line or whitespace. */
const SLASH_TRIGGER_RE = /(?:^|\s)\/([\w-]*)$/

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

interface MarkdownEditorProps {
  initialData?: MarkdownData
  onSave: (data: MarkdownData) => void
  onCancel: () => void
}

export function MarkdownEditor({ initialData, onSave, onCancel }: MarkdownEditorProps) {
  const { resolvedTheme } = useTheme()
  const isDarkMode = resolvedTheme === "dark"

  const [content, setContent] = useState(initialData?.content || "")
  const [embeds, setEmbeds] = useState<Record<string, EmbeddableBlock>>(
    initialData?.embeds ?? {},
  )
  const [showBlockPicker, setShowBlockPicker] = useState(false)
  const [slashMenu, setSlashMenu] = useState<SlashMenuState | null>(null)
  const [highlightedIndex, setHighlightedIndex] = useState(0)

  const settings = useEditorSettings("markdown")
  const editorRef = useRef<IStandaloneCodeEditor | null>(null)
  const slashMenuRef = useRef<SlashMenuState | null>(null)
  const highlightedRef = useRef(0)
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([])

  // Mirror state into refs so Monaco event handlers see the latest values
  // without re-binding every render.
  useEffect(() => {
    slashMenuRef.current = slashMenu
  }, [slashMenu])
  useEffect(() => {
    highlightedRef.current = highlightedIndex
    const el = optionRefs.current[highlightedIndex]
    if (el) el.scrollIntoView({ block: "nearest" })
  }, [highlightedIndex])

  // -------------------------------------------------------------------------
  // Slash menu — options
  // -------------------------------------------------------------------------

  const allOptions = EMBEDDABLE_BLOCK_TYPES
    // Markdown cannot embed itself — avoids infinite-nesting UX and editing loops.
    .filter((type) => type !== "markdown")
    .map((type) => {
      const entry = BLOCK_REGISTRY[type]
      return {
        type,
        title: entry.label,
        description: entry.description,
        Icon: entry.icon,
      }
    })

  const filteredOptions = (() => {
    const q = (slashMenu?.query ?? "").toLowerCase()
    if (!q) return allOptions
    return allOptions.filter(
      (o) =>
        o.title.toLowerCase().includes(q) ||
        o.description.toLowerCase().includes(q) ||
        o.type.toLowerCase().includes(q),
    )
  })()

  // Keep highlight in range when the filter changes.
  useEffect(() => {
    if (highlightedIndex >= filteredOptions.length) {
      setHighlightedIndex(0)
    }
  }, [filteredOptions.length, highlightedIndex])

  // -------------------------------------------------------------------------
  // Insert helpers
  // -------------------------------------------------------------------------

  const insertEmbedBlock = useCallback(
    (block: Block, replaceRange?: MonacoNS.IRange) => {
      const embeddable = block as EmbeddableBlock
      setEmbeds((prev) => ({ ...prev, [block.id]: embeddable }))

      const token = buildEmbedToken(block.id)
      const editor = editorRef.current
      if (!editor) {
        setContent((c) => `${c}\n${token}\n`)
        return
      }
      const model = editor.getModel()
      if (!model) return

      if (replaceRange) {
        editor.executeEdits("insert-embed", [
          { range: replaceRange, text: token, forceMoveMarkers: true },
        ])
      } else {
        const position = editor.getPosition()
        if (!position) {
          setContent((c) => `${c}\n${token}\n`)
          return
        }
        editor.executeEdits("insert-embed", [
          {
            range: {
              startLineNumber: position.lineNumber,
              startColumn: position.column,
              endLineNumber: position.lineNumber,
              endColumn: position.column,
            },
            text: `\n${token}\n`,
            forceMoveMarkers: true,
          },
        ])
      }
      setContent(model.getValue())
      editor.focus()
    },
    [],
  )

  const handlePickerSelect = useCallback(
    (factory: (id: string) => Block) => {
      const block = factory(crypto.randomUUID())
      insertEmbedBlock(block)
      setShowBlockPicker(false)
    },
    [insertEmbedBlock],
  )

  /**
   * Persist edits made through the inline `BlockEmbedView` editor.
   * The block keeps its id; only `data` changes.
   */
  const handleEmbedChange = useCallback(
    (id: string, data: EmbeddableBlockData) => {
      setEmbeds((prev) => {
        const existing = prev[id]
        if (!existing) return prev
        return { ...prev, [id]: { ...existing, data } as EmbeddableBlock }
      })
    },
    [],
  )

  /**
   * Remove an embed both from the payload map and from the markdown source.
   * Strips every `<block-embed id="<id>" />` occurrence (defensive: usually 1).
   */
  const handleEmbedRemove = useCallback((id: string) => {
    setEmbeds((prev) => {
      if (!(id in prev)) return prev
      const next = { ...prev }
      delete next[id]
      return next
    })
    setContent((prev) => {
      const next = prev.replace(BLOCK_EMBED_TOKEN_RE, (full, capturedId) =>
        capturedId === id ? "" : full,
      )
      const editor = editorRef.current
      if (editor) {
        const model = editor.getModel()
        if (model && model.getValue() !== next) {
          model.setValue(next)
        }
      }
      return next
    })
  }, [])

  const handleSlashSelect = useCallback(
    (type: EmbeddableBlockType) => {
      const state = slashMenuRef.current
      if (!state) return
      const config = BLOCK_REGISTRY[type]
      const block = config.createEmpty(crypto.randomUUID())
      insertEmbedBlock(block, state.range)
      setSlashMenu(null)
      setHighlightedIndex(0)
    },
    [insertEmbedBlock],
  )

  // -------------------------------------------------------------------------
  // Monaco wiring: detect slash trigger + capture nav keys
  // -------------------------------------------------------------------------

  const updateSlashMenuFromCursor = useCallback((editor: IStandaloneCodeEditor) => {
    const model = editor.getModel()
    const position = editor.getPosition()
    if (!model || !position) {
      setSlashMenu(null)
      return
    }
    const lineContent = model.getLineContent(position.lineNumber)
    const before = lineContent.slice(0, position.column - 1)
    const match = before.match(SLASH_TRIGGER_RE)
    if (!match) {
      setSlashMenu(null)
      return
    }
    const query = match[1] ?? ""
    // The `/` character itself starts at the captured group's index - 1
    // (the optional leading whitespace doesn't consume `/`).
    const slashIndex = before.length - query.length - 1
    const range: MonacoNS.IRange = {
      startLineNumber: position.lineNumber,
      startColumn: slashIndex + 1,
      endLineNumber: position.lineNumber,
      endColumn: position.column,
    }

    const visible = editor.getScrolledVisiblePosition(position)
    const domNode = editor.getDomNode()
    if (!visible || !domNode) {
      setSlashMenu(null)
      return
    }
    const rect = domNode.getBoundingClientRect()
    setSlashMenu({
      query,
      anchor: {
        top: rect.top + visible.top + visible.height + 4,
        left: rect.left + visible.left,
      },
      range,
    })
  }, [])

  const handleEditorMount = useCallback(
    (editor: IStandaloneCodeEditor) => {
      editorRef.current = editor

      editor.onDidChangeModelContent(() => {
        updateSlashMenuFromCursor(editor)
      })
      editor.onDidChangeCursorPosition(() => {
        updateSlashMenuFromCursor(editor)
      })
      editor.onDidScrollChange(() => {
        // Re-anchor or close while scrolling — simpler to close.
        if (slashMenuRef.current) setSlashMenu(null)
      })
      editor.onDidBlurEditorWidget(() => {
        // Delay so click on the menu still registers.
        setTimeout(() => setSlashMenu(null), 120)
      })

      editor.onKeyDown((e) => {
        const menu = slashMenuRef.current
        if (!menu) return
        const opts = filteredOptionsRef.current
        if (opts.length === 0) return

        // Browser keyCode mapping: ArrowUp=38 ArrowDown=40 Enter=13
        // Tab=9 Escape=27. Monaco's IKeyboardEvent.keyCode is its OWN enum,
        // so use `browserEvent.key` for reliability.
        const key = e.browserEvent.key
        if (key === "ArrowDown") {
          e.preventDefault()
          e.stopPropagation()
          setHighlightedIndex((i) => (i + 1) % opts.length)
        } else if (key === "ArrowUp") {
          e.preventDefault()
          e.stopPropagation()
          setHighlightedIndex((i) => (i - 1 + opts.length) % opts.length)
        } else if (key === "Enter" || key === "Tab") {
          e.preventDefault()
          e.stopPropagation()
          const selected = opts[highlightedRef.current]
          if (selected) handleSlashSelect(selected.type)
        } else if (key === "Escape") {
          e.preventDefault()
          e.stopPropagation()
          setSlashMenu(null)
        }
      })
    },
    [updateSlashMenuFromCursor, handleSlashSelect],
  )

  // Keep filtered options reachable from the keydown handler without
  // rebinding it on every keystroke.
  const filteredOptionsRef = useRef(filteredOptions)
  useEffect(() => {
    filteredOptionsRef.current = filteredOptions
  }, [filteredOptions])

  // -------------------------------------------------------------------------
  // Save / cancel
  // -------------------------------------------------------------------------

  const handleSave = () => {
    onSave({
      content,
      embeds: pruneUnusedEmbeds(content, embeds),
      title: initialData?.title,
      caption: initialData?.caption,
    })
  }

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------

  return (
    <>
      <BlockEditorShell
        settings={settings}
        onClose={onCancel}
        icon={<FileText className="h-5 w-5 text-blue-600 dark:text-blue-400" />}
        title="Markdown Editor"
        headerActions={
          <Button
            variant="outline"
            size="sm"
            onClick={() => setShowBlockPicker(true)}
            className="border-gray-300 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <Blocks className="h-4 w-4 mr-1" />
            Insert Block
          </Button>
        }
        footer={
          <div className="flex gap-2 justify-end">
            <Button
              variant="outline"
              onClick={onCancel}
              className="border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 bg-transparent"
            >
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-600"
            >
              <Save className="h-4 w-4" />
              Save Markdown
            </Button>
          </div>
        }
      >
        <div className="flex-1 overflow-hidden flex">
          {/* Left — Monaco source */}
          <div className="w-1/2 border-r border-gray-200 dark:border-gray-800 flex flex-col">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="text-sm font-medium text-gray-800 dark:text-gray-200 uppercase tracking-wide">
                Editor
              </h3>
            </div>
            <div className="flex-1 overflow-hidden">
              <BaseMonacoEditor
                language="markdown"
                value={content}
                onChange={(value) => setContent(value || "")}
                onMount={handleEditorMount}
                isDark={isDarkMode}
                options={settings.editor}
                extraOptions={{ roundedSelection: true }}
              />
            </div>
          </div>

          {/* Right — live preview */}
          <div className="w-1/2 flex flex-col">
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900">
              <h3 className="text-sm font-medium text-gray-800 dark:text-gray-200 uppercase tracking-wide">
                Live Preview
              </h3>
            </div>
            <div className="flex-1 overflow-auto p-6 bg-white dark:bg-gray-950">
              <MarkdownRenderer
                content={content}
                embeds={embeds}
                editable
                onEmbedChange={handleEmbedChange}
                onEmbedRemove={handleEmbedRemove}
                className=""
                emptyFallback={
                  <p className="text-gray-400 dark:text-gray-600 italic">
                    Your markdown preview will appear here. Type{" "}
                    <code className="px-1 py-0.5 rounded bg-gray-100 dark:bg-gray-800">/</code>{" "}
                    to insert a block.
                  </p>
                }
              />
            </div>
          </div>
        </div>
      </BlockEditorShell>

      <BlockTypePicker
        open={showBlockPicker}
        onOpenChange={setShowBlockPicker}
        onSelect={handlePickerSelect}
        allowedBlockTypes={
          EMBEDDABLE_BLOCK_TYPES.filter((t) => t !== "markdown") as EmbeddableBlockType[]
        }
      />

      {slashMenu && filteredOptions.length > 0 && typeof document !== "undefined"
        ? createPortal(
            <div
              className={cn(
                "fixed z-80 min-w-[280px] max-h-[360px] overflow-y-auto",
                "rounded-md border-2 border-blue-500/40 bg-popover text-popover-foreground shadow-2xl",
              )}
              style={{ top: slashMenu.anchor.top, left: slashMenu.anchor.left }}
              role="listbox"
              onMouseDown={(e) => e.preventDefault()}
            >
              <div className="p-1">
                {filteredOptions.map((option, i) => {
                  const Icon = option.Icon
                  const isSelected = highlightedIndex === i
                  return (
                    <button
                      key={option.type}
                      ref={(el) => {
                        optionRefs.current[i] = el
                      }}
                      type="button"
                      role="option"
                      aria-selected={isSelected}
                      tabIndex={-1}
                      onMouseEnter={() => setHighlightedIndex(i)}
                      onClick={() => handleSlashSelect(option.type)}
                      className={cn(
                        "relative flex w-full items-start gap-2 rounded-sm px-2 py-1.5 text-left text-sm outline-none transition-colors",
                        isSelected
                          ? "bg-blue-600 text-white ring-2 ring-blue-400 ring-inset"
                          : "hover:bg-accent/60",
                      )}
                    >
                      <Icon
                        className={cn(
                          "h-4 w-4 mt-0.5 shrink-0",
                          isSelected ? "text-white" : "text-muted-foreground",
                        )}
                      />
                      <div className="min-w-0 flex-1">
                        <div className="font-medium leading-tight">{option.title}</div>
                        <div
                          className={cn(
                            "text-xs leading-tight truncate",
                            isSelected ? "text-blue-100" : "text-muted-foreground",
                          )}
                        >
                          {option.description}
                        </div>
                      </div>
                    </button>
                  )
                })}
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  )
}
