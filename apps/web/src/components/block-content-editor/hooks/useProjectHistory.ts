"use client"

import { useCallback, useState } from "react"
import { toast } from "sonner"
import { deserializeProject, serializeProject } from "@/components/block-content-editor/lib/storage/editor/block-storage"
import type { UseProjectStorageReturn } from "./useProjectStorage"

export interface UseProjectHistoryReturn {
  isViewingHistory: boolean
  currentViewingSha: string | null
  loadCommit(sha: string): Promise<void>
  loadSnapshot(tag: string): Promise<void>
  returnToHead(): Promise<void>
}

export function useProjectHistory(project: UseProjectStorageReturn): UseProjectHistoryReturn {
  const [isViewingHistory, setIsViewingHistory] = useState(false)
  const [currentViewingSha, setCurrentViewingSha] = useState<string | null>(null)
  const [headProjectData, setHeadProjectData] = useState<string | null>(null)

  // ── Load commit ──

  const loadCommit = useCallback(async (sha: string) => {
    if (!project.projectId) return

    try {
      const history = await project.db.listHistory(project.projectId)
      const isHead = history.length > 0 && history[0]?.sha === sha

      // If loading HEAD, return to normal editing mode
      if (isHead) {
        if (isViewingHistory && headProjectData) {
          project.setBlocks(deserializeProject(headProjectData))
        }
        setIsViewingHistory(false)
        setCurrentViewingSha(null)
        setHeadProjectData(null)
        toast.success("Viewing latest version", {
          description: "You can edit the project",
          duration: 2000,
          icon: "✏️",
        })
        return
      }

      // Preserve HEAD data before switching to history
      if (!isViewingHistory) {
        setHeadProjectData(serializeProject(project.blocks))
      }

      // Load the commit
      const commitData = await project.db.loadFromHistory(project.projectId, sha)
      if (!commitData) {
        toast.error("Failed to load commit", {
          description: "The historical version could not be found",
          duration: 3000,
        })
        return
      }

      if (!commitData.data) {
        toast.error("Invalid historical data", {
          description: "This commit contains incomplete data. It may be from an older version.",
          duration: 4000,
        })
        return
      }

      project.setBlocks(deserializeProject(commitData.data))

      setIsViewingHistory(true)
      setCurrentViewingSha(sha)

      toast.info("Viewing historical version", {
        description: "This is read-only. Return to latest to edit.",
        duration: 3000,
        icon: "📜",
      })
    } catch (error) {
      console.error("Failed to load commit:", error)
      toast.error("Failed to load historical version", {
        description: error instanceof Error ? error.message : "Unknown error",
        duration: 4000,
      })
    }
  }, [project, isViewingHistory, headProjectData])

  // ── Load snapshot ──

  const loadSnapshot = useCallback(async (tag: string) => {
    if (!project.projectId) return

    if (!isViewingHistory) {
      setHeadProjectData(serializeProject(project.blocks))
    }

    const snapshots = await project.db.listSnapshots(project.projectId)
    const snapshot = snapshots.find((s) => s.tag === tag)
    if (snapshot) await loadCommit(snapshot.sha)
  }, [project, isViewingHistory, loadCommit])

  // ── Return to HEAD ──

  const returnToHead = useCallback(async () => {
    if (!project.projectId || !headProjectData) return

    project.setBlocks(deserializeProject(headProjectData))
    setIsViewingHistory(false)
    setCurrentViewingSha(null)
    setHeadProjectData(null)

    toast.success("Returned to latest version", {
      description: "You can now edit the project",
      duration: 2000,
      icon: "✏️",
    })
  }, [project, headProjectData])

  return {
    isViewingHistory,
    currentViewingSha,
    loadCommit,
    loadSnapshot,
    returnToHead,
  }
}
