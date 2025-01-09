'use client';

import { useState } from 'react';
import ResizablePanel from './ResizablePanel';

export default function Layout({ children }: { children: React.ReactNode }) {
  const [isDarkMode, setIsDarkMode] = useState(true);

  const memoizedChildren = {
    left: children[0],
    right: children[1],
  };

  return (
    <div className={`flex h-screen w-screen ${isDarkMode ? 'dark' : ''}`}>
      <ResizablePanel
        left={memoizedChildren.left}
        right={memoizedChildren.right}
        isDarkMode={isDarkMode}
        setIsDarkMode={setIsDarkMode}
      />
    </div>
  );
}
