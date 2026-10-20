"use client"

import React, { useState } from 'react'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@game-guild/ui/components/popover"
import { Button } from "@game-guild/ui/components/button"
import { Badge } from "@game-guild/ui/components/badge"
import { Checkbox } from "@game-guild/ui/components/checkbox"
import { ChevronDown, ChevronRight, X } from "lucide-react"
import { ScrollArea } from "@game-guild/ui/components/scroll-area"

interface MimeTypeOption {
  value: string
  label: string
  extensions: string[]
  color: string
}

const MIME_TYPE_OPTIONS: MimeTypeOption[] = [
  {
    value: 'image',
    label: 'Images',
    extensions: ['.jpg', '.jpeg', '.png', '.gif', '.svg', '.webp', '.bmp', '.ico', '.tiff', '.psd'],
    color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200'
  },
  {
    value: 'video',
    label: 'Videos',
    extensions: ['.mp4', '.webm', '.ogg', '.avi', '.mov', '.wmv', '.flv', '.mkv', '.m4v', '.3gp'],
    color: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200'
  },
  {
    value: 'audio',
    label: 'Audio',
    extensions: ['.mp3', '.wav', '.ogg', '.m4a', '.flac', '.aac', '.wma', '.opus', '.oga'],
    color: 'bg-pink-100 text-pink-800 dark:bg-pink-900 dark:text-pink-200'
  },
  {
    value: 'text',
    label: 'Text & Code',
    extensions: ['.txt', '.md', '.js', '.ts', '.jsx', '.tsx', '.json', '.xml', '.html', '.css', '.scss', '.sass', '.less', '.py', '.java', '.c', '.cpp', '.h', '.hpp', '.rs', '.go', '.rb', '.php', '.sh', '.bash', '.yml', '.yaml', '.sql', '.lua', '.r', '.swift', '.kt', '.cs', '.vb', '.pl', '.dart', '.scala'],
    color: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
  },
  {
    value: 'application',
    label: 'Applications',
    extensions: ['.pdf', '.zip', '.rar', '.7z', '.tar', '.gz', '.exe', '.dmg', '.apk', '.deb', '.rpm', '.msi'],
    color: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200'
  },
  {
    value: 'font',
    label: 'Fonts',
    extensions: ['.ttf', '.otf', '.woff', '.woff2', '.eot', '.svg'],
    color: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900 dark:text-indigo-200'
  },
]

interface FilterMimeTypesProps {
  selectedTypes: string[] // Array of extensions like ['.jpg', '.png', '.mp4']
  onChange: (types: string[]) => void
  className?: string
}

