"use client"

import { useEffect, useMemo, useRef, useState } from "react"
import { toast } from "sonner"
import { FileWarning, Loader2 } from "lucide-react"
import { EnhancedStorageAdapter } from "@/components/block-content-editor/lib/storage/editor/enhanced-storage-adapter"
import { deserializeProject } from "@/components/block-content-editor/lib/storage/editor/block-storage"
import { BlockArrayViewer } from "@/components/block-content-editor/engines/blocks/block-array-viewer"
import type { ProjectData } from "@/components/block-content-editor/extras/preview/preview-load-operations"

interface StaticViewerProps {
  projectId: string
  className?: string
  showTitle?: boolean
  showMeta?: boolean
}

export function StaticViewer({
  projectId,
  className,
  showTitle = true,
  showMeta = true,
}: StaticViewerProps) {
  const [project, setProject] = useState<ProjectData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const dbStorage = useRef<EnhancedStorageAdapter>(new EnhancedStorageAdapter())

  useEffect(() => {
    let cancelled = false
    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        await dbStorage.current.init()
        if (cancelled) return
        const data = await dbStorage.current.load(projectId)
        if (cancelled) return
        if (!data) setError(`Project "${projectId}" not found`)
        else setProject(data)
      } catch (err) {
        if (cancelled) return
        const msg = err instanceof Error ? err.message : "Unknown error"
        setError(msg)
        toast.error("Failed to load project", { description: msg })
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => { cancelled = true }
  }, [projectId])

  const blocks = useMemo(() => (project ? deserializeProject(project.data) : []), [project])

  if (loading) {
    return (
      <div className={`flex items-center justify-center py-20 ${className ?? ""}`}>
        <Loader2 className="h-8 w-8 animate-spin text-gray-400 dark:text-gray-500" />
      </div>
    )
  }

  if (error || !project) {
    return (
      <div className={`flex flex-col items-center justify-center py-20 text-center ${className ?? ""}`}>
        <FileWarning className="h-12 w-12 text-gray-300 dark:text-gray-600 mb-4" />
        <h3 className="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-1">Project Not Found</h3>
        <p className="text-sm text-gray-500 dark:text-gray-400">{error ?? "The project could not be loaded."}</p>
      </div>
    )
  }

  const meta = project
  const updatedAt = meta.metadata?.updatedAt ? new Date(meta.metadata.updatedAt) : null
  const tags: string[] = meta.tags ?? []

  return (
    <article className={className}>
      {(showTitle || showMeta) && (
        <header className="mb-6">
          {showTitle && (
            <h1 className="text-3xl font-bold text-gray-900 dark:text-gray-50 mb-2">
              {project.name}
            </h1>
          )}
          {showMeta && (
            <div className="flex flex-wrap items-center gap-3 text-sm text-gray-500 dark:text-gray-400">
              {updatedAt && (
                <time dateTime={updatedAt.toISOString()}>
                  {updatedAt.toLocaleDateString(undefined, { year: "numeric", month: "long", day: "numeric" })}
                </time>
              )}
              {tags.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {tags.map((tag) => (
                    <span
                      key={tag}
                      className="px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}
        </header>
      )}

      <div className="border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900 p-6">
        <BlockArrayViewer blocks={blocks} />
      </div>
    </article>
  )
}
