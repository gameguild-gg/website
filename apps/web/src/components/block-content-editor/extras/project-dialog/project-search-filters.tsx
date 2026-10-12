"use client"

import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Button } from "@game-guild/ui/components/button"
import { useState, useEffect } from "react"
import { Cloud, Database, HardDrive, Calendar, User, AlertCircle } from "lucide-react"
import { useGoogleDriveAuth } from "@/components/block-content-editor/hooks/editor/use-google-drive-auth"
import type { StorageType } from "@/components/block-content-editor/lib/storage/editor/storage-types"

interface ProjectSearchFiltersProps {
  searchTerm: string
  onSearchChange: (value: string) => void
  selectedTags: string[]
  onTagsChange: (tags: string[]) => void
  availableTags: Array<{ name: string }>
  tagFilterMode: "all" | "any"
  onTagFilterModeChange: (mode: "all" | "any") => void
  storageTypeFilter?: StorageType
  onStorageTypeFilterChange?: (type: StorageType | undefined) => void
  itemsPerPage: number
  onItemsPerPageChange: (value: number) => void
  showFilters?: boolean
  forceVerticalLayout?: boolean
  // Advanced filters props
  authorFilter?: string
  onAuthorFilterChange?: (value: string) => void
  statusFilter?: "all" | "draft" | "published" | "scheduled"
  onStatusFilterChange?: (value: "all" | "draft" | "published" | "scheduled") => void
  dateFromFilter?: string
  onDateFromFilterChange?: (value: string) => void
  dateToFilter?: string
  onDateToFilterChange?: (value: string) => void
  accessFilter?: "all" | "all-access" | "all-authors" 
  onAccessFilterChange?: (value: "all" | "all-access" | "all-authors") => void
  sortOrder?: "newest" | "oldest" | "name" | "name-desc"
  onSortOrderChange?: (value: "newest" | "oldest" | "name" | "name-desc") => void
  showAdvancedFilters?: boolean
}

