import React from 'react';
import dynamic from 'next/dynamic';
import { CodeFile } from '../types/codeEditor';
import CodeTabs from './CodeTabs';
import { CharacterCount } from './CharacterCount';

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), { ssr: false });

interface EditorPanelProps {
  files: CodeFile[];
  activeFileIndex: number;
  onTabChange: (index: number) => void;
  onEditorChange: (value: string | undefined) => void;
  mode: 'light' | 'dark' | 'high-contrast';
  currentFileChars: number;
  maxFileChars: number;
  totalChars: number;
  maxTotalChars: number;
  currentFileCount: number;
  maxFiles: number;
}

export function EditorPanel({
  files,
  activeFileIndex,
  onTabChange,
  onEditorChange,
  mode,
  currentFileChars,
  maxFileChars,
  totalChars,
  maxTotalChars,
  currentFileCount,
  maxFiles,
}: EditorPanelProps) {
  return (
    <div className="flex flex-col h-full">
      <CodeTabs files={files} activeIndex={activeFileIndex} onTabChange={onTabChange} mode={mode} />
      <div className="flex-grow overflow-auto">
        <MonacoEditor
          height="100%"
          language={files[activeFileIndex]?.language || 'plaintext'}
          value={files[activeFileIndex]?.content || ''}
          theme={mode === 'light' ? 'vs-light' : mode === 'dark' ? 'vs-dark' : 'hc-black'}
          onChange={onEditorChange}
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            scrollBeyondLastLine: false,
            automaticLayout: true,
          }}
        />
      </div>
      <CharacterCount
        currentFileChars={currentFileChars}
        maxFileChars={maxFileChars}
        totalChars={totalChars}
        maxTotalChars={maxTotalChars}
        currentFileCount={currentFileCount}
        maxFiles={maxFiles}
        mode={mode}
      />
    </div>
  );
}

