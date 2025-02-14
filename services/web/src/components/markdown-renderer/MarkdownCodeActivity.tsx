import React, { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { CodeLanguage, useWasmer, WasmerStatus } from '@/components/wasmer/use-wasmer';
import { Card } from '@/components/ui/card';
import Editor from '@monaco-editor/react';
import { Play } from 'lucide-react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

export interface MarkdownCodeActivityProps {
  code: string;
  description: string;
  language: CodeLanguage;
  expectedOutput: string;
  stdin: string;
  height?: number;
}

export function MarkdownCodeActivity(params: MarkdownCodeActivityProps) {
  const { wasmerStatus, runCode, error } = useWasmer();
  const [stdErr, setStdErr] = useState<string>('');
  const [stdOut, setStdOut] = useState<string>('');
  const [code, setCode] = useState<string>('');
  const [isCorrect, setIsCorrect] = useState<boolean | null>(null);

  // set code only on first render
  useEffect(() => {
    setCode(params.code);
  }, []);

  const handleRunCode = async () => {
    if (wasmerStatus == WasmerStatus.RUNNING || wasmerStatus == WasmerStatus.LOADING_PACKAGE || wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER) return;

    const result = await runCode({
      data: code,
      language: params.language,
      stdin: params.stdin,
    });
    console.log(JSON.stringify(result));
    if (result.stderr) {
      setStdErr(result.stderr);
    }
    if (result.stdout) {
      setStdOut(result.stdout);
    }

    // compare stdout with expected output without whitespaces(tab, spaces, newlines, carriage returns)
    if (params.expectedOutput && params.expectedOutput.replace(/\s/g, '') !== result.stdout?.replace(/\s/g, '')) {
      setIsCorrect(false);
    } else setIsCorrect(true);
  };

  return (
    <div className="container mx-auto p-4 space-y-4">
      <MarkdownRenderer content={params.description} />
      {/* Editor de código */}
      <Card className="bg-[#1e1e1e] text-white p-4 font-mono text-sm">
        <Editor
          height={`${params.height || 200}px`}
          defaultLanguage={params.language}
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

      {isCorrect === true && <div className={`p-2 rounded bg-red-100 text-green-800`}>Correct output!</div>}

      {/* Área de saída */}
      {wasmerStatus != WasmerStatus.UNINITIALIZED && !(wasmerStatus == WasmerStatus.READY_TO_RUN && isCorrect) && (
        <>
          <Card className="bg-[#2d2d2d] text-white p-4 min-h-[150px] font-mono">
            {wasmerStatus == WasmerStatus.LOADING_WASMER && <p>Loading Wasmer...</p>}
            {wasmerStatus == WasmerStatus.LOADING_PACKAGE && <p>Loading Python...</p>}
            {wasmerStatus == WasmerStatus.RUNNING && <p>Running...</p>}
            {wasmerStatus == WasmerStatus.FAILED_EXECUTION && <p>Failed Execution</p>}
            {wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER && <p>Failed Loading Wasmer</p>}
            {wasmerStatus == WasmerStatus.FAILED_LOADING_PACKAGE && <p>Failed Loading Package</p>}
            {error && <p className="text-red-400">Error: {error}</p>}
            {isCorrect === false && (
              <>
                <p className="text-red-400">Expected: {params.expectedOutput}</p>
                <p className="text-red-400">Your Output: {stdOut}</p>
              </>
            )}
          </Card>
        </>
      )}

      {/* Botões */}
      {(isCorrect === false || isCorrect === null) && (
        <div className="flex justify-between">
          <Button
            variant="secondary"
            className="bg-[#2d2d2d] text-white hover:bg-[#3d3d3d]"
            onClick={handleRunCode}
            disabled={
              wasmerStatus == WasmerStatus.RUNNING || wasmerStatus == WasmerStatus.LOADING_PACKAGE || wasmerStatus == WasmerStatus.FAILED_LOADING_WASMER
            }
          >
            <Play className="w-4 h-4 mr-2" />
            {wasmerStatus == WasmerStatus.RUNNING ? 'Running...' : 'Run'}
          </Button>
        </div>
      )}
    </div>
  );
}
