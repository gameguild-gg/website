'use client'

import { useState } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import Link from 'next/link'

interface CourseHierarchyProps {
  structure: any
  onSelectSection: (section: string) => void
}

export default function CourseHierarchy({ structure, onSelectSection }: CourseHierarchyProps) {
  const [expandedItems, setExpandedItems] = useState<string[]>([])

  const toggleItem = (item: string) => {
    setExpandedItems(prev => 
      prev.includes(item) 
        ? prev.filter(i => i !== item)
        : [...prev, item]
    )
  }

  const renderTree = (tree: any, prefix: string = '') => {
    return Object.entries(tree).map(([key, value]) => {
      const fullPath = `${prefix}${key}`
      const isExpandable = typeof value === 'object' && Object.keys(value).length > 0
      const isExpanded = expandedItems.includes(fullPath)

      return (
        <li key={fullPath} className="mb-2">
          <div className="flex items-center">
            {isExpandable && (
              <button
                onClick={() => toggleItem(fullPath)}
                className="mr-2"
              >
                {isExpanded ? <ChevronDown size={20} /> : <ChevronRight size={20} />}
              </button>
            )}
            <span 
              className="cursor-pointer hover:text-blue-600"
              onClick={() => onSelectSection(fullPath)}
            >
              {key}
            </span>
          </div>
          {isExpanded && isExpandable && (
            <ul className="ml-6 mt-2">
              {renderTree(value, `${fullPath}/`)}
            </ul>
          )}
        </li>
      )
    })
  }

  return (
    <nav className="w-64 bg-white shadow-md p-4">
      <h2 className="text-lg font-semibold mb-4">Course Structure</h2>
      <ul>
        {structure && renderTree(structure)}
      </ul>
    </nav>
  )
}

