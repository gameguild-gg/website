'use client'

import { Tree, FileTree } from '@/components/ui/tree'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useState, useEffect, useRef, useCallback } from 'react'
import { HierarchyBasev1_0_0 } from '@/interface-base/structure.base.v1.0.0';
import { CodeFile } from '../types/codeEditor';

interface FileExplorerProps {
  codeFiles: CodeFile[];
  activeFileIndex: number;
  onTabChange: (index: number) => void;
  mode: 'light' | 'dark' | 'high-contrast';
  hierarchy: HierarchyBasev1_0_0;
}


export function FileExplorer({ codeFiles, activeFileIndex, onTabChange, mode, hierarchy }: FileExplorerProps) {
  const [expandedItems, setExpandedItems] = useState<string[]>([]);

  const toggleItem = (item: string) => {
    setExpandedItems(prev =>
      prev.includes(item)
        ? prev.filter(i => i !== item)
        : [...prev, item]
    );
  };

  const renderFileTree = (files: CodeFile[]) => {
    return (
      <FileTree>
        {files.map((file, index) => (
          <Tree.Item
            key={index}
            onClick={() => onTabChange(index)}
            className={cn(
              'cursor-pointer',
              index === activeFileIndex
                ? mode === 'light'
                  ? 'bg-blue-100 text-blue-800'
                  : mode === 'dark'
                  ? 'bg-blue-900 text-blue-100'
                  : 'bg-yellow-300 text-black'
                : mode === 'light'
                ? 'hover:bg-gray-100'
                : mode === 'dark'
                ? 'hover:bg-gray-700'
                : 'hover:bg-gray-800'
            )}
          >
            {file.name}
          </Tree.Item>
        ))}
      </FileTree>
    );
  };

  return (
    <div className="border-r border-gray-200 dark:border-gray-600 h-full overflow-y-auto">
      <div className="p-4">
        <h2 className="text-lg font-semibold mb-2">File Explorer</h2>
        {renderFileTree(codeFiles)}
      </div>
    </div>
  );
}

