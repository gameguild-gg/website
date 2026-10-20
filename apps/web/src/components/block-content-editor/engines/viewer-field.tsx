"use client"

import { Button } from "@game-guild/ui/components/button"
import { Eye } from "lucide-react"
import { BlockArrayViewer } from "@/components/block-content-editor/engines/blocks/block-array-viewer"
import { useViewer } from "./viewer-provider"
import { LexicalSurface } from "@game-guild/lexical-surface"

export function ViewerField() {
  const { viewer, ui, fieldConfig } = useViewer()

  if (viewer.currentProject) {
    const documentBlock = fieldConfig.projectType === "document" && viewer.blocks.length === 1
      ? viewer.blocks[0]
      : undefined

    if (documentBlock?.type === "rich-text") {
      const block = documentBlock
      const data = block.data
      const viewerMountKey = `${viewer.currentProject.id ?? "view"}-${block.id}`

      return (
        <div className="border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950 shadow-sm rounded-lg flex flex-col min-h-full">
          <LexicalSurface
            namespace="DocumentViewer"
            mountKey={viewerMountKey}
            initialState={data?.content ?? null}
            readOnly={true}
            onChange={() => { }}
            className="flex-1 flex flex-col"
            // Keep neutral to preserve fixed page geometry from PagesPlugin.
            contentClassName="max-w-none"
          />
        </div>
      )
    }

    return (
      <div className="border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900 p-6">
        <BlockArrayViewer blocks={viewer.blocks} />
      </div>
    )
  }

  return (
    <div className="border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <div className="p-6 px-12 py-12">
        <div className="py-16 text-center">
          <Eye className="mx-auto mb-4 h-16 w-16 text-gray-300 dark:text-gray-600" />
          <h3 className="mb-2 text-xl font-semibold text-gray-900 dark:text-gray-100">
            No Project Selected
          </h3>
          <p className="mb-6 text-gray-500 dark:text-gray-400">Choose a project to view its content</p>
          <Button
            onClick={() => ui.setOpenDialogOpen(true)}
            disabled={!viewer.isDbInitialized}
            className="bg-blue-600 text-white hover:bg-blue-700 dark:bg-blue-600 dark:hover:bg-blue-700"
          >
            Open Project
          </Button>
        </div>
      </div>
    </div>
  )
}
