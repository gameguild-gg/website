'use client'

import Link from 'next/link'
import { Sun, Moon, ZapOff, ChevronLeft } from 'lucide-react'

interface PageHeaderProps {
  title: string
  backLink?: string // Make backLink optional
  userId: string | null
  mode: 'light' | 'dark' | 'high-contrast'
  onModeToggle: () => void
}

export default function PageHeader({ title, backLink, userId, mode, onModeToggle }: PageHeaderProps) {
  return (
    <div className="flex items-center mb-4">
      {backLink && ( // Conditionally render the back button
        <Link
          href={backLink}
          className={`mr-4 p-2 rounded-full transition-colors duration-200 ${
            mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
            mode === 'dark' ? 'bg-gray-700 text-gray-200 hover:bg-gray-600' :
            'bg-yellow-300 text-black hover:bg-yellow-400'
          }`}
        >
          <ChevronLeft className="w-6 h-6" />
        </Link>
      )}
      <div className="flex-grow text-center">
        <h1 className="text-3xl font-bold">{title}</h1>
      </div>
      <div>
        <button onClick={onModeToggle} className={`p-2 rounded-full transition-colors duration-200 ${
          mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
          mode === 'dark' ? 'bg-gray-700 text-gray-200 hover:bg-gray-600' :
          'bg-yellow-300 text-black hover:bg-yellow-400'
        }`}>
          {mode === 'light' ? <Sun className="w-5 h-5" /> : mode === 'dark' ? <Moon className="w-5 h-5" /> : <ZapOff className="w-5 h-5" />}
        </button>
      </div>
    </div>
  )
}

