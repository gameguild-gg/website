'use client';

import { useState } from 'react';
import { Play } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { usePyodide } from './use-pyodite';

interface CodeInterfaceProps {
  initialCode: string;
}

export default function CodeInterface({ initialCode }: CodeInterfaceProps) {
  const { pyodideLoaded, loading, error, runPython, output } = usePyodide();
  const [code, setCode] = useState(initialCode);

  const handleRunCode = () => {
    if (pyodideLoaded) {
      runPython(code);
    }
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
        {loading && <p>Loading Pyodide...</p>}
        {error && <p>Error loading Pyodide: {error}</p>}
        <pre className="whitespace-pre-wrap">{output}</pre>
      </Card>

      <div className="flex justify-between">
        <Button
          variant="secondary"
          className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
          onClick={handleRunCode}
          disabled={!pyodideLoaded || loading}
        >
          <Play className="w-4 h-4 mr-2" />
          Run
        </Button>

        <Button className="bg-black text-white hover:bg-gray-900 rounded-full px-6">
          Finish lesson
        </Button>
      </div>
    </div>
  );
}
