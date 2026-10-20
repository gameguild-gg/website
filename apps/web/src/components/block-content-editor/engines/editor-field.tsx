"use client"
/**
 * Main editor field component, used in both the doc editor and block editor.
 * Always renders in container scroll mode — the internal content area scrolls
 * independently while the toolbar/chrome stays fixed.
 */
import { useCallback, useEffect, useRef, useState } from "react"
import { BlockArrayEditor } from "@/components/block-content-editor/engines/blocks/block-array-editor"
import { BLOCK_REGISTRY } from "@/components/block-content-editor/engines/blocks/block-component-registry"
import { nextBlockId } from "@/components/block-content-editor/lib/storage/editor/block-structure"
import { useEditor } from "./editor-provider"
import { LexicalSurface, type PageSettings } from "@game-guild/lexical-surface"
import { cn } from "@/lib/utils"

export interface EditorFieldToolbarContainerLayout {
  /** Tailwind classes for the toolbar outer wrapper. */
  className?: string
  /** Tailwind classes for the toolbar inner wrapper. */
  innerClassName?: string
  /** Whether toolbar and document surface should visually merge. */
  mergeWithContent?: boolean
}

export interface EditorFieldContentContainerLayout {
  /** Tailwind classes for the outer content wrapper. */
  className?: string
  /** Tailwind classes for the scroll wrapper (block mode). */
  scrollClassName?: string
  /** Tailwind classes for the document surface wrapper. */
  documentClassName?: string
  /** Tailwind classes for the blocks surface wrapper. */
  blocksClassName?: string
  /** Optional initial page settings used in document mode. */
  pageSettings?: PageSettings
}

const BASE_CONTAINER_CLASS = "w-full flex flex-col min-h-0 overflow-hidden"
const DEFAULT_CONTENT_CONTAINER_CLASS = "flex-1 h-full max-h-[var(--editor-max-height)]"
const DEFAULT_CONTENT_SCROLL_CLASS = "flex-1 min-h-0"
const DEFAULT_DOC_SURFACE_CLASS =
  "flex-1 min-h-0 w-full max-w-4xl mx-auto bg-white dark:bg-gray-950 border border-gray-200 dark:border-gray-800 shadow-sm rounded-lg"
const DEFAULT_BLOCKS_SURFACE_CLASS =
  "w-full max-w-4xl mx-auto border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 p-4 transition-transform duration-300 ease-in-out"
const DEFAULT_TOOLBAR_CONTAINER_CLASS = "w-full shrink-0 h-auto"
const DEFAULT_TOOLBAR_INNER_CLASS = "w-full max-w-4xl mx-auto"
const DRAG_PREVIEW_WIDTH = "50%"
const DRAG_PREVIEW_SCALE = 0.75
const BLOCKS_SCROLL_BOTTOM_INSET_PX = 256

export interface EditorFieldProps {
  /** Optional additional className for the outermost container. */
  className?: string
  /**
   * Compound 1 — internal Lexical toolbar container.
   * (Applies in document mode; blocks do not render this toolbar.)
   */
  toolbarContainer?: EditorFieldToolbarContainerLayout
  /**
   * Compound 2 — content container (Lexical body or blocks list).
   */
  contentContainer?: EditorFieldContentContainerLayout
}

