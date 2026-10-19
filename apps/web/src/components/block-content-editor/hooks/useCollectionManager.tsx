"use client"

import { useState, useEffect, useMemo, useCallback } from 'react'
import { Edit, Trash } from "lucide-react"
import { collectionRepository } from "@/components/block-content-editor/extras/code-studio/file-system/collection-repository"
import type { CollectionFolder, CollectionStructure } from "@/components/block-content-editor/extras/code-studio/file-system/collection-types"
import {
  applySorting,
  type ManagerCard,
  type CardAction,
  type FilterConfig,
} from "@/components/block-content-editor/extras/manager-page"
import { toast } from "sonner"
import { getDefaultBrowserAssetRepository } from "@game-guild/assets/browser"

const assetRepository = getDefaultBrowserAssetRepository()

interface CollectionItem {
  id: string
  name: string
  description?: string
  tags?: string[]
  fileCount: number
  totalSize: number
  created: string
  updated: string
}

interface UseCollectionManagerProps {
  isDbInitialized: boolean
  activeContext: 'projects' | 'assets' | 'collections'
  filters: FilterConfig
  currentPage: number
  itemsPerPage: number
}

export interface UseCollectionManagerReturn {
  collectionCards: ManagerCard[]
  collectionPrimaryActions: CardAction[]
  collectionSecondaryActions: CardAction[]
  filteredCount: number
  collectionToDelete: { id: string; name: string } | null
  setCollectionToDelete: (collection: { id: string; name: string } | null) => void
  collectionToEdit: { id: string; name: string } | null
  setCollectionToEdit: (collection: { id: string; name: string } | null) => void
  newCollectionName: string
  setNewCollectionName: (name: string) => void
  handleConfirmCollectionDelete: () => Promise<void>
  handleConfirmCollectionEdit: () => Promise<void>
}

// Helper functions for collection details
function countFilesInStructure(structure: CollectionStructure | CollectionFolder): number {
  let count = 0
  if (structure.files) count += structure.files.length
  if (structure.folders) {
    structure.folders.forEach((folder) => {
      count += countFilesInStructure(folder)
    })
  }
  return count
}

function calculateTotalSize(structure: CollectionStructure | CollectionFolder): number {
  let size = 0
  if (structure.files) {
    structure.files.forEach((file) => {
      size += file.size ?? 0
    })
  }
  if (structure.folders) {
    structure.folders.forEach((folder) => {
      size += calculateTotalSize(folder)
    })
  }
  return size
}

