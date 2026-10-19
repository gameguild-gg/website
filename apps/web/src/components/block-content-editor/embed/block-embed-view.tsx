"use client"

/**
 * Surface-agnostic React shell for an embedded block.
 *
 * Renders the preview registered for `block.type` and, when `editable`,
 * overlays minimal affordances (Editar / Remover) that delegate to the
 * surface (Lexical decorator today, Markdown renderer in the future) via
 * the `onChange` / `onRemove` callbacks.
 *
 * MUST NOT import from `lexical`, `@lexical/*`, or anything under
 * `../nodes/` / `../plugins/`.
 */

import { useCallback, useEffect, useRef, useState } from "react"
import { Pencil, Trash2 } from "lucide-react"
import { Button } from "@game-guild/ui/components/button"
import { cn } from "@/lib/utils"

import { BlockEditorModal } from "../engines/blocks/block-editor-modal"
import { EMBEDDABLE_BLOCK_CONFIG } from "./block-embed-registry"
import { isEmbeddableBlockType } from "./embeddable-blocks"
import type { EmbeddableBlock, EmbeddableBlockData } from "./types"

interface BlockEmbedViewProps {
  block: EmbeddableBlock
  editable: boolean
  /** Called with the new `data` after the user saves the inline editor. */
  onChange?: (data: EmbeddableBlockData) => void
  /** Called when the user clicks the Remove affordance. */
  onRemove?: () => void
  /**
   * When true, opens the editor automatically once if `block.data.isNew`
   * is truthy. Default true.
   */
  autoOpenIfNew?: boolean
}

export function BlockEmbedView({
  block,
  editable,
  onChange,
  onRemove,
  autoOpenIfNew = true,
}: BlockEmbedViewProps) {
  const [editorOpen, setEditorOpen] = useState(false)
  const autoOpenedRef = useRef(false)

  // Auto-open editor for newly inserted blocks (one-shot).
  useEffect(() => {
    if (!editable || !autoOpenIfNew || autoOpenedRef.current) return
    const data = block.data as { isNew?: boolean } | null
    if (data && data.isNew) {
      autoOpenedRef.current = true
      setEditorOpen(true)
    }
  }, [editable, autoOpenIfNew, block.data])

  const handleSave = useCallback(
    (newData: unknown) => {
      // Strip transient `isNew` flag on first save, matching the block array
      // engine convention.
      const cleaned =
        newData && typeof newData === "object" && "isNew" in newData
          ? { ...(newData as unknown as Record<string, unknown>), isNew: undefined }
          : newData
      onChange?.(cleaned as EmbeddableBlockData)
      setEditorOpen(false)
    },
    [onChange],
  )

  if (!isEmbeddableBlockType(block.type)) {
    // Defensive: shouldn't happen — surface code only stores embeddable types.
    return null
  }

  const entry = EMBEDDABLE_BLOCK_CONFIG[block.type]
  // The registry keys are strictly typed, so this is safe at runtime; the
  // generic preview component accepts the matching Block shape.
  const Preview = entry.Preview as React.ComponentType<{ block: EmbeddableBlock }>

  return (
    <div
      className={cn(
        "group relative my-2 rounded-lg",
        editable && "ring-1 ring-transparent transition-shadow hover:ring-border",
      )}
      data-block-embed
      data-block-type={block.type}
    >
      <Preview block={block} />

      {editable && (
        <div className="pointer-events-none absolute right-2 top-2 flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100">
          <Button
            type="button"
            size="icon"
            variant="secondary"
            aria-label="Editar bloco embedado"
            title="Editar"
            className="pointer-events-auto h-7 w-7 shadow-md"
            onClick={(e) => {
              e.preventDefault()
              e.stopPropagation()
              setEditorOpen(true)
            }}
          >
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <Button
            type="button"
            size="icon"
            variant="destructive"
            aria-label="Remover bloco embedado"
            title="Remover"
            className="pointer-events-auto h-7 w-7 shadow-md"
            onClick={(e) => {
              e.preventDefault()
              e.stopPropagation()
              onRemove?.()
            }}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      )}

      {editable && editorOpen && (
        <BlockEditorModal
          open={editorOpen}
          onOpenChange={setEditorOpen}
          block={block}
          blockType={block.type}
          onSave={handleSave}
        />
      )}
    </div>
  )
}