export function FilterMimeTypes({ 
  selectedTypes, 
  onChange, 
  className = "w-[180px]" 
}: FilterMimeTypesProps) {
  const [open, setOpen] = useState(false)
  const [expandedCategories, setExpandedCategories] = useState<string[]>([])

  const toggleCategory = (categoryValue: string) => {
    setExpandedCategories(prev =>
      prev.includes(categoryValue)
        ? prev.filter(c => c !== categoryValue)
        : [...prev, categoryValue]
    )
  }

  const handleToggleExtension = (extension: string) => {
    if (selectedTypes.includes(extension)) {
      onChange(selectedTypes.filter(t => t !== extension))
    } else {
      onChange([...selectedTypes, extension])
    }
  }

  const handleToggleCategory = (category: MimeTypeOption) => {
    const categoryExtensions = category.extensions
    const allSelected = categoryExtensions.every(ext => selectedTypes.includes(ext))
    
    if (allSelected) {
      // Deselect all extensions from this category
      onChange(selectedTypes.filter(t => !categoryExtensions.includes(t)))
    } else {
      // Select all extensions from this category
      const newSelections = [...new Set([...selectedTypes, ...categoryExtensions])]
      onChange(newSelections)
    }
  }

  const getSelectedCountForCategory = (category: MimeTypeOption) => {
    return category.extensions.filter(ext => selectedTypes.includes(ext)).length
  }

  const isCategoryFullySelected = (category: MimeTypeOption) => {
    return category.extensions.every(ext => selectedTypes.includes(ext))
  }

  const isCategoryPartiallySelected = (category: MimeTypeOption) => {
    const count = getSelectedCountForCategory(category)
    return count > 0 && count < category.extensions.length
  }

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    onChange([])
  }

  const getButtonLabel = () => {
    if (selectedTypes.length === 0) return "File types"
    if (selectedTypes.length === 1) return selectedTypes[0]
    return `${selectedTypes.length} extensions`
  }

  return (
    <Popover
      open={open}
      onOpenChange={(nextOpen, eventDetails) => {
        if (!nextOpen && eventDetails.reason === 'outside-press') {
          const target = eventDetails.event.target as HTMLElement
          if (target.closest('[data-mime-type-content]')) {
            eventDetails.cancel()
            return
          }
        }
        setOpen(nextOpen)
      }}
    >
      <PopoverTrigger render={<Button variant="outline"
          role="combobox"
          aria-expanded={open}
          className={`${className} justify-between`} />}>
        <span className="truncate">{getButtonLabel()}</span>
        <div className="flex items-center gap-1 ml-2">
          {selectedTypes.length > 0 && (
            <button
              onClick={handleClear}
              className="hover:bg-gray-200 dark:hover:bg-gray-700 rounded-full p-0.5"
            >
              <X className="h-3 w-3" />
            </button>
          )}
          <ChevronDown className="h-4 w-4 shrink-0 opacity-50" />
        </div>
      </PopoverTrigger>
      <PopoverContent
        className="w-[500px] p-0"
        align="start"
      >
        <div className="p-3 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between mb-2">
            <h4 className="text-sm font-semibold">File Types & Extensions</h4>
            {selectedTypes.length > 0 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onChange([])}
                className="h-6 text-xs"
              >
                Clear all
              </Button>
            )}
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Select specific file extensions to filter
          </p>
        </div>

        <ScrollArea className="max-h-[500px]" data-mime-type-content>
          <div className="p-2 space-y-1">
            {MIME_TYPE_OPTIONS.map((option) => {
              const isExpanded = expandedCategories.includes(option.value)
              const selectedCount = getSelectedCountForCategory(option)
              const isFullySelected = isCategoryFullySelected(option)
              const isPartiallySelected = isCategoryPartiallySelected(option)

              return (
                <div
                  key={option.value}
                  className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden"
                >
                  {/* Category Header */}
                  <div className="flex items-center gap-2 p-2 bg-gray-50 dark:bg-gray-800/50 hover:bg-gray-100 dark:hover:bg-gray-800">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 w-6 p-0"
                      onClick={() => toggleCategory(option.value)}
                    >
                      {isExpanded ? (
                        <ChevronDown className="h-4 w-4" />
                      ) : (
                        <ChevronRight className="h-4 w-4" />
                      )}
                    </Button>
                    
                    <Checkbox
                      checked={isFullySelected}
                      indeterminate={isPartiallySelected}
                      aria-label={option.label}
                      onCheckedChange={() => handleToggleCategory(option)}
                      className="mt-0"
                    />
                    
                    <div className="flex-1 min-w-0 flex items-center gap-2">
                      <span className="text-sm font-medium">{option.label}</span>
                      <Badge 
                        variant="secondary" 
                        className={`text-xs px-1.5 py-0 ${option.color}`}
                      >
                        {selectedCount > 0 ? `${selectedCount}/` : ''}{option.extensions.length}
                      </Badge>
                    </div>

                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 text-xs px-2"
                      onClick={() => toggleCategory(option.value)}
                    >
                      {isExpanded ? 'Collapse' : 'Expand'}
                    </Button>
                  </div>

                  {/* Extensions Grid */}
                  {isExpanded && (
                    <div className="p-3 bg-white dark:bg-gray-900 border-t border-gray-200 dark:border-gray-700">
                      <div className="grid grid-cols-4 gap-2">
                        {option.extensions.map((ext) => (
                          <label
                            key={ext}
                            className="flex items-center gap-2 p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 cursor-pointer"
                          >
                            <Checkbox
                              checked={selectedTypes.includes(ext)}
                              onCheckedChange={() => handleToggleExtension(ext)}
                            />
                            <span className="text-xs font-mono text-gray-700 dark:text-gray-300">
                              {ext}
                            </span>
                          </label>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        </ScrollArea>

        <div className="p-3 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900">
          <div className="flex items-center justify-between text-xs">
            <span className="text-gray-600 dark:text-gray-400">
              {selectedTypes.length} extension{selectedTypes.length !== 1 ? 's' : ''} selected
            </span>
            <Button
              size="sm"
              onClick={() => setOpen(false)}
              className="h-7"
            >
              Done
            </Button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  )
}
