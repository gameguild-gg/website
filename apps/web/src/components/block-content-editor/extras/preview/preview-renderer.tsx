"use client"
import type { SerializedEditorState } from "lexical"
import type { ProjectData } from "../../lib/storage/editor/project-data"

import { SerializedContentRenderer } from "./serialized-content-renderer"
import { PreviewHeader } from "./preview-header"

interface PreviewRendererProps {
  serializedState: SerializedEditorState
  showHeader?: boolean
  projectName?: string
  className?: string
  projectId?: string
  storageAdapter?: {
    load: (id: string) => Promise<ProjectData | null>
  }
}

export function PreviewRenderer({
  serializedState,
  showHeader = false,
  projectName,
  className = "prose prose-stone dark:prose-invert max-w-none",
  projectId,
  storageAdapter,
}: PreviewRendererProps) {
  return (
    <div className={className}>
      {showHeader && projectName && <PreviewHeader projectName={projectName} />}
      <SerializedContentRenderer serializedState={serializedState} projectId={projectId} storageAdapter={storageAdapter} />
    </div>
  )
}
