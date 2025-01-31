'use client'

import { useState } from 'react'
import ResizablePanel from './ResizablePanel'

export default function Layout({ children }: { children: React.ReactNode }) {
  //const [isDarkMode, setIsDarkMode] = useState(true)

  const memoizedChildren = {
    leftPanel: children[0],
    rightPanel: children[1]
  }

  return (
    <div className={`flex h-screen w-screen`}>
      <ResizablePanel
        leftPanel={memoizedChildren.leftPanel}
        rightPanel={memoizedChildren.rightPanel} direction={'horizontal'}        //isDarkMode={isDarkMode}
        //setIsDarkMode={setIsDarkMode}
      />
    </div>
  )
}

