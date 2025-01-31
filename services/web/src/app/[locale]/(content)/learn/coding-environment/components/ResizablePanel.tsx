'use client'

import React, { useState, useCallback, useEffect, useRef } from 'react'
import { GripVertical, GripHorizontal } from 'lucide-react'

const debounce = (func: Function, wait: number) => {
  let timeout: NodeJS.Timeout
  return (...args: any[]) => {
    clearTimeout(timeout)
    timeout = setTimeout(() => func(...args), wait)
  }
}

interface ResizablePanelProps {
  leftPanel?: React.ReactNode
  rightPanel?: React.ReactNode
  top?: React.ReactNode
  bottom?: React.ReactNode
  direction: 'horizontal' | 'vertical'
  initialSize?: number
  minSize?: number
  maxSize?: number
  isDarkMode?: string
}

const ResizablePanel: React.FC<ResizablePanelProps> = ({ 
  leftPanel, 
  rightPanel, 
  top, 
  bottom, 
  direction, 
  initialSize = 50,
  minSize = 10,
  maxSize = 90
}) => {
  const [size, setSize] = useState(initialSize)
  const panelRef = useRef<HTMLDivElement>(null)

  const handleResize = useCallback(
    debounce(() => {
      if (!panelRef.current) return
      const { width, height } = panelRef.current.getBoundingClientRect()
      if (direction === 'horizontal') {
        setSize((width / window.innerWidth) * 100)
      } else {
        setSize((height / window.innerHeight) * 100)
      }
    }, 16),
    [direction]
  )

  useEffect(() => {
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [handleResize])

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault()
    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }, [])

  const handleMouseMove = useCallback((e: MouseEvent) => {
    if (direction === 'horizontal') {
      const newSize = (e.clientX / window.innerWidth) * 100
      setSize(Math.min(Math.max(newSize, minSize), maxSize))
    } else {
      const newSize = (e.clientY / window.innerHeight) * 100
      setSize(Math.min(Math.max(newSize, minSize), maxSize))
    }
  }, [direction, minSize, maxSize])

  const handleMouseUp = useCallback(() => {
    document.removeEventListener('mousemove', handleMouseMove)
    document.removeEventListener('mouseup', handleMouseUp)
  }, [handleMouseMove])

  if (direction === 'horizontal') {
    return (
      <div ref={panelRef} className="flex h-full bg-[#1E1E1E] text-[#D4D4D4]">
        <div style={{ width: `${size}%` }} className="overflow-auto">
          {leftPanel}
        </div>
        <div
          className="w-2 bg-[#3C3C3C] cursor-col-resize flex items-center justify-center"
          onMouseDown={handleMouseDown}
        >
          <GripVertical className="w-4 h-4 text-[#6A6A6A]" />
        </div>
        <div style={{ width: `${100 - size}%` }} className="overflow-auto">
          {rightPanel}
        </div>
      </div>
    )
  } else {
    return (
      <div ref={panelRef} className="flex flex-col h-full bg-[#1E1E1E] text-[#D4D4D4]">
        <div style={{ height: `${size}%` }} className="overflow-auto">
          {top}
        </div>
        <div
          className="h-2 bg-[#3C3C3C] cursor-row-resize flex items-center justify-center"
          onMouseDown={handleMouseDown}
        >
          <GripHorizontal className="w-4 h-4 text-[#6A6A6A]" />
        </div>
        <div style={{ height: `${100 - size}%` }} className="overflow-auto">
          {bottom}
        </div>
      </div>
    )
  }
}

export default ResizablePanel

