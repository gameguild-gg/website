import React, { useState, useEffect, useRef } from 'react'
import { X, ChevronLeft, ChevronRight, FileText } from 'lucide-react'

interface CodeFile {
  name: string
  content: string
  language: string
}

interface CodeTabsProps {
  files: CodeFile[]
  activeIndex: number
  onTabChange: (index: number) => void
  onCloseTab: (index: number) => void
  mode: 'light' | 'dark' | 'high-contrast'
}

export default function CodeTabs({ files, activeIndex, onTabChange, onCloseTab, mode }: CodeTabsProps) {
  const tabsRef = useRef<HTMLDivElement>(null)
  const [showLeftArrow, setShowLeftArrow] = useState(false)
  const [showRightArrow, setShowRightArrow] = useState(false)

  const checkForArrows = () => {
    if (tabsRef?.current) { // Add optional chaining
      const { scrollLeft, scrollWidth, clientWidth } = tabsRef.current;
      if (scrollLeft && scrollWidth && clientWidth) { // Add conditional check
        setShowLeftArrow(scrollLeft > 0);
        setShowRightArrow(scrollLeft < scrollWidth - clientWidth);
      }
    }
  }

  useEffect(() => {
    checkForArrows()
    window.addEventListener('resize', checkForArrows)
    return () => window.removeEventListener('resize', checkForArrows)
  }, [files])

  const scroll = (direction: 'left' | 'right') => {
    if (tabsRef.current) {
      const { scrollWidth, clientWidth } = tabsRef.current
      const scrollAmount = direction === 'left' ? 0 : scrollWidth - clientWidth
      tabsRef.current.scrollTo({ left: scrollAmount, behavior: 'smooth' })
      setTimeout(checkForArrows, 300) // Check after animation completes
    }
  }

  return (
    <div className={`flex items-center ${
      mode === 'light'
        ? 'bg-gray-100 border-gray-300'
        : mode === 'dark'
        ? 'bg-gray-800 border-gray-700'
        : 'bg-black border-gray-600'
    } border-b`}>
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
        className="flex overflow-x-auto scrollbar-hide"
        onScroll={checkForArrows}
      >
        {files.map((file, index) => (
          <div
            key={index}
            onClick={() => onTabChange(index)}
            className={`px-3 py-2 text-sm font-medium flex items-center whitespace-nowrap group cursor-pointer ${
              index === activeIndex
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
          >
            <span>{file.name}</span>
            <button
              onClick={(e) => {
                e.stopPropagation();
                onCloseTab(index);
              }}
              className={`ml-2 p-1 rounded-full opacity-0 group-hover:opacity-100 transition-opacity ${
                mode === 'light'
                  ? 'hover:bg-gray-300'
                  : mode === 'dark'
                  ? 'hover:bg-gray-500'
                  : 'hover:bg-gray-700'
              }`}
            >
              <X className="w-3 h-3" />
            </button>
          </div>
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
  )
}

