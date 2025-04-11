import { useCallback, useRef, useState } from 'react';
import { proxy, wrap } from 'comlink';
import { CodeExecutorBase } from './code-executor.base';
import { RunnerStatus } from './code-executor.types';

type CompileResult = {
  stdout: string;
  stderr: string;
  success: boolean;
};

export function useClang() {
  const [status, setStatus] = useState<RunnerStatus>(RunnerStatus.UNINITIALIZED);
  const [stdout, setStdout] = useState<string>('');
  const [stderr, setStderr] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const workerRef = useRef<Worker | null>(null);
  const executorRef = useRef<CodeExecutorBase | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const init = async (): Promise<void> => {
    if (workerRef.current) return; // Already initialized
    try {
      // Create the worker
      console.log('before worker');
      workerRef.current = new Worker(new URL('./clang-worker.ts', import.meta.url), { type: 'module' });
      console.log('after worker');

      // Create the executor from the worker using Comlink
      executorRef.current = wrap<CodeExecutorBase>(workerRef.current);

      // Set up the callbacks
      executorRef.current.setOnStdOut(
        proxy((data: string) => {
          setStdout((prev) => prev + data);
        }),
      );

      executorRef.current.setOnStdErr(
        proxy((data: string) => {
          setStderr((prev) => prev + data);
        }),
      );

      executorRef.current.setOnError(
        proxy((data: string) => {
          setError(data);
        }),
      );

      // Initialize the executor
      await executorRef.current.init(
        proxy((newStatus: RunnerStatus) => {
          setStatus(newStatus);
        }),
      );
    } catch (err) {
      setError(`Failed to initialize Clang worker: ${err}`);
      setStatus(RunnerStatus.FAILED_LOADING);
    }
  };

  // Function to compile, link, and run C++ code
  const compileAndRun = useCallback(
    async (code: string, stdin?: string): Promise<CompileResult> => {
      console.log('compileAndRun called');
      await init(); // Ensure the worker is initialized
      console.log('after init');

      if (!executorRef.current || status !== RunnerStatus.READY) {
        throw new Error('Clang executor not initialized');
      }

      // Clear previous output
      setStdout('');
      setStderr('');
      setError(null);

      // Create a new abort controller
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      abortControllerRef.current = new AbortController();

      try {
        // Run the code
        await executorRef.current.run(code, {
          stdIn: stdin,
          onStatusChange: (status) => {
            setStatus(status);
          },
          abort: () => {
            if (abortControllerRef.current) {
              abortControllerRef.current.abort();
            }
          },
        });

        return {
          stdout,
          stderr,
          success: true,
        };
      } catch (err) {
        setError(`Execution error: ${err}`);
        return {
          stdout,
          stderr: stderr || `Execution error: ${err}`,
          success: false,
        };
      }
    },
    [status, stdout, stderr],
  );

  // Function to abort the current execution
  const abort = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
  }, []);

  return {
    init,
    compileAndRun,
    abort,
    status,
    stdout,
    stderr,
    error,
  };
}
