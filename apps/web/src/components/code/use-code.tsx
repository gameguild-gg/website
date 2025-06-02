'use client';

import { useCallback, useEffect, useState } from 'react';
import { useClang } from '@/components/code/clang/use-clang';
import { usePyodide } from '@/components/code/pyodide/use-pyodide';
import {
  CodingTestEnum,
  CodingTestParamsWithLanguage,
  CompileAndRunParams,
  FileMap,
  RunnerStatus,
  RunResult,
  SimpleCodingTests,
} from '@/components/code/types';

// Define structure for output stages to better organize output from different engines
export type StageOutput = {
  init: string;
  compile: string;
  link: string;
  execute: string;
};

export function useCode() {
  const {
    init: initClang,
    compileAndRun: compileAndRunClang,
    status: clangStatus,
    initOutput: clangInitOutput,
    compilerOutput: clangCompilerOutput, 
    linkerOutput: clangLinkerOutput,
    executionOutput: clangExecutionOutput,
    error: clangError,
    abort: abortClang,
  } = useClang();

  const { pyodideLoaded, loading: pyodideLoading, runPython, output: pyodideOutput, error: pyodideError, initPyodide, cleanup: cleanupPyodide } = usePyodide();

  const [status, setStatus] = useState<RunnerStatus>(RunnerStatus.UNINITIALIZED);
  const [output, setOutput] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  
  // Add separate output stages
  const [initOutput, setInitOutput] = useState<string>('');
  const [compileOutput, setCompileOutput] = useState<string>('');
  const [linkOutput, setLinkOutput] = useState<string>('');
  const [executeOutput, setExecuteOutput] = useState<string>('');

  // Update status based on underlying engines
  useEffect(() => {
    if (clangStatus === RunnerStatus.RUNNING) {
      setStatus(RunnerStatus.RUNNING);
    } else if (pyodideLoading) {
      setStatus(RunnerStatus.LOADING);
    } else if (clangStatus === RunnerStatus.READY && (!pyodideLoaded || pyodideLoaded)) {
      // We consider the combined system ready if Clang is ready
      // Pyodide will be lazy-loaded when needed
      setStatus(RunnerStatus.READY);
    } else if (clangStatus === RunnerStatus.FAILED_LOADING || pyodideError) {
      setStatus(RunnerStatus.FAILED_LOADING);
    }
  }, [clangStatus, pyodideLoaded, pyodideLoading, pyodideError]);

  // Update output and error states from underlying engines
  useEffect(() => {
    // For combined output
    if (clangExecutionOutput) {
      setOutput(
        [clangInitOutput, clangCompilerOutput, clangLinkerOutput, clangExecutionOutput]
          .filter(Boolean)
          .join('\n---\n')
      );
    }
    // For separate output stages
    setInitOutput(clangInitOutput);
    setCompileOutput(clangCompilerOutput);
    setLinkOutput(clangLinkerOutput);
    setExecuteOutput(clangExecutionOutput);
  }, [clangInitOutput, clangCompilerOutput, clangLinkerOutput, clangExecutionOutput]);

  useEffect(() => {
    if (pyodideOutput) {
      setOutput(pyodideOutput);
      // For Python, all output is considered execution output
      setExecuteOutput(pyodideOutput);
    }
  }, [pyodideOutput]);

  useEffect(() => {
    setError(clangError || pyodideError || null);
  }, [clangError, pyodideError]);

  // Initialize both runtimes
  const init = useCallback(async () => {
    try {
      setStatus(RunnerStatus.LOADING);
      await initClang();
      // Pyodide will be lazy-loaded when needed
      setStatus(RunnerStatus.READY);
      return true;
    } catch (err) {
      setError(`Failed to initialize code execution environments: ${err}`);
      setStatus(RunnerStatus.FAILED_LOADING);
      return false;
    }
  }, [initClang]);

  // Utility to extract code from FileMap
  const extractCodeFromFileMap = (data: FileMap, preferredExtensions?: string[]): string => {
    // Look for main files or the first relevant file
    if (preferredExtensions) {
      for (const ext of preferredExtensions) {
        const mainFile = Object.entries(data).find(([key]) => key.endsWith(ext) && key.includes('main'));
        if (mainFile && typeof mainFile[1] === 'string') {
          return mainFile[1];
        }
      }
      
      // If no main file, just look for any file with the right extension
      for (const ext of preferredExtensions) {
        const anyFile = Object.entries(data).find(([key]) => key.endsWith(ext));
        if (anyFile && typeof anyFile[1] === 'string') {
          return anyFile[1];
        }
      }
    }

    // Fallback to any string
    for (const [key, value] of Object.entries(data)) {
      if (typeof value === 'string') {
        return value;
      } else if (typeof value === 'object' && !(value instanceof Uint8Array)) {
        // Recursive search in nested file maps
        const nestedCode = extractCodeFromFileMap(value as FileMap);
        if (nestedCode) return nestedCode;
      }
    }

    return '';
  };

  // Unified function to compile and run code
  const compileAndRun = useCallback(
    async (params: CompileAndRunParams): Promise<RunResult> => {
      const { language, data, stdin } = params;

      // Reset all outputs
      setOutput('');
      setError(null);
      setInitOutput('');
      setCompileOutput('');
      setLinkOutput('');
      setExecuteOutput('');
      
      setStatus(RunnerStatus.RUNNING);

      try {
        let preferredExtensions: string[] = [];

        // Set preferred extensions by language
        switch (language) {
          case 'c':
            preferredExtensions = ['.c'];
            break;
          case 'cpp':
            preferredExtensions = ['.cpp', '.cc', '.cxx'];
            break;
          case 'python':
            preferredExtensions = ['.py'];
            break;
          default:
            break;
        }

        const code = extractCodeFromFileMap(data, preferredExtensions);

        if (!code) {
          throw new Error('No code found in provided data');
        }

        let success = false;
        let outputValue = '';
        let errorValue = null;

        if (language === 'python') {
          await runPython(code);
          // Consider successful unless there's an error
          success = !pyodideError;
          outputValue = pyodideOutput;
          errorValue = pyodideError;
        } else if (language === 'c' || language === 'cpp') {
          const result = await compileAndRunClang(code, stdin);
          success = result.success;
          // Combined output is already set via effects
          outputValue = [clangInitOutput, clangCompilerOutput, clangLinkerOutput, clangExecutionOutput]
            .filter(Boolean)
            .join('\n---\n');
          errorValue = clangError;
        } else {
          throw new Error(`Language '${language}' not yet supported`);
        }

        setStatus(success ? RunnerStatus.READY : RunnerStatus.FAILED_EXECUTION);

        return {
          success,
          output: outputValue,
          error: errorValue,
        };
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : String(err);
        setError(errorMessage);
        setStatus(RunnerStatus.FAILED_EXECUTION);

        return {
          success: false,
          output: '',
          error: errorMessage,
        };
      }
    },
    [
      compileAndRunClang, 
      runPython, 
      pyodideError, 
      pyodideOutput, 
      clangInitOutput,
      clangCompilerOutput,
      clangLinkerOutput,
      clangExecutionOutput,
      clangError
    ],
  );

  // More complex function to run code challenges with tests
  const runChallenge = useCallback(
    async (options: CodingTestParamsWithLanguage): Promise<RunResult> => {
      const { language, files, tests } = options;

      // Convert string to FileMap if necessary
      const fileMap: FileMap = typeof files === 'string' ? { [`main.${language === 'python' ? 'py' : language === 'cpp' ? 'cpp' : 'c'}`]: files } : files;

      // If no tests, just run the code
      if (!tests) {
        return await compileAndRun({
          language,
          data: fileMap,
          stdin: undefined,
        });
      }

      // Handle simple console tests
      if (tests.type && tests.type === CodingTestEnum.CONSOLE) {
        const consoleTests = tests as SimpleCodingTests;
        const publicResults = [];

        // Run public tests
        for (const test of consoleTests.publicTests) {
          const result = await compileAndRun({
            language,
            data: fileMap,
            stdin: test.stdin,
          });

          // Check if output matches expected
          const success = result.output.trim() === test.stdout.trim();
          publicResults.push({
            ...result,
            success,
            expected: test.stdout,
          });
        }

        // For now, just return the first result
        // A more complete implementation would run all tests and aggregate results
        return publicResults.length > 0
          ? publicResults[0]
          : {
              success: false,
              output: 'No tests were executed',
              error: null,
            };
      }

      // Function and custom tests would be implemented here
      // But for now, just run the code without evaluating the tests
      return await compileAndRun({
        language,
        data: fileMap,
        stdin: undefined,
      });
    },
    [compileAndRun],
  );

  // Cleanup function to abort running processes
  const abort = useCallback(() => {
    abortClang();
  }, [abortClang]);

  // Cleanup all resources
  const cleanup = useCallback(() => {
    abort();
    cleanupPyodide();
  }, [abort, cleanupPyodide]);

  return {
    init,
    compileAndRun,
    runChallenge,
    abort,
    cleanup,
    status,
    output,
    error,
    // Expose detailed outputs for UI that needs them
    initOutput,
    compileOutput,
    linkOutput,
    executeOutput,
  };
}
