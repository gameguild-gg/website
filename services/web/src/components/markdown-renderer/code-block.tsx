'use client';

import { useEffect, useState } from 'react';
import { Play } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { CodeLanguage, FileMap, RunnerStatus } from '@/components/code/types';
import { useCode } from '@/components/code/use-code';

export type CodeInterfaceProps = {
  height?: number;
  language: CodeLanguage;
  data: FileMap | string;
};

export default function CodeBlock(params: CodeInterfaceProps) {
  const { data, height, language } = params;
  const { status, compileAndRun, error } = useCode();
  const [stdErr, setStdErr] = useState<string>('');
  const [stdOut, setStdOut] = useState<string>('');
  const [code, setCode] = useState<string>('');

  useEffect(() => {
    if (data && typeof data === 'string') setCode(data);
    else setCode('Multiple files are not supported yet');
  }, [data]);

  const handleRunCode = async () => {
    if (status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING) return;

    const result = await compileAndRun({
      data: { [`main.${language === 'python' ? 'py' : language}`]: code },
      language: language,
      stdin: '',
    });
    console.log(JSON.stringify(result));
    if (result.error) {
      setStdErr(result.error);
    }
    if (result.output) {
      setStdOut(result.output);
    }
  };

  return (
    <div className="container mx-auto p-4 space-y-4">
      {/* Editor de código */}
      <Card className="bg-[#1e1e1e] text-white p-4 font-mono text-sm">
        <Editor
          height={`${height || 200}px`}
          defaultLanguage={language}
          theme="vs-dark"
          value={code}
          onChange={(value) => setCode(value || '')}
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            lineNumbers: 'on',
            readOnly: false,
            domReadOnly: false,
            padding: { top: 0 },
            scrollBeyondLastLine: false,
            automaticLayout: true,
          }}
        />
      </Card>

      {/* Área de saída */}
      <Card className="bg-[#2d2d2d] text-white p-4 min-h-[150px] font-mono">
        {status === RunnerStatus.UNINITIALIZED && <p>Environment Uninitialized</p>}
        {status === RunnerStatus.LOADING && <p>Loading {params.language} environment...</p>}
        {status === RunnerStatus.RUNNING && <p>Running...</p>}
        {status === RunnerStatus.FAILED_EXECUTION && <p>Failed Execution</p>}
        {status === RunnerStatus.FAILED_LOADING && <p>Failed Loading Environment</p>}
        {error && <p className="text-red-400">Error: {error}</p>}
        <pre className="whitespace-pre-wrap">{stdOut}</pre>
      </Card>

      {/* Botões */}
      <div className="flex justify-between">
        <Button
          variant="secondary"
          className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
          onClick={handleRunCode}
          disabled={status === RunnerStatus.RUNNING || status === RunnerStatus.LOADING || status === RunnerStatus.FAILED_LOADING}
        >
          <Play className="w-4 h-4 mr-2" />
          {status === RunnerStatus.RUNNING ? 'Running...' : 'Run'}
        </Button>

        <Button className="bg-black text-white hover:bg-gray-900 rounded-full px-6">Finish lesson</Button>
      </div>
    </div>
  );
}
