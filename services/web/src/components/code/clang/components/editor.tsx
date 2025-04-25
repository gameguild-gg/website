import React, { useMemo, useCallback } from 'react';
import { LanguageExt, useRunner, RunnerStatus, useClang } from '../module';
import LanguageSelector from './langauge-selector';
import MonacoEditor from '@monaco-editor/react';
import { Play } from 'lucide-react';

export default function Editor() {
  const codeMap = useRunner((state) => state.codeMap);
  const language = useRunner((state) => state.language);
  const setCode = useRunner((state) => state.setCode);
  const { compileAndRun, status } = useClang();

  const fileExtension = useMemo(() => `test${LanguageExt[language]}`, [language]);

  const handleRun = useCallback(async () => {
    if (status === RunnerStatus.READY || status === RunnerStatus.UNINITIALIZED) {
      await compileAndRun(codeMap[language]);
    }
  }, [compileAndRun, codeMap, language, status]);

  return (
    <div className="h-full flex flex-col">
      <div className="h-[50px] flex items-center justify-between px-4 flex-shrink-0">
        <span className="font-medium text-sm text-gray-700">{fileExtension}</span>
        <div className="flex items-center gap-2">
          <LanguageSelector />
          <button 
            className="focus:outline-transparent leading-none" 
            onClick={handleRun}
            disabled={status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING}
          >
            <Play className={`h-5 w-5 ${status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING ? 'text-gray-400' : 'text-green-500'}`} />
          </button>
        </div>
      </div>
      <div className="flex-grow">
        <MonacoEditor
          value={codeMap[language]}
          onChange={(value) => setCode(value || '')}
          language={language.toLowerCase()}
        />
      </div>
    </div>
  );
}
