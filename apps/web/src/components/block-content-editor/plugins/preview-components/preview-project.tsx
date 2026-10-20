"use client"

import { useState, useEffect } from "react"
import { FileText, Copy, ExternalLink, RefreshCw } from "lucide-react"
import { Button } from "@game-guild/ui/components/button"
import { SerializedContentRenderer } from "../../extras/preview/serialized-content-renderer"
import { toast } from "sonner"
import type { SerializedEditorState } from "lexical"
import type { ProjectData as StoredProjectData } from "../../lib/storage/editor/project-data"

interface ProjectNodeData {
  projectId: string
  projectName: string
  editorState: any
  isLocalCopy: boolean
  isReference?: boolean
  wasReference?: boolean
  originalProjectId?: string
  size?: number
  caption?: string
}

interface PreviewProjectProps {
  node: {
    data: ProjectNodeData
  }
  storageAdapter?: {
    load: (id: string) => Promise<StoredProjectData | null>
  }
}

export function PreviewProject({ node, storageAdapter }: PreviewProjectProps) {
  const [loadedData, setLoadedData] = useState<ProjectNodeData | null>(
    node.data.isReference ? null : node.data
  )
  const [isLoading, setIsLoading] = useState(node.data.isReference ? true : false)

  // Load project data when it's a reference
  useEffect(() => {
    if (node.data.isReference && storageAdapter && !loadedData) {
      const loadReferenceData = async () => {
        try {
          setIsLoading(true)
          const fullProject = await storageAdapter.load(node.data.projectId)
          
          if (!fullProject) {
            console.error("Referenced project not found")
            setIsLoading(false)
            return
          }

          // Parse project data
          let editorState = null

          try {
            editorState = JSON.parse(fullProject.data)
          } catch (error) {
            console.error("Failed to parse project data:", error)
          }

          const loadedProjectData: ProjectNodeData = {
            ...node.data,
            editorState,
            size: fullProject.metadata.size,
          }

          setLoadedData(loadedProjectData)
          setIsLoading(false)
        } catch (error) {
          console.error("Error loading reference project:", error)
          setIsLoading(false)
        }
      }

      loadReferenceData()
    }
  }, [node.data.isReference, node.data.projectId, storageAdapter, loadedData])

  const formatSize = (sizeInKB?: number): string => {
    if (!sizeInKB) return "Unknown size"
    if (sizeInKB < 1024) {
      return `${sizeInKB.toFixed(1)}KB`
    } else {
      return `${(sizeInKB / 1024).toFixed(1)}MB`
    }
  }

  const handleOpenInNewTab = () => {
    const targetId = node.data.originalProjectId || node.data.projectId
    const url = `/block-content-editor/studio#${targetId}`
    window.open(url, '_blank')
  }

  return (
    <div className="my-4 w-full border-l-4 border-blue-500/30 hover:border-blue-500/50 transition-colors">
      {/* Header with project info */}
      <div className="flex items-center justify-between bg-gray-100 dark:bg-gray-800 px-4 py-2 mb-2">
        <div className="flex items-center gap-3 flex-1 min-w-0">
          <FileText className="h-5 w-5 text-gray-600 dark:text-gray-400 shrink-0" />
          <div className="flex flex-col min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-semibold truncate text-gray-900 dark:text-gray-100">
                {node.data.projectName}
              </span>
              {node.data.isLocalCopy && (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 text-xs font-medium bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded">
                  <Copy className="h-3 w-3" />
                  Local Copy
                </span>
              )}
              {node.data.isReference && (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 text-xs font-medium bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded">
                  <FileText className="h-3 w-3" />
                  Referenced
                </span>
              )}
            </div>
            <div className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
              <span className="capitalize">Single Project</span>
              <span>•</span>
              <span>{formatSize(loadedData?.size ?? node.data.size)}</span>
              {node.data.isLocalCopy && node.data.originalProjectId && (
                <>
                  <span>•</span>
                  <span className="font-mono">From: {node.data.originalProjectId.slice(0, 8)}</span>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Action button */}
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            onClick={handleOpenInNewTab}
            title="Open in new tab"
            className="h-8 w-8 p-0"
          >
            <ExternalLink className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Caption */}
      {node.data.caption && (
        <div className="px-4 py-2 text-sm text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/50 mb-2">
          {node.data.caption}
        </div>
      )}

      {/* Content - Read-only preview */}
      <div className="w-full">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 px-4">
            <div className="text-center text-gray-500 dark:text-gray-400">
              <FileText className="h-12 w-12 mx-auto mb-2 opacity-50 animate-pulse" />
              <p className="text-sm">Loading project...</p>
            </div>
          </div>
        ) : !loadedData ? (
          <div className="flex items-center justify-center py-12 px-4">
            <div className="text-center text-gray-500 dark:text-gray-400">
              <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
              <p className="text-sm">Failed to load project</p>
            </div>
          </div>
        ) : loadedData.editorState ? (
          <div className="w-full">
            <SerializedContentRenderer serializedState={loadedData.editorState} />
          </div>
        ) : (
          <div className="flex items-center justify-center py-12 px-4">
            <div className="text-center text-gray-500 dark:text-gray-400">
              <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
              <p className="text-sm">No content available</p>
              <p className="text-xs mt-1">Single Layout</p>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
