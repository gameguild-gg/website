'use client';

import { useState } from 'react';
import { Play } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useWasmer, WasmerPackage, WasmerRunParams, WasmerStatus } from '@/components/wasmer/use-wasmer';

type CodeInterfaceProps = {
  initialCode: string;
};

export default function CodeInterface({ initialCode }: CodeInterfaceProps) {
  const { wasmerStatus, run, error } = useWasmer();
  const [stdErr, setStdErr] = useState<string>('');
  const [stdOut, setStdOut] = useState<string>('');

  const [code, setCode] = useState(initialCode);
  const [registryInput, setRegistryInput] = useState<WasmerPackage>(WasmerPackage.python);

  const handleRunCode = async () => {
    if (wasmerStatus == WasmerStatus.RUNNING || wasmerStatus == WasmerStatus.LOADING_PACKAGE || wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER) return;
    const args = ['-c', code];

    const params: WasmerRunParams = {
      package: registryInput,
      args,
      onComplete: (result) => {
        if (result.ok) {
          setStdOut(result.stdout);
        } else {
          setStdErr(result.stderr);
        }
      },
    };

    await run(params);
  };

  return (
    <div className="lg:container mx-auto p-4 space-y-4">
      {/* Editor de código */}
      <Card className="bg-[#1e1e1e] text-white p-4 font-mono text-sm">
        <Editor
          height="200px"
          defaultLanguage="python"
          theme="vs-dark"
          value={code}
          onChange={(value) => setCode(value || '')}
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            lineNumbers: 'on',
            readOnly: false,
            domReadOnly: false,
            padding: { top: 10 },
            scrollBeyondLastLine: false,
          }}
        />
      </Card>

      {/* Área de saída */}
      <Card className="bg-[#2d2d2d] text-white p-4 min-h-[150px] font-mono">
        {wasmerStatus == WasmerStatus.UNINITIALIZED && <p>Wasmer Uninitialized</p>}
        {wasmerStatus == WasmerStatus.LOADING_WASMER && <p>Loading Wasmer...</p>}
        {wasmerStatus == WasmerStatus.LOADING_PACKAGE && <p>Loading Python...</p>}
        {wasmerStatus == WasmerStatus.RUNNING && <p>Running...</p>}
        {wasmerStatus == WasmerStatus.FAILED_EXECUTION && <p>Failed Execution</p>}
        {wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER && <p>Failed Loading Wasmer</p>}
        {wasmerStatus == WasmerStatus.FAILED_LOADING_PACKAGE && <p>Failed Loading Package</p>}
        {error && <p className="text-red-400">Error: {error}</p>}
        <pre className="whitespace-pre-wrap">{stdOut}</pre>
      </Card>

      {/* Botões */}
      <div className="flex justify-between">
        <Button
          variant="secondary"
          className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
          onClick={handleRunCode}
          disabled={wasmerStatus == WasmerStatus.RUNNING || wasmerStatus == WasmerStatus.LOADING_PACKAGE || wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER}
        >
          <Play className="w-4 h-4 mr-2" />
          {wasmerStatus == WasmerStatus.RUNNING ? 'Running...' : 'Run'}
        </Button>

        <Button className="bg-black text-white hover:bg-gray-900 rounded-full px-6">Finish lesson</Button>
      </div>
    </div>
  );
}
