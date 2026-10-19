"use client"

import type React from "react"

import { Button } from "@game-guild/ui/components/button"
import { Input } from "@game-guild/ui/components/input"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@game-guild/ui/components/dialog"
import { Label } from "@game-guild/ui/components/label"
import { useState, useEffect, useRef } from "react"
import { toast } from "sonner"
import { Upload, FileText, FolderOpen, Archive, X } from "lucide-react"
import { ProjectImporter, type ImportedProjectData } from "@/components/block-content-editor/lib/interopAdapter/project-importer"
import type { ProjectData } from "@/components/block-content-editor/lib/storage/editor/project-data"
import type { StorageType } from "@/components/block-content-editor/lib/storage/editor/storage-types"
import type { ProjectPreferences } from "@/components/block-content-editor/lib/storage/editor/project-preferences"
import { findAssetUris } from "@game-guild/assets"
import { getDefaultBrowserAssetRepository } from "@game-guild/assets/browser"
import { getImportErrorMessage, readEntriesAsFiles } from "./import-project-files"

const assetRepository = getDefaultBrowserAssetRepository()

interface StorageAdapter {
  list: () => Promise<ProjectData[]>
  save: (id: string, name: string, data: string, tags: string[], storageType?: StorageType, preferences?: ProjectPreferences) => Promise<void>
}

interface ImportProjectDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  isDbInitialized: boolean
  storageAdapter: StorageAdapter
  availableTags: Array<{ name: string }>
  onProjectCreate: (projectData: { id: string; name: string; tags: string[] }) => void
  onProjectsListUpdate: () => void
  onAvailableTagsUpdate: () => void
  generateProjectId: () => string
  onOpenProject?: (projectData: { id: string; name: string; tags: string[] }) => void
}

