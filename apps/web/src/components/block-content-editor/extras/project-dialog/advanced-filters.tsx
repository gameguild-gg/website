"use client"

import { Input } from "@game-guild/ui/components/input"
import { Label } from "@game-guild/ui/components/label"
import { Button } from "@game-guild/ui/components/button"
import { Calendar, CalendarCheck, User, AlertCircle } from "lucide-react"

interface AdvancedFiltersProps {
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
  showAdvanced?: boolean
}

export function AdvancedFilters({
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
  showAdvanced = false
}: AdvancedFiltersProps) {
  if (!showAdvanced) {
    return null
  }

  const clearAllFilters = () => {
    onAuthorFilterChange?.("")
    onStatusFilterChange?.("all")
    onDateFromFilterChange?.("")
    onDateToFilterChange?.("")
    onAccessFilterChange?.("all")
  }

  const hasActiveFilters = authorFilter || statusFilter !== "all" || dateFromFilter || dateToFilter || accessFilter !== "all"

  return (
    <div className="space-y-4 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
      <div className="flex items-center justify-between">
        <Label className="text-base font-medium">Advanced Filters</Label>
        {hasActiveFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={clearAllFilters}
            className="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            Clear all
          </Button>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Author Filter */}
        {onAuthorFilterChange && (
          <div className="space-y-2">
            <Label className="text-sm font-medium flex items-center gap-2">
              <User className="w-4 h-4" />
              Author
            </Label>
            <Input
              placeholder="Filter by author..."
              value={authorFilter}
              onChange={(e) => onAuthorFilterChange(e.target.value)}
              className="w-full"
            />
          </div>
        )}

        {/* Status Filter */}
        {onStatusFilterChange && (
          <div className="space-y-2">
            <Label className="text-sm font-medium flex items-center gap-2">
              <AlertCircle className="w-4 h-4" />
              Status
            </Label>
            <select
              value={statusFilter}
              onChange={(e) => onStatusFilterChange(e.target.value as "all" | "draft" | "published" | "scheduled")}
              className="w-full rounded border bg-background px-3 py-2 text-sm border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-1 focus:ring-blue-500 dark:focus:ring-blue-400"
            >
              <option value="all">All statuses</option>
              <option value="draft">Draft</option>
              <option value="published">Published</option>
              <option value="scheduled">Scheduled</option>
            </select>
          </div>
        )}

        {/* Date From Filter */}
        {onDateFromFilterChange && (
          <div className="space-y-2">
            <Label className="text-sm font-medium flex items-center gap-2">
              <Calendar className="w-4 h-4" />
              Date From
            </Label>
            <Input
              type="date"
              value={dateFromFilter}
              onChange={(e) => onDateFromFilterChange(e.target.value)}
              className="w-full"
            />
          </div>
        )}

        {/* Date To Filter */}
        {onDateToFilterChange && (
          <div className="space-y-2">
            <Label className="text-sm font-medium flex items-center gap-2">
              <CalendarCheck className="w-4 h-4" />
              Date To
            </Label>
            <Input
              type="date"
              value={dateToFilter}
              onChange={(e) => { onDateToFilterChange(e.target.value); }}
              className="w-full"
            />
          </div>
        )}

        {/* Access Filter */}
        {onAccessFilterChange && (
          <div className="space-y-2 md:col-span-2 lg:col-span-1">
            <Label className="text-sm font-medium">Access Level</Label>
            <select
              value={accessFilter}
              onChange={(e) => onAccessFilterChange(e.target.value as "all" | "all-access" | "all-authors")}
              className="w-full rounded border bg-background px-3 py-2 text-sm border-gray-300 dark:border-gray-600 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-1 focus:ring-blue-500 dark:focus:ring-blue-400"
            >
              <option value="all">All posts</option>
              <option value="all-access">All access</option>
              <option value="all-authors">All authors</option>
            </select>
          </div>
        )}
      </div>

      {/* Active Filters Summary */}
      {hasActiveFilters && (
        <div className="pt-3 border-t border-gray-200 dark:border-gray-600">
          <div className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
            <span className="font-medium">Active filters:</span>
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
              {dateFromFilter && (
                <span className="px-2 py-1 bg-purple-100 text-purple-800 rounded-full dark:bg-purple-900 dark:text-purple-200">
                  From: {dateFromFilter}
                </span>
              )}
              {dateToFilter && (
                <span className="px-2 py-1 bg-purple-100 text-purple-800 rounded-full dark:bg-purple-900 dark:text-purple-200">
                  To: {dateToFilter}
                </span>
              )}
              {accessFilter !== "all" && (
                <span className="px-2 py-1 bg-orange-100 text-orange-800 rounded-full dark:bg-orange-900 dark:text-orange-200">
                  Access: {accessFilter}
                </span>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}