export function EditorField({
  className,
  toolbarContainer,
  contentContainer,
}: EditorFieldProps = {}) {
  const { project, history, effectiveFieldConfig: fieldConfig } = useEditor()
  const [blocksDragging, setBlocksDragging] = useState(false)
  const fieldRef = useRef<HTMLDivElement>(null)
  const wrapperRef = useRef<HTMLDivElement>(null)

  const resolvedToolbar = {
    className: toolbarContainer?.className ?? DEFAULT_TOOLBAR_CONTAINER_CLASS,
    innerClassName: toolbarContainer?.innerClassName ?? DEFAULT_TOOLBAR_INNER_CLASS,
    mergeWithContent: toolbarContainer?.mergeWithContent ?? true,
  } as const

  const resolvedContent = {
    className: contentContainer?.className ?? DEFAULT_CONTENT_CONTAINER_CLASS,
    scrollClassName: contentContainer?.scrollClassName ?? DEFAULT_CONTENT_SCROLL_CLASS,
    documentClassName: contentContainer?.documentClassName,
    blocksClassName: contentContainer?.blocksClassName,
    pageSettings: contentContainer?.pageSettings,
  } as const

  // Mode "single block document": ensures exactly one block of
  // the allowed type (or rich-text by default). Automatically creates if
  // the list is empty, so the user opens directly in the
  // editor without needing to go through the block picker.
  useEffect(() => {
    if (!fieldConfig.singleBlockMode) return
    if (history.isViewingHistory) return
    if (project.blocks.length > 0) return
    const type = fieldConfig.allowedBlockTypes?.[0] ?? "rich-text"
    const config = BLOCK_REGISTRY[type]
    if (!config) return
    project.setBlocks([config.createEmpty(nextBlockId(project.blocks))])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fieldConfig.singleBlockMode, project.blocks.length, history.isViewingHistory])

  const handleDragStateChange = useCallback((dragging: boolean) => {
    setBlocksDragging(dragging)
    if (dragging) {
      requestAnimationFrame(() => {
        wrapperRef.current?.scrollIntoView({ behavior: "auto", block: "nearest" })
      })
    }
  }, [])

  const hasRestrictedBlockTypes = !!fieldConfig.allowedBlockTypes && fieldConfig.allowedBlockTypes.length <= 1
  const isQuizMode = fieldConfig.projectType === "quiz"
  const hideBlocks = hasRestrictedBlockTypes || (isQuizMode && !fieldConfig.allowedBlockTypes?.length)

  // Document Mode interception: fully unlocked LexicalSurface without block chrome
  const documentBlock = fieldConfig.projectType === "document" && project.blocks.length === 1
    ? project.blocks[0]
    : undefined

  if (documentBlock?.type === "rich-text") {
    const block = documentBlock
    const data = block.data

    // mountKey ensures LexicalComposer re-mounts when a different project
    // is loaded (otherwise the old empty state stays).
    const documentMountKey = `${project.projectId ?? "new"}-${block.id}`
    const canMergeToolbarWithContent = !history.isViewingHistory && resolvedToolbar.mergeWithContent
    const initialPageSettings: PageSettings | undefined = resolvedContent.pageSettings

    return (
      <div
        className={cn(
          BASE_CONTAINER_CLASS,
          resolvedContent.className,
          className
        )}
      >
        <LexicalSurface
          namespace="DocumentEditor"
          mountKey={documentMountKey}
          initialState={data?.content ?? null}
          readOnly={history.isViewingHistory}
          onChange={(content) => {
            const next = [...project.blocks]
            next[0] = { ...block, data: { ...data, content } }
            project.setBlocks(next)
          }}
          placeholder="Start writing your document..."
          className={cn(
            "relative",
            resolvedContent.documentClassName ?? DEFAULT_DOC_SURFACE_CLASS,
            canMergeToolbarWithContent && "rounded-b-lg border-t-0",
          )}
          toolbarWrapper={(toolbar) => (
            <div className={cn(resolvedToolbar.className)}>
              <div
                className={cn(
                  resolvedToolbar.innerClassName,
                  canMergeToolbarWithContent &&
                    "border border-gray-200 dark:border-gray-800 border-b-0 rounded-t-lg bg-white dark:bg-gray-950 shadow-sm overflow-hidden",
                )}
              >
                {toolbar}
              </div>
            </div>
          )}
          initialPageSettings={initialPageSettings}
          // In paged mode, page geometry/padding is owned by `PagesPlugin`.
          // Keep this class neutral so we don't override fixed sheet sizing.
          contentClassName="max-w-none"
          contentScrollable
        />
      </div>
    )
  }

  return (
    <div
      className={cn(
        BASE_CONTAINER_CLASS,
        resolvedContent.className,
        className
      )}
    >
      <div
        ref={wrapperRef}
        className={cn("overflow-y-auto", resolvedContent.scrollClassName)}
        style={{ paddingBottom: `${BLOCKS_SCROLL_BOTTOM_INSET_PX}px` }}
      >
        <div
          ref={fieldRef}
          className={cn(resolvedContent.blocksClassName ?? DEFAULT_BLOCKS_SURFACE_CLASS)}
          style={
            blocksDragging
              ? {
                  width: DRAG_PREVIEW_WIDTH,
                  marginLeft: "auto",
                  marginRight: "auto",
                  transform: `scale(${DRAG_PREVIEW_SCALE})`,
                  transformOrigin: "top center",
                }
              : undefined
          }
        >
          <BlockArrayEditor
            blocks={project.blocks}
            onChange={project.setBlocks}
            readOnly={history.isViewingHistory}
            allowedBlockTypes={fieldConfig.allowedBlockTypes}
            defaultPickerTab={hideBlocks || isQuizMode ? "templates" : "blocks"}
            hideBlockTypesTab={hideBlocks}
            singleBlockMode={fieldConfig.singleBlockMode}
            onDragStateChange={handleDragStateChange}
          />
        </div>
      </div>
    </div>
  )
}
