"use client"

import React from 'react'
import { type FilterConfig } from '../types'
import { FilterSearch } from './filter-search'
import { FilterTags } from './filter-tags'
import { FilterSelect } from './filter-select'
import { FilterMimeTypes } from './filter-mime-types'
import { FilterSort } from './filter-sort'
import { FilterActiveBadges } from './filter-active-badges'
import {
  STORAGE_TYPE_OPTIONS,
  MIME_TYPE_OPTIONS,
  ASSET_TYPE_OPTIONS,
  USAGE_FILTER_OPTIONS,
  ITEMS_PER_PAGE_OPTIONS,
} from './filter-options'

interface ManagerFiltersProps {
  filters: FilterConfig
  onFilterChange: (filters: Partial<FilterConfig>) => void
  availableTags?: Array<{ name: string }>
  availableProjects?: Array<{ id: string; name: string }>
  contextType: 'projects' | 'assets' | 'collections'
  itemsPerPage: number
  onItemsPerPageChange: (items: number) => void
}

export function ManagerFilters({
  filters,
  onFilterChange,
  availableTags = [],
  availableProjects = [],
  contextType,
  itemsPerPage,
  onItemsPerPageChange,
}: ManagerFiltersProps) {
  const isProjectContext = contextType === 'projects'
  const isAssetContext = contextType === 'assets'
  const isCollectionContext = contextType === 'collections'

  const searchPlaceholder = isProjectContext 
    ? "Search projects..." 
    : isAssetContext 
    ? "Search assets..." 
    : "Search collections..."

  const projectOptions = [
    { value: 'all', label: 'All projects' },
    ...availableProjects.map(p => ({ value: p.id, label: p.name }))
  ]

  return (
    <div className="p-4 space-y-4">
      {/* Search and Tags Row */}
      <div className="flex items-center gap-2 flex-wrap">
        <FilterSearch
          value={filters.searchTerm}
          onChange={(value) => onFilterChange({ searchTerm: value })}
          placeholder={searchPlaceholder}
        />

        {(isProjectContext || isCollectionContext) && availableTags.length > 0 && (
          <FilterTags
            selectedTags={filters.tags || []}
            availableTags={availableTags}
            tagFilterMode={filters.tagFilterMode || 'all'}
            onTagsChange={(tags) => onFilterChange({ tags })}
            onModeChange={(mode) => onFilterChange({ tagFilterMode: mode })}
          />
        )}

        <FilterSelect
          value={itemsPerPage.toString()}
          onChange={(value) => onItemsPerPageChange(parseInt(value))}
          options={ITEMS_PER_PAGE_OPTIONS}
          className="w-[140px]"
        />
      </div>

      {/* Additional Filters Row */}
      <div className="flex items-center gap-2 flex-wrap">
        {isProjectContext && (
          <FilterSelect
            value={filters.storageType || 'all'}
            onChange={(value) => onFilterChange({ storageType: value as any })}
            options={STORAGE_TYPE_OPTIONS}
            placeholder="Storage type"
            className="w-[180px]"
          />
        )}

        {isAssetContext && (
          <>
            <FilterMimeTypes
              selectedTypes={filters.mimeTypes || []}
              onChange={(types) => { onFilterChange({ mimeTypes: types }); }}
            />

            <FilterSelect
              value={filters.assetType || 'all'}
              onChange={(value) => onFilterChange({ assetType: value as any })}
              options={ASSET_TYPE_OPTIONS}
              placeholder="Asset type"
            />

            {availableProjects.length > 0 && (
              <FilterSelect
                value={filters.projectFilter || 'all'}
                onChange={(value) => onFilterChange({ projectFilter: value })}
                options={projectOptions}
                placeholder="Filter by project"
                className="w-[180px]"
              />
            )}

            <FilterSelect
              value={filters.usageFilter || 'all'}
              onChange={(value) => onFilterChange({ usageFilter: value as any })}
              options={USAGE_FILTER_OPTIONS}
              placeholder="Usage"
            />
          </>
        )}

        <FilterSort
          sortOrder={filters.sortOrder || []}
          onSortChange={(sortOrder) => onFilterChange({ sortOrder })}
        />
      </div>

      <FilterActiveBadges
        filters={filters}
        availableProjects={availableProjects}
        onFilterChange={onFilterChange}
      />
    </div>
  )
}
