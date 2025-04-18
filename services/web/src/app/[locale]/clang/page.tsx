'use client';

import { useClang } from '@/components/code/use-clang';
import { useState } from 'react';
import { TextArea } from '@/components/ui/textArea';
import { RunnerStatus } from '@/components/code/code-executor.types';
import { Button } from '@/components/chess/ui/button';

const OutputSection = ({ title, output }: { title: string; output: string }) => (
  <div className="mb-4">
    <h3 className="text-white text-sm font-semibold mb-1">{title}</h3>
    {output && <div className="p-2 font-mono whitespace-pre-wrap">{output}</div>}
  </div>
);

export default function ClangEditor() {
  const [code, setCode] = useState('#include <iostream>\n\nint main() {\n  std::cout << "Hello, World!" << std::endl;\n  return 0;\n}');
  const { 
    compileAndRun, 
    abort, 
    status, 
    initOutput,
    compilerOutput,
    linkerOutput,
    executionOutput,
    error 
  } = useClang();

  const handleRun = async () => {
    if (status === RunnerStatus.READY || status === RunnerStatus.UNINITIALIZED) {
      console.log('handleRun called');
      const result = await compileAndRun(code);
      console.log('Compilation result:', result);
    }
  };

  return (
    <div className="flex flex-col h-full">
      <div className="h-1/2">
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
      <div className="h-1/2 bg-gray-900 text-white overflow-auto p-4">
        <OutputSection title="Initialization" output={initOutput} />
        <OutputSection title="Compilation" output={compilerOutput} />
        <OutputSection title="Linking" output={linkerOutput} />
        <OutputSection title="Execution" output={executionOutput} />
        {error && <div className="p-2 text-red-500 font-semibold">{error}</div>}
      </div>
    </div>
  );
}