export function ProjectSearchFilters({
  searchTerm,
  onSearchChange,
  selectedTags,
  onTagsChange,
  availableTags,
  tagFilterMode,
  onTagFilterModeChange,
  storageTypeFilter,
  onStorageTypeFilterChange,
  itemsPerPage,
  onItemsPerPageChange,
  showFilters = false,
  forceVerticalLayout = false,
  // Advanced filters
  authorFilter = "",
  onAuthorFilterChange,
  statusFilter = "all",
  onStatusFilterChange,
  dateFromFilter = "",
  onDateFromFilterChange,
  dateToFilter = "",
  onDateToFilterChange,
  accessFilter = "all",
  onAccessFilterChange,
  sortOrder = "newest",
  onSortOrderChange,
  showAdvancedFilters = false,
}: ProjectSearchFiltersProps) {
  const [tagSearchInput, setTagSearchInput] = useState("")
  const [showTagDropdown, setShowTagDropdown] = useState(false)

  // Google Drive authentication hook
  const { isAuthenticated: isGoogleDriveAuthenticated } = useGoogleDriveAuth()

  // Close tag dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (showTagDropdown) {
        const target = event.target as Element
        if (!target.closest(".tag-filter-container")) {
          setShowTagDropdown(false)
        }
      }
    }

    document.addEventListener("mousedown", handleClickOutside)
    return () => document.removeEventListener("mousedown", handleClickOutside)
  }, [showTagDropdown])

  if (!showFilters) {
    return null
  }

  return (
    <div className="space-y-3 rounded-xl border border-border/40 bg-muted/30 p-4">
      
      { (
        <>
          <div className={`flex flex-col gap-4 pt-2 ${!forceVerticalLayout && "md:flex-row md:gap-6"}`}>
            {/* Project Search Section */}
            <div className="flex-1 space-y-3">
              <div className="flex items-center justify-between">
                <Label className="text-sm font-medium">Filter by Projects:</Label>
                <div className="flex items-center gap-2">
                  <Label className="text-xs text-gray-700 dark:text-gray-400">Items per page:</Label>
                  <select
                    value={itemsPerPage}
                    onChange={(e) => onItemsPerPageChange(Number(e.target.value))}
                    className="rounded border bg-background px-2 py-1 text-sm"
                  >
                    <option value={8}>8</option>
                    <option value={16}>16</option>
                    <option value={32}>32</option>
                    <option value={64}>64</option>
                    <option value={128}>128</option>
                  </select>
                </div>
              </div>
              <Input
                placeholder="Search projects by name..."
                value={searchTerm}
                onChange={(e) => { onSearchChange(e.target.value); }}
                className="w-full"
              />
            </div>

            <div className="hidden h-auto w-px bg-gray-200 dark:bg-gray-700 md:block" />

            {/* Tags Filter Section */}
            <div className="tag-filter-container flex-1 space-y-2">
              <div className="flex items-center justify-between">
                <Label className="text-sm font-medium">Filter by tags:</Label>
                <div className="flex items-center gap-2">
                  <Label className="text-xs text-gray-700 dark:text-gray-400">Match tags:</Label>
                  <select
                    value={tagFilterMode}
                    onChange={(e) => onTagFilterModeChange(e.target.value as "all" | "any")}
                    className="rounded border bg-background px-2 py-1 text-xs"
                  >
                    <option value="any">Any tags</option>
                    <option value="all">All tags</option>
                  </select>
                </div>
              </div>

              <div className="relative">
                <Input
                  placeholder="Search or select tags..."
                  value={tagSearchInput}
                  onChange={(e) => {
                    setTagSearchInput(e.target.value)
                    setShowTagDropdown(true)
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
                    className={`h-4 w-4 transition-transform ${showTagDropdown ? "rotate-180" : ""}`}
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                  </svg>
                </button>

                {showTagDropdown && (
                  <div className="absolute z-10 mt-1 max-h-48 w-full overflow-y-auto rounded-md border bg-white shadow-lg dark:border-gray-700 dark:bg-gray-800">
                    {(() => {
                      const filtered = availableTags.filter((tag) =>
                        tag.name.toLowerCase().includes(tagSearchInput.toLowerCase()),
                      )
                      if (filtered.length === 0) {
                        return (
                          <div className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">
                            {availableTags.length === 0 ? "No tags available" : `No tags matching "${tagSearchInput}"`}
                          </div>
                        )
                      }
                      return filtered.map((tag) => (
                        <button
                          key={tag.name}
                          type="button"
                          onClick={() =>
                            onTagsChange(
                              selectedTags.includes(tag.name)
                                ? selectedTags.filter((t) => t !== tag.name)
                                : [...selectedTags, tag.name],
                            )
                          }
                          className="flex w-full items-center justify-between px-3 py-2 text-left transition-colors hover:bg-gray-100 dark:hover:bg-gray-700"
                        >
                          <div className="flex items-center gap-2">
                            <div
                              className={`flex h-4 w-4 items-center justify-center rounded border ${
                                selectedTags.includes(tag.name)
                                  ? "border-blue-500 bg-blue-500"
                                  : "border-gray-300 dark:border-gray-600"
                              }`}
                            >
                              {selectedTags.includes(tag.name) && (
                                <svg className="h-3 w-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                  <path
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    strokeWidth={2}
                                    d="M5 13l4 4L19 7"
                                  />
                                </svg>
                              )}
                            </div>
                            <span className="text-sm">{tag.name}</span>
                          </div>
                        </button>
                      ))
                    })()}
                  </div>
                )}
              </div>

              {selectedTags.length > 0 && (
                <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                  <div className="flex flex-wrap gap-1">
                    {selectedTags.map((tagName) => (
                      <span
                        key={tagName}
                        className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2 py-1 text-xs text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                      >
                        {tagName}
                        <button
                          type="button"
                          onClick={() => onTagsChange(selectedTags.filter((t) => t !== tagName))}
                          className="hover:text-blue-600 dark:hover:text-blue-300"
                        >
                          <svg className="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        </button>
                      </span>
                    ))}
                  </div>
                  <button
                    type="button"
                    onClick={() => onTagsChange([])}
                    className="text-xs text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
                  >
                    Clear all
                  </button>
                </div>
              )}
            </div>

            {/* Storage Type Filter Section */}
            {onStorageTypeFilterChange && (
              <>
                <div className="hidden h-auto w-px bg-gray-200 dark:bg-gray-700 md:block" />
                
                <div className="flex-1 space-y-2">
                  <div className="flex items-center justify-between">
                    <Label className="text-sm font-medium">Storage type:</Label>
                    {isGoogleDriveAuthenticated && (
                      <div className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                        <Cloud className="h-3 w-3" />
                        <span>Google Drive Connected</span>
                      </div>
                    )}
                  </div>
                  
                  <div className="space-y-2">
                    <select
                      value={storageTypeFilter || ""}
                      onChange={(e) => onStorageTypeFilterChange(e.target.value as StorageType || undefined)}
                      className="w-full rounded border bg-background px-3 py-2 text-sm"
                    >
                      <option value="">All storage types</option>
                      <option value="local">💾 Local storage only</option>
                      <option value="gameguild-cloud">🏢 GameGuild Cloud</option>
                      <option value="google-drive">
                        ☁️ Google Drive {!isGoogleDriveAuthenticated ? "(Connect to access)" : ""}
                      </option>
                    </select>
                    
                    {/* Storage type quick filters */}
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => onStorageTypeFilterChange(storageTypeFilter === "local" ? undefined : "local")}
                        className={`flex items-center gap-1 px-2 py-1 rounded text-xs transition-colors ${
                          storageTypeFilter === "local"
                            ? "bg-gray-600 text-white dark:bg-gray-400 dark:text-gray-900"
                            : "bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700"
                        }`}
                        title="Show only local projects"
                      >
                        <HardDrive className="h-3 w-3" />
                        Local
                      </button>
                      
                      <button
                        type="button"
                        onClick={() => onStorageTypeFilterChange(storageTypeFilter === "gameguild-cloud" ? undefined : "gameguild-cloud")}
                        className={`flex items-center gap-1 px-2 py-1 rounded text-xs transition-colors ${
                          storageTypeFilter === "gameguild-cloud"
                            ? "bg-blue-600 text-white"
                            : "bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700"
                        }`}
                        title="Show only GameGuild Cloud projects"
                      >
                        <Database className="h-3 w-3" />
                        GameGuild
                      </button>
                      
                      <button
                        type="button"
                        onClick={() => onStorageTypeFilterChange(storageTypeFilter === "google-drive" ? undefined : "google-drive")}
                        className={`flex items-center gap-1 px-2 py-1 rounded text-xs transition-colors ${
                          storageTypeFilter === "google-drive"
                            ? "bg-green-600 text-white"
                            : isGoogleDriveAuthenticated
                            ? "bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700"
                            : "bg-gray-100 text-gray-400 cursor-not-allowed dark:bg-gray-800 dark:text-gray-600"
                        }`}
                        title={isGoogleDriveAuthenticated ? "Show only Google Drive projects" : "Connect to Google Drive first"}
                        disabled={!isGoogleDriveAuthenticated}
                      >
                        <Cloud className="h-3 w-3" />
                        Google Drive
                      </button>
                    </div>
                  </div>
                  
                  {storageTypeFilter && (
                    <div className="flex items-center justify-between pt-1">
                      <span className="text-xs text-gray-600 dark:text-gray-400">
                        Showing: {storageTypeFilter === "gameguild-cloud" ? "GameGuild Cloud" : storageTypeFilter === "google-drive" ? "Google Drive" : storageTypeFilter} projects
                      </span>
                      <button
                        type="button"
                        onClick={() => onStorageTypeFilterChange(undefined)}
                        className="text-xs text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
                      >
                        Clear
                      </button>
                    </div>
                  )}
                </div>
              </>
            )}
          </div>

          {/* Advanced Filters Section */}
          {showAdvancedFilters && (
            <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
              <div className="flex items-center justify-between mb-3">
                <Label className="text-sm font-medium">Advanced Filters</Label>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    onAuthorFilterChange?.("")
                    onStatusFilterChange?.("all")
                    onAccessFilterChange?.("all")
                    onDateFromFilterChange?.("")
                    onDateToFilterChange?.("")
                    onSortOrderChange?.("newest")
                  }}
                  className="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                >
                  Clear all
                </Button>
              </div>
              
              <div className="grid grid-cols-5 gap-3">
                {/* Author Filter */}
                {onAuthorFilterChange && (
                  <div className="space-y-1">
                    <Label className="text-xs font-medium flex items-center gap-1">
                      <User className="w-3 h-3" />
                      Author
                    </Label>
                    <Input
                      placeholder="Filter by author..."
                      value={authorFilter}
                      onChange={(e) => onAuthorFilterChange(e.target.value)}
                      className="h-8 text-xs"
                    />
                  </div>
                )}

                {/* Status Filter */}
                {onStatusFilterChange && (
                  <div className="space-y-1">
                    <Label className="text-xs font-medium flex items-center gap-1">
                      <AlertCircle className="w-3 h-3" />
                      Status
                    </Label>
                    <select
                      value={statusFilter}
                      onChange={(e) => onStatusFilterChange(e.target.value as "all" | "draft" | "published" | "scheduled")}
                      className="w-full h-8 rounded border bg-background px-2 text-xs border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-1 focus:ring-blue-500 dark:focus:ring-blue-400"
                    >
                      <option value="all">All statuses</option>
                      <option value="draft">Draft</option>
                      <option value="published">Published</option>
                      <option value="scheduled">Scheduled</option>
                    </select>
                  </div>
                )}

                {/* Access Filter */}
                {onAccessFilterChange && (
                  <div className="space-y-1">
                    <Label className="text-xs font-medium">Access Level</Label>
                    <select
                      value={accessFilter}
                      onChange={(e) => onAccessFilterChange(e.target.value as "all" | "all-access" | "all-authors")}
                      className="w-full h-8 rounded border bg-background px-2 text-xs border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-1 focus:ring-blue-500 dark:focus:ring-blue-400"
                    >
                      <option value="all">All posts</option>
                      <option value="all-access">All access</option>
                      <option value="all-authors">All authors</option>
                    </select>
                  </div>
                )}

                {/* Date Range Filter (consolidated) */}
                {(onDateFromFilterChange && onDateToFilterChange) && (
                  <div className="space-y-1">
                    <Label className="text-xs font-medium flex items-center gap-1">
                      <Calendar className="w-3 h-3" />
                      Date Range
                    </Label>
                    <div className="flex gap-1">
                      <Input
                        type="date"
                        value={dateFromFilter}
                        onChange={(e) => onDateFromFilterChange(e.target.value)}
                        className="h-8 text-xs flex-1"
                        placeholder="From"
                        title="Date From"
                      />
                      <Input
                        type="date"
                        value={dateToFilter}
                        onChange={(e) => onDateToFilterChange(e.target.value)}
                        className="h-8 text-xs flex-1"
                        placeholder="To"
                        title="Date To"
                      />
                    </div>
                  </div>
                )}

                {/* Sort Order Filter */}
                {onSortOrderChange && (
                  <div className="space-y-1">
                    <Label className="text-xs font-medium">Sort Order</Label>
                    <select
                      value={sortOrder}
                      onChange={(e) => onSortOrderChange(e.target.value as "newest" | "oldest" | "name" | "name-desc")}
                      className="w-full h-8 rounded border bg-background px-2 text-xs border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-1 focus:ring-blue-500 dark:focus:ring-blue-400"
                    >
                      <option value="newest">Newest first</option>
                      <option value="oldest">Oldest first</option>
                      <option value="name">Name A-Z</option>
                      <option value="name-desc">Name Z-A</option>
                    </select>
                  </div>
                )}
              </div>

              {/* Active Advanced Filters Summary */}
              {(authorFilter || statusFilter !== "all" || dateFromFilter || dateToFilter || accessFilter !== "all" || sortOrder !== "newest") && (
                <div className="mt-3 pt-2 border-t border-gray-200 dark:border-gray-600">
                  <div className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
                    <span className="font-medium">Active advanced filters:</span>
                    <div className="flex flex-wrap gap-1">
                      {authorFilter && (
                        <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded-full dark:bg-blue-900 dark:text-blue-200">
                          Author: {authorFilter}
                        </span>
                      )}
                      {statusFilter !== "all" && (
                        <span className="px-2 py-1 bg-green-100 text-green-800 rounded-full dark:bg-green-900 dark:text-green-200">
                          Status: {statusFilter}
                        </span>
                      )}
                      {accessFilter !== "all" && (
                        <span className="px-2 py-1 bg-orange-100 text-orange-800 rounded-full dark:bg-orange-900 dark:text-orange-200">
                          Access: {accessFilter}
                        </span>
                      )}
                      {(dateFromFilter || dateToFilter) && (
                        <span className="px-2 py-1 bg-purple-100 text-purple-800 rounded-full dark:bg-purple-900 dark:text-purple-200">
                          Date: {dateFromFilter || '...'} → {dateToFilter || '...'}
                        </span>
                      )}
                      {sortOrder !== "newest" && (
                        <span className="px-2 py-1 bg-indigo-100 text-indigo-800 rounded-full dark:bg-indigo-900 dark:text-indigo-200">
                          Sort: {sortOrder === "oldest" ? "Oldest first" : sortOrder === "name" ? "Name A-Z" : "Name Z-A"}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  )
}