export function useCollectionManager({
  isDbInitialized,
  activeContext,
  filters,
  currentPage,
  itemsPerPage,
}: UseCollectionManagerProps): UseCollectionManagerReturn {
  const [collections, setCollections] = useState<CollectionItem[]>([])
  const [collectionToDelete, setCollectionToDelete] = useState<{ id: string; name: string } | null>(null)
  const [collectionToEdit, setCollectionToEdit] = useState<{ id: string; name: string } | null>(null)
  const [newCollectionName, setNewCollectionName] = useState("")

  const loadCollections = useCallback(async () => {
    try {
      const collectionList = await collectionRepository.list()

      const collectionsWithDetails = await Promise.all(
        collectionList.map(async (collection) => {
          try {
            const manifest = await collectionRepository.get(collection.id)
            if (manifest) {
              const fileCount = countFilesInStructure(manifest.structure)
              const totalSize = calculateTotalSize(manifest.structure)
              return {
                id: collection.id,
                name: collection.name,
                description: collection.description,
                tags: collection.tags,
                fileCount,
                totalSize,
                created: new Date(collection.created).toISOString(),
                updated: new Date(collection.updated).toISOString(),
              }
            }
          } catch (error) {
            console.error(`Failed to load manifest for collection ${collection.id}:`, error)
          }

          return {
            id: collection.id,
            name: collection.name,
            description: collection.description,
            tags: collection.tags,
            fileCount: 0,
            totalSize: 0,
            created: new Date(collection.created).toISOString(),
            updated: new Date(collection.updated).toISOString(),
          }
        })
      )

      setCollections(collectionsWithDetails)
    } catch (error) {
      console.error("Failed to load collections:", error)
      toast.error("Failed to load collections", {
        description: error instanceof Error ? error.message : "Unknown error"
      })
    }
  }, [])

  // Load collections when switching to collections context
  useEffect(() => {
    if (activeContext === 'collections' && isDbInitialized) {
      loadCollections()
    }
  }, [activeContext, isDbInitialized, loadCollections])

  // Filter and sort collections
  const filteredCollections = useMemo(() => {
    const filtered = collections.filter((collection) => {
      const matchesSearch = !filters.searchTerm ||
        collection.name.toLowerCase().includes(filters.searchTerm.toLowerCase()) ||
        (collection.description && collection.description.toLowerCase().includes(filters.searchTerm.toLowerCase()))

      const matchesTags = !filters.tags || filters.tags.length === 0 || (
        collection.tags && (
          filters.tagFilterMode === 'all'
            ? filters.tags.every(tag => collection.tags?.includes(tag))
            : filters.tags.some(tag => collection.tags?.includes(tag))
        )
      )

      return matchesSearch && matchesTags
    })

    return applySorting(filtered, filters.sortOrder || [], 'updated')
  }, [collections, filters])

  // CRUD handlers
  const handleCollectionDelete = useCallback((collectionId: string, collectionName: string) => {
    setCollectionToDelete({ id: collectionId, name: collectionName })
  }, [])

  const handleCollectionEdit = useCallback((collectionId: string, currentName: string) => {
    setCollectionToEdit({ id: collectionId, name: currentName })
    setNewCollectionName(currentName)
  }, [])

  const handleConfirmCollectionDelete = useCallback(async () => {
    if (!collectionToDelete) return
    try {
      await collectionRepository.remove(collectionToDelete.id)
      await assetRepository.reconcileUsage(
        { type: "code-studio-collection", id: collectionToDelete.id },
        [],
      )
      toast.success("Collection deleted successfully")
      await loadCollections()
      setCollectionToDelete(null)
    } catch (error) {
      console.error("Failed to delete collection:", error)
      toast.error("Failed to delete collection")
    }
  }, [collectionToDelete, loadCollections])

  const handleConfirmCollectionEdit = useCallback(async () => {
    if (!collectionToEdit || !newCollectionName.trim()) return
    try {
      const success = await collectionRepository.rename(collectionToEdit.id, newCollectionName.trim())
      if (success) {
        toast.success("Collection renamed successfully")
        await loadCollections()
        setCollectionToEdit(null)
        setNewCollectionName("")
      } else {
        toast.error("Failed to rename collection")
      }
    } catch (error) {
      console.error("Failed to rename collection:", error)
      toast.error("Failed to rename collection")
    }
  }, [collectionToEdit, newCollectionName, loadCollections])

  // Convert to ManagerCard format
  const collectionCards: ManagerCard[] = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage
    const endIndex = startIndex + itemsPerPage
    return filteredCollections.slice(startIndex, endIndex).map(collection => ({
      type: 'collection' as const,
      id: collection.id,
      name: collection.name,
      description: collection.description,
      tags: collection.tags,
      fileCount: collection.fileCount,
      totalSize: collection.totalSize,
      createdAt: new Date(collection.created).toISOString(),
      updatedAt: new Date(collection.updated).toISOString(),
    }))
  }, [filteredCollections, currentPage, itemsPerPage])

  // Card actions
  const collectionPrimaryActions: CardAction[] = useMemo(() => [
    {
      label: 'Rename',
      icon: <Edit className="h-4 w-4" />,
      onClick: (card) => handleCollectionEdit(card.id, card.name),
    },
  ], [handleCollectionEdit])

  const collectionSecondaryActions: CardAction[] = useMemo(() => [
    {
      label: 'Delete',
      icon: <Trash className="h-4 w-4" />,
      onClick: (card) => handleCollectionDelete(card.id, card.name),
      variant: 'destructive' as const,
    },
  ], [handleCollectionDelete])

  return {
    collectionCards,
    collectionPrimaryActions,
    collectionSecondaryActions,
    filteredCount: filteredCollections.length,
    collectionToDelete,
    setCollectionToDelete,
    collectionToEdit,
    setCollectionToEdit,
    newCollectionName,
    setNewCollectionName,
    handleConfirmCollectionDelete,
    handleConfirmCollectionEdit,
  }
}
