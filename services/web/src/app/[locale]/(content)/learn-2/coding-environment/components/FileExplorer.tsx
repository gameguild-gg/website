import React, { useState, useEffect, useRef } from 'react'
import { X, ChevronLeft, ChevronRight, FileText, Folder, FilePlus, FileDown } from 'lucide-react'
import { HierarchyBasev1_0_0 } from '@/interface-base/structure.base.v1.0.0';
import { CodeFile } from '../types/codeEditor';

interface FileExplorerProps {
  codeFiles: CodeFile[]
  activeFileIndex: number
  onTabChange: (index: number) => void
  mode: 'light' | 'dark' | 'high-contrast'
  hierarchy: HierarchyBasev1_0_0
}

export default function FileExplorer({ codeFiles, activeFileIndex, onTabChange, mode, hierarchy }: FileExplorerProps) {
  const tabsRef = useRef<HTMLDivElement>(null)
  const [showLeftArrow, setShowLeftArrow] = useState(false)
  const [showRightArrow, setShowRightArrow] = useState(false)

  const checkForArrows = () => {
    if (tabsRef?.current) {
      const { scrollLeft, scrollWidth, clientWidth } = tabsRef.current;
      if (scrollLeft && scrollWidth && clientWidth) {
        setShowLeftArrow(scrollLeft > 0);
        setShowRightArrow(scrollLeft < scrollWidth - clientWidth);
      }
    }
  }

  useEffect(() => {
    checkForArrows()
    window.addEventListener('resize', checkForArrows)
    return () => window.removeEventListener('resize', checkForArrows)
  }, [codeFiles])

  const scroll = (direction: 'left' | 'right') => {
    if (tabsRef.current) {
      const { scrollWidth, clientWidth } = tabsRef.current
      const scrollAmount = direction === 'left' ? 0 : scrollWidth - clientWidth
      tabsRef.current.scrollTo({ left: scrollAmount, behavior: 'smooth' })
      setTimeout(checkForArrows, 300)
    }
  }

  return (
    <div className="border-t border-gray-200 dark:border-gray-600 h-48">
      <div className={`flex items-center ${
        mode === 'light'
          ? 'bg-gray-100 border-gray-300'
          : mode === 'dark'
          ? 'bg-gray-800 border-gray-700'
          : 'bg-black border-gray-600'
      }`}>
        {showLeftArrow && (
          <button
            onClick={() => scroll('left')}
            className={`p-2 ${
              mode === 'light' ? 'text-gray-600 hover:bg-gray-200' :
              mode === 'dark' ? 'text-gray-400 hover:bg-gray-700' :
              'text-yellow-300 hover:bg-gray-800'
            }`}
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
        )}
        <div
          ref={tabsRef}
          className="flex overflow-x-auto scrollbar-hide flex-1"
          onScroll={checkForArrows}
        >
          {codeFiles.map((file, index) => (
            <button
              key={index}
              className={`px-3 py-2 text-sm font-medium flex items-center whitespace-nowrap ${
                index === activeFileIndex
                  ? mode === 'light'
                    ? 'bg-white text-blue-600 border-t-2 border-blue-500'
                    : mode === 'dark'
                    ? 'bg-[#1e1e1e] text-white border-t-2 border-blue-500'
                    : 'bg-black text-yellow-300 border-t-2 border-yellow-300'
                  : mode === 'light'
                  ? 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  : mode === 'dark'
                  ? 'bg-gray-700 text-gray-400 hover:bg-gray-600'
                  : 'bg-gray-900 text-gray-300 hover:bg-gray-800'
              }`}
              onClick={() => onTabChange(index)}
            >
              {file.language === 'folder' ? <Folder className="mr-2 w-4 h-4" /> : <FileText className="mr-2 w-4 h-4" />}
              {file.name}
            </button>
          ))}
        </div>
        {showRightArrow && (
          <button
            onClick={() => scroll('right')}
            className={`p-2 ${
              mode === 'light' ? 'text-gray-600 hover:bg-gray-200' :
              mode === 'dark' ? 'text-gray-400 hover:bg-gray-700' :
              'text-yellow-300 hover:bg-gray-800'
            }`}
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        )}
      </div>
    </div>
  )
}

