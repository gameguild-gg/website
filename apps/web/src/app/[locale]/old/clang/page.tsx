'use client';

import { useClang } from '@/components/code/use-clang';
import { useState } from 'react';
import { TextArea } from '@/components/ui/textArea';
import { RunnerStatus } from '@/components/code/code-executor.types';
import { Button } from '@/components/chess/ui/button';

export default function ClangEditor() {
  const [code, setCode] = useState('#include <iostream>\n\nint main() {\n  std::cout << "Hello, World!" << std::endl;\n  return 0;\n}');
  const { compileAndRun, abort, stdout, stderr, error, status } = useClang();

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
      <div className="flex">
        <Button disabled={status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING} onClick={handleRun}>
          {
            // if it is uninitialized, failed, or ready, show "Run"
            status === RunnerStatus.UNINITIALIZED ||
            status === RunnerStatus.FAILED_LOADING ||
            status === RunnerStatus.FAILED_EXECUTION ||
            status === RunnerStatus.READY
              ? 'Run'
              : 'Running...'
          }
        </Button>
        <Button onClick={abort} disabled={status !== RunnerStatus.RUNNING}>
          Stop
        </Button>
      </div>
      <div className="h-1/2 bg-black text-white overflow-auto">
        {stdout && <div className="p-2">{stdout}</div>}
        {stderr && <div className="p-2 text-red-500">{stderr}</div>}
        {error && <div className="p-2 text-red-500">Error: {error}</div>}
      </div>
    </div>
  );
}
