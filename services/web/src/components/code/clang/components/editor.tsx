import React, { useMemo } from 'react';
import { LanguageExt, useRunner } from '../module';
import LanguageSelector from './langauge-selector';
import MonacoEditor from '@monaco-editor/react';
import { Play } from 'lucide-react';

export default function Editor() {
  // Use separate hooks for each piece of state to prevent re-renders
  const codeMap = useRunner((state) => state.codeMap);
  const language = useRunner((state) => state.language);
  const setCode = useRunner((state) => state.setCode);
  const runCode = useRunner((state) => state.runCode);

  // Memoize the file extension to prevent re-renders
  const fileExtension = useMemo(() => `test${LanguageExt[language]}`, [language]);

  return (
    <div className="h-full flex flex-col">
      <div className="h-[50px] flex items-center justify-between px-4 flex-shrink-0">
        <span className="font-medium text-sm text-gray-700">{fileExtension}</span>
        <div className="flex items-center gap-2">
          <LanguageSelector />
          <button className="focus:outline-transparent leading-none" onClick={runCode}>
            <Play className="text-green-500 h-5 w-5" />
          </button>
        </div>
      </div>
      <MonacoEditor
        value={codeMap[language]}
        language={language}
        options={{ fontSize: 17, minimap: { enabled: false }, wordWrap: 'on' }}
        onChange={(code) => code && setCode(code)}
      />
    </div>
  );
}
