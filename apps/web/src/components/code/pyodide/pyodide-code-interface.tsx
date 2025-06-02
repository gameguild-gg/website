'use client';

import { useState } from 'react';
import { Play } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useCode } from '../use-code';
import { RunnerStatus } from '../types';

interface CodeInterfaceProps {
  initialCode: string;
}

export default function PyodideCodeInterface({ initialCode }: CodeInterfaceProps) {
  const { compileAndRun, output, error, status } = useCode();
  const [code, setCode] = useState(initialCode);

  const handleRunCode = async () => {
    await compileAndRun({
      language: 'python',
      data: { 'main.py': code },
    });
  };

  return (
    <div className="container mx-auto p-4 space-y-4">
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

      <Card className="bg-[#2d2d2d] text-white p-4 min-h-[150px] font-mono">
        {status === RunnerStatus.LOADING && <p>Loading Python runtime...</p>}
        {status === RunnerStatus.RUNNING && <p>Running...</p>}
        {error && <p>Error: {error}</p>}
        <pre className="whitespace-pre-wrap">{output}</pre>
      </Card>

      <div className="flex justify-between">
        <Button
          variant="secondary"
          className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
          onClick={handleRunCode}
          disabled={status === RunnerStatus.LOADING || status === RunnerStatus.RUNNING}
        >
          <Play className="w-4 h-4 mr-2" />
          Run
        </Button>

        <Button className="bg-black text-white hover:bg-gray-900 rounded-full px-6">Finish lesson</Button>
      </div>
    </div>
  );
}