export function ImportProjectDialog({
  open,
  onOpenChange,
  isDbInitialized,
  storageAdapter,
  availableTags,
  onProjectCreate,
  onProjectsListUpdate,
  onAvailableTagsUpdate,
  generateProjectId,
  onOpenProject,
}: ImportProjectDialogProps) {
  const [projectName, setProjectName] = useState("")
  const [projectTags, setProjectTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState("")
  const [showTagDropdown, setShowTagDropdown] = useState(false)
  const [importedProject, setImportedProject] = useState<ImportedProjectData | null>(null)
  const [isDragOver, setIsDragOver] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const folderInputRef = useRef<HTMLInputElement>(null)

  // Close tag dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (showTagDropdown) {
        const target = event.target as Element
        if (!target.closest(".relative")) {
          setShowTagDropdown(false)
        }
      }
    }

    document.addEventListener("mousedown", handleClickOutside)
    return () => document.removeEventListener("mousedown", handleClickOutside)
  }, [showTagDropdown])

  const resetForm = () => {
    setProjectName("")
    setProjectTags([])
    setTagInput("")
    setShowTagDropdown(false)
    setImportedProject(null)
    setIsDragOver(false)
  }

  const applyImportedData = (importedData: ImportedProjectData, sourceLabel: string) => {
    if (!ProjectImporter.validateImportedData(importedData)) {
      throw new Error('Invalid project data format')
    }
    setImportedProject(importedData)
    setProjectName(importedData.name)
    setProjectTags(importedData.tags)
    toast.success("Imported successfully", {
      description: `Loaded project: ${importedData.name} (from ${sourceLabel})`,
      duration: 3000,
      icon: "📁",
    })
  }

  const handleFileUpload = async (file: File) => {
    try {
      if (!ProjectImporter.isSupportedFile(file.name)) {
        const supportedExtensions = ProjectImporter.getSupportedExtensions()
        throw new Error(`Unsupported file format. Supported formats: ${supportedExtensions.join(', ')}`)
      }
      const importedData = await ProjectImporter.importFromFile(file)
      applyImportedData(importedData, file.name)
    } catch (error) {
      console.error("Import error:", error)
      toast.error("Import failed", {
        description: getImportErrorMessage(error, "Failed to import project file"),
        duration: 4000,
        icon: "❌",
      })
    }
  }

  const handleFolderUpload = async (files: File[]) => {
    try {
      const importedData = await ProjectImporter.importFromFolder(files)
      const folderLabel = files[0]?.webkitRelativePath?.split('/')[0] || 'folder'
      applyImportedData(importedData, folderLabel)
    } catch (error) {
      console.error("Folder import error:", error)
      toast.error("Import failed", {
        description: getImportErrorMessage(error, "Failed to import project folder"),
        duration: 4000,
        icon: "❌",
      })
    }
  }

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)

    const items = e.dataTransfer.items
    if (items && items.length > 0) {
      // Detect directory drops
      const entries: FileSystemEntry[] = []
      for (let i = 0; i < items.length; i++) {
        const item = items[i]
        const entry = item && item.webkitGetAsEntry ? item.webkitGetAsEntry() : null
        if (entry) entries.push(entry)
      }
      const dirEntry = entries.find((entry) => entry?.isDirectory)
      if (dirEntry) {
        const collected = await readEntriesAsFiles(dirEntry)
        if (collected.length > 0) {
          await handleFolderUpload(collected)
          return
        }
      }
    }

    const files = Array.from(e.dataTransfer.files)
    if (files.length > 0) {
      handleFileUpload(files[0] as File)
    }
  }

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(true)
  }

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
  }

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files && files.length > 0) {
      handleFileUpload(files[0] as File)
    }
    // Reset so the same file can be re-selected later
    e.target.value = ""
  }

  const handleFolderSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files && files.length > 0) {
      handleFolderUpload(Array.from(files))
    }
    e.target.value = ""
  }

  const handleSave = async (openAfterSave = false) => {
    if (!importedProject) {
      toast.error("No project to import", {
        description: "Please upload a project file first",
        duration: 3000,
        icon: "📁",
      })
      return
    }

    if (!projectName.trim()) {
      toast.error("Project name required", {
        description: "Please enter a name for the project",
        duration: 3000,
        icon: "✏️",
      })
      return
    }

    if (projectTags.length === 0) {
      toast.error("Tags required", {
        description: "Please add at least one tag to the project",
        duration: 3000,
        icon: "🏷️",
      })
      return
    }



    // Check if project with same name already exists
    const existingProjects = await storageAdapter.list()
    if (existingProjects.some((p) => p.name === projectName.trim())) {
      let suggestedName = `${projectName.trim()}-imported`
      let counter = 1

      while (existingProjects.some((p) => p.name === suggestedName)) {
        counter++
        suggestedName = `${projectName.trim()}-imported-${counter}`
      }

      toast.error("Name already exists", {
        description: `Project "${projectName.trim()}" already exists. Suggestion: ${suggestedName}`,
        duration: 5000,
        icon: "🚫",
      })
      return
    }

    try {
      const newProjectId = generateProjectId()
      
      // Save project with preferences
      await storageAdapter.save(
        newProjectId, 
        projectName.trim(), 
        importedProject.data, 
        projectTags,
        undefined, // storageType
        importedProject.preferences || importedProject.metadata?.preferences
      )

      const importedUris = findAssetUris(JSON.parse(importedProject.data) as unknown)
      await assetRepository.reconcileUsage(
        { type: "project", id: newProjectId },
        importedUris.map((uri, index) => ({
          uri,
          consumerId: `imported-reference:${index}`,
          role: "project-content",
        })),
      )

      if (importedProject.assetsImported > 0) {
        toast.info("Assets imported", {
          description: `${importedProject.assetsImported} assets restored from the project bundle`,
          duration: 3000,
          icon: "📦",
        })
      }

      const projectData = {
        id: newProjectId,
        name: projectName.trim(),
        tags: projectTags,
      }

      onProjectCreate(projectData)

      // Reset form
      resetForm()
      onOpenChange(false)

      // Update lists
      await onProjectsListUpdate()
      await onAvailableTagsUpdate()

      if (openAfterSave && onOpenProject) {
        onOpenProject(projectData)
      }

      toast.success("Project imported successfully", {
        description: `"${projectName.trim()}" has been ${openAfterSave ? "imported and opened" : "imported"}`,
        duration: 3000,
        icon: "🎉",
      })
    } catch (error) {
      console.error("Save error:", error)
      toast.error("Failed to save project", {
        description: "Could not save the imported project. Please try again.",
        duration: 4000,
        icon: "❌",
      })
    }
  }

  const handleCancel = () => {
    if (importedProject?.importedAssetUris.length) {
      void Promise.all(
        importedProject.importedAssetUris.map((uri) =>
          assetRepository.remove(uri, { force: true }).catch(() => undefined),
        ),
      )
    }
    resetForm()
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => nextOpen ? onOpenChange(true) : handleCancel()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Import Project</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          {/* File Upload Area */}
          {!importedProject && (
            <div
              className={`border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
                isDragOver
                  ? "border-blue-500 bg-blue-50 dark:bg-blue-950"
                  : "border-gray-300 dark:border-gray-600 hover:border-gray-400 dark:hover:border-gray-500"
              }`}
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
            >
              <Upload className="w-8 h-8 mx-auto mb-2 text-gray-400" />
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
                Drag and drop your project file or folder here, or click to browse
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-500 mb-4">
                Supports: .zip, .block-content-editor file, or an uncompressed projeto-* folder
              </p>
              <div className="flex justify-center gap-2">
                <Button variant="outline" onClick={() => fileInputRef.current?.click()}>
                  <FileText className="w-4 h-4 mr-2" />
                  Choose File
                </Button>
                <Button variant="outline" onClick={() => folderInputRef.current?.click()}>
                  <FolderOpen className="w-4 h-4 mr-2" />
                  Choose Folder
                </Button>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".zip,.block-content-editor"
                onChange={handleFileSelect}
                className="hidden"
              />
              <input
                ref={folderInputRef}
                type="file"
                onChange={handleFolderSelect}
                className="hidden"
                // @ts-expect-error non-standard attributes for directory picker
                webkitdirectory=""
                directory=""
                multiple
              />
            </div>
          )}

          {/* Project Details Form */}
          {importedProject && (
            <>
              {/* File Info */}
              <div className="p-3 bg-green-50 dark:bg-green-950 rounded-lg">
                <div className="flex items-center gap-2 mb-2">
                  <Archive className="w-4 h-4 text-green-600 dark:text-green-400" />
                  <span className="text-sm font-medium text-green-800 dark:text-green-200">
                    File Imported Successfully
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setImportedProject(null)}
                    className="ml-auto h-6 w-6 p-0"
                  >
                    <X className="w-3 h-3" />
                  </Button>
                </div>
                <p className="text-xs text-green-700 dark:text-green-300">
                  Project data loaded. You can modify the name and tags below.
                </p>
              </div>

              {/* Project Name */}
              <div>
                <Label htmlFor="import-project-name">Project Name</Label>
                <Input
                  id="import-project-name"
                  value={projectName}
                  onChange={(e) => setProjectName(e.target.value)}
                  placeholder="Enter project name..."
                  className="mt-1"
                />
              </div>

              {/* Tags Section - Same as create dialog */}
              <div className="space-y-3">
                <Label>Tags (required) *</Label>

                <div className="relative">
                  <div className="flex gap-2">
                    <div className="relative flex-1">
                      <Input
                        placeholder="Search existing tags or type to create new..."
                        value={tagInput}
                        onChange={(e) => {
                          setTagInput(e.target.value)
                          setShowTagDropdown(true)
                        }}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" && tagInput.trim()) {
                            e.preventDefault()
                            const newTag = tagInput.trim()
                            if (!projectTags.includes(newTag)) {
                              setProjectTags((prev) => [...prev, newTag])
                            }
                            setTagInput("")
                            setShowTagDropdown(false)
                          }
                          if (e.key === "Escape") {
                            setShowTagDropdown(false)
                          }
                        }}
                        onFocus={() => setShowTagDropdown(true)}
                        className="pr-10"
                      />
                      <button
                        type="button"
                        onClick={() => setShowTagDropdown(!showTagDropdown)}
                        className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                      >
                        <svg
                          className={`w-4 h-4 transition-transform ${showTagDropdown ? "rotate-180" : ""}`}
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                        </svg>
                      </button>
                    </div>
                  </div>

                  {/* Tag Dropdown - Same logic as create dialog */}
                  {showTagDropdown && (
                    <div className="absolute z-10 w-full mt-1 bg-white dark:bg-gray-800 border dark:border-gray-700 rounded-md shadow-lg max-h-48 overflow-y-auto">
                      {(() => {
                        const filteredTags = tagInput.trim()
                          ? availableTags.filter(
                              (tag) =>
                                tag.name.toLowerCase().includes(tagInput.toLowerCase()) &&
                                !projectTags.includes(tag.name),
                            )
                          : availableTags.filter((tag) => !projectTags.includes(tag.name))

                        return (
                          <>
                            {filteredTags.length > 0 && (
                              <>
                                {filteredTags.slice(0, 10).map((tag) => (
                                  <button
                                    key={tag.name}
                                    type="button"
                                    onClick={() => {
                                      setProjectTags((prev) => [...prev, tag.name])
                                      setTagInput("")
                                      setShowTagDropdown(false)
                                    }}
                                    className="w-full px-3 py-2 text-left hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center justify-between"
                                  >
                                    <span className="text-sm">{tag.name}</span>
                                  </button>
                                ))}
                                {tagInput.trim() &&
                                  !availableTags.some((tag) => tag.name.toLowerCase() === tagInput.toLowerCase()) &&
                                  !projectTags.includes(tagInput.trim()) && (
                                    <div className="border-t dark:border-gray-700 my-1"></div>
                                  )}
                              </>
                            )}

                            {tagInput.trim() &&
                              !availableTags.some((tag) => tag.name.toLowerCase() === tagInput.toLowerCase()) &&
                              !projectTags.includes(tagInput.trim()) && (
                                <button
                                  type="button"
                                  onClick={() => {
                                    const newTag = tagInput.trim()
                                    setProjectTags((prev) => [...prev, newTag])
                                    setTagInput("")
                                    setShowTagDropdown(false)
                                  }}
                                  className="w-full px-3 py-2 text-left hover:bg-gray-100 dark:hover:bg-gray-700"
                                >
                                  <div className="flex items-center gap-2">
                                    <svg
                                      className="w-4 h-4 text-green-600 dark:text-green-400"
                                      fill="none"
                                      stroke="currentColor"
                                      viewBox="0 0 24 24"
                                    >
                                      <path
                                        strokeLinecap="round"
                                        strokeLinejoin="round"
                                        strokeWidth={2}
                                        d="M12 4v16m8-8H4"
                                      />
                                    </svg>
                                    <span className="text-sm">
                                      Create "<strong>{tagInput.trim()}</strong>"
                                    </span>
                                  </div>
                                </button>
                              )}
                          </>
                        )
                      })()}
                    </div>
                  )}
                </div>

                {/* Selected Tags */}
                {projectTags.length > 0 && (
                  <div className="space-y-2">
                    <Label className="text-sm text-gray-600 dark:text-gray-400">Selected tags:</Label>
                    <div className="flex flex-wrap gap-2">
                      {projectTags.map((tag, index) => (
                        <span
                          key={index}
                          className="inline-flex items-center gap-1 px-3 py-1 bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200 text-sm rounded-full"
                        >
                          {tag}
                          <button
                            type="button"
                            onClick={() => setProjectTags((prev) => prev.filter((_, i) => i !== index))}
                            className="hover:text-blue-600 dark:hover:text-blue-300 ml-1"
                          >
                            <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                strokeWidth={2}
                                d="M6 18L18 6M6 6l12 12"
                              />
                            </svg>
                          </button>
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </>
          )}

          {/* Action Buttons */}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={handleCancel}>
              Cancel
            </Button>
            {importedProject && (
              <>
                <Button
                  variant="outline"
                  onClick={() => handleSave(false)}
                  disabled={!projectName.trim() || projectTags.length === 0}
                >
                  Save and Exit
                </Button>
                <Button onClick={() => handleSave(true)} disabled={!projectName.trim() || projectTags.length === 0}>
                  Save and Open
                </Button>
              </>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
