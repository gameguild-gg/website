'use client';

import { useState } from 'react';
import { Loader2, Play } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useWasmer } from './use-wasmer';

type CodeInterfaceProps = {
  initialCode: string;
};

export default function CodeInterface({ initialCode }: CodeInterfaceProps) {
  const { wasmerLoaded, wasmerLoading, running, packageLoaded, packageLoading, selectedPackage, setPackage, run } = useWasmer();

  const [code, setCode] = useState(initialCode);
  const [output, setOutput] = useState<string>('');
  const [error, setError] = useState<string>('');
  const [registryInput, setRegistryInput] = useState<string>('python/python');

  const handleRunCode = async () => {
    if (!wasmerLoaded || !packageLoaded) return;
    setOutput('');
    setError('');

    const args = ['-c', code];

    await run(args, (result) => {
      if (result.ok) {
        setOutput(result.stdout);
      } else {
        setError(result.stderr);
      }
    });
  };

  const handleLoadPackage = async () => {
    if (!registryInput.trim()) return;
    setPackage(registryInput);
  };

  return (
    <div className="lg:container mx-auto p-4 space-y-4">
      {/* Informações do estado */}
      <Card className="bg-[#2d2d2d] text-white p-4 font-mono text-xs">
        <p>⚡ Wasmer Loaded: {wasmerLoaded ? '✅ Yes' : '❌ No'}</p>
        <p>⏳ Loading: {wasmerLoading ? '⏳ Yes' : '❌ No'}</p>
        <p>📦 Package Loaded: {packageLoaded ? `✅ Yes (${selectedPackage})` : '❌ No'}</p>
        <p>🚀 Running: {running ? '🏃 Yes' : '❌ No'}</p>
        <p>📥 Package Loading: {packageLoading ? '⏳ Yes' : '❌ No'}</p>
      </Card>

      {/* Input para carregar Registry manualmente */}
      <div className="flex space-x-2">
        <input
          type="text"
          value={registryInput}
          onChange={(e) => setRegistryInput(e.target.value)}
          className="w-full bg-[#1e1e1e] text-white border border-gray-600 rounded p-2"
          placeholder="Enter Registry (e.g. python/python)"
        />
        <Button onClick={handleLoadPackage} disabled={packageLoading || !wasmerLoaded}>
          {packageLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Load'}
        </Button>
      </div>

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
        {wasmerLoading && <p>Loading Wasmer...</p>}
        {running && <p>Running Python...</p>}
        {error && <p className="text-red-400">Error: {error}</p>}
        <pre className="whitespace-pre-wrap">{output}</pre>
      </Card>

      {/* Botões */}
      <div className="flex justify-between">
        <Button
          variant="secondary"
          className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
          onClick={handleRunCode}
          disabled={!wasmerLoaded || !packageLoaded || wasmerLoading || running}
        >
          <Play className="w-4 h-4 mr-2" />
          {running ? 'Running...' : 'Run'}
        </Button>

        <Button className="bg-black text-white hover:bg-gray-900 rounded-full px-6">Finish lesson</Button>
      </div>
    </div>
  );
}
