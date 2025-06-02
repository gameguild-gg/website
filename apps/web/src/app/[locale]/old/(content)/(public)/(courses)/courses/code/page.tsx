'use client';

import { useCode } from '@/components/code/use-code';
import { useState } from 'react';
import { TextArea } from '@/components/ui/textArea';
import { CodeLanguage, RunnerStatus } from '@/components/code/types';
import { Button } from '@/components/chess/ui/button';

const OutputSection = ({ title, output, className = '' }: { title: string; output: string; className?: string }) => (
  <div className={`mb-4 ${className}`}>
    <h3 className="text-white text-sm font-semibold mb-1 flex items-center">
      <span className="mr-2">{title}</span>
      {output && <div className="h-px flex-grow bg-gray-700" />}
    </h3>
    {output && (
      <pre className="p-2 font-mono whitespace-pre-wrap break-words bg-gray-800 rounded border border-gray-700 overflow-y-auto max-h-[400px]">
        {output.split('\n').map((line, i) => (
          <span key={i} className="block min-h-[1.2em]">
            {line || '\u00A0'}
          </span>
        ))}
      </pre>
    )}
  </div>
);

export default function CodeEditor() {
  const [code, setCode] = useState('#include <iostream>\n\nint main() {\n  std::cout << "Hello, World!" << std::endl;\n  return 0;\n}');
  const [language, setLanguage] = useState<CodeLanguage>('cpp');
  const { compileAndRun, abort, status, error, initOutput, compileOutput, linkOutput, executeOutput } = useCode();

  const handleRun = async () => {
    if (status === RunnerStatus.READY || status === RunnerStatus.UNINITIALIZED) {
      let filename: string;
      switch (language) {
        case 'c':
          filename = 'main.c';
          break;
        case 'cpp':
          filename = 'main.cpp';
          break;
        case 'python':
          filename = 'main.py';
          break;
        default:
          filename = 'main.txt';
      }

      await compileAndRun({
        language,
        data: { [filename]: code },
      });
    }
  };

  const handleLanguageChange = (newLanguage: CodeLanguage) => {
    setLanguage(newLanguage);

    // Update code template based on selected language
    switch (newLanguage) {
      case 'c':
        setCode('#include <stdio.h>\n\nint main() {\n  printf("Hello, World!\\n");\n  return 0;\n}');
        break;
      case 'cpp':
        setCode('#include <iostream>\n\nint main() {\n  std::cout << "Hello, World!" << std::endl;\n  return 0;\n}');
        break;
      case 'python':
        setCode('# Python Hello World program\n\nprint("Hello, World!")');
        break;
    }
  };

  // Determine which output sections to show based on language
  const showCompilationOutputs = language === 'c' || language === 'cpp';

  return (
    <div className="flex flex-col h-full w-full">
      <div className="w-full mb-2">
        <div className="flex gap-4 mb-2">
          <button
            className={`px-3 py-1 rounded ${language === 'cpp' ? 'bg-blue-600 text-white' : 'bg-gray-700 text-gray-300'}`}
            onClick={() => handleLanguageChange('cpp')}
          >
            C++
          </button>
          <button
            className={`px-3 py-1 rounded ${language === 'c' ? 'bg-blue-600 text-white' : 'bg-gray-700 text-gray-300'}`}
            onClick={() => handleLanguageChange('c')}
          >
            C
          </button>
          <button
            className={`px-3 py-1 rounded ${language === 'python' ? 'bg-blue-600 text-white' : 'bg-gray-700 text-gray-300'}`}
            onClick={() => handleLanguageChange('python')}
          >
            Python
          </button>
        </div>
        <TextArea value={code} onChange={(e) => setCode(e.target.value)} />
      </div>
      <div className="flex gap-2 my-2">
        <Button disabled={status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING} onClick={handleRun}>
          {status === RunnerStatus.UNINITIALIZED ||
          status === RunnerStatus.FAILED_LOADING ||
          status === RunnerStatus.FAILED_EXECUTION ||
          status === RunnerStatus.READY
            ? 'Run'
            : 'Running...'}
        </Button>
        <Button onClick={abort} disabled={status !== RunnerStatus.RUNNING}>
          Stop
        </Button>
      </div>
      <div className="w-full bg-gray-900 text-white overflow-auto p-4 flex flex-col">
        {showCompilationOutputs ? (
          <>
            <OutputSection title="Initialization" output={initOutput} />
            <OutputSection title="Compilation" output={compileOutput} />
            <OutputSection title="Linking" output={linkOutput} />
            <OutputSection title="Execution" output={executeOutput} />
          </>
        ) : (
          <OutputSection title="Output" output={executeOutput} />
        )}
        {error && <div className="p-2 text-red-500 font-semibold">{error}</div>}
      </div>
    </div>
  );
}
