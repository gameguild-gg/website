'use client';

import { useClang } from '@/components/code/use-clang';
import { useState } from 'react';
import { TextArea } from '@/components/ui/textArea';
import { RunnerStatus } from '@/components/code/types';
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

export default function ClangEditor() {
  const [code, setCode] = useState('#include <iostream>\n\nint main() {\n  std::cout << "Hello, World!" << std::endl;\n  return 0;\n}');
  const { compileAndRun, abort, status, initOutput, compilerOutput, linkerOutput, executionOutput, error } = useClang();

  const handleRun = async () => {
    if (status === RunnerStatus.READY || status === RunnerStatus.UNINITIALIZED) {
      const result = await compileAndRun(code);
    }
  };

  return (
    <div className="flex flex-col h-full w-full">
      <div className="w-full mb-4">
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
        <OutputSection title="Initialization" output={initOutput} />
        <OutputSection title="Compilation" output={compilerOutput} />
        <OutputSection title="Linking" output={linkerOutput} />
        <OutputSection title="Execution" output={executionOutput} />
        {error && <div className="p-2 text-red-500 font-semibold">{error}</div>}
      </div>
    </div>
  );
}
