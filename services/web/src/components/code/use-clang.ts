import { useCallback, useEffect, useRef, useState } from 'react';
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

  // Function to update the status asynchronously
  const updateStatus = useCallback(async () => {
    if (executorRef.current) {
      try {
        const currentStatus = await executorRef.current.getStatus();
        setStatus(currentStatus);
        return currentStatus;
      } catch (err) {
        console.error('Failed to get status:', err);
        return RunnerStatus.UNINITIALIZED;
      }
    }
    return RunnerStatus.UNINITIALIZED;
  }, []);

  const init = async (): Promise<void> => {
    if (workerRef.current && executorRef.current) {
      // If already initialized and ready, check status directly
      const currentStatus = await updateStatus();
      if (currentStatus === RunnerStatus.READY) {
        console.log('Worker already initialized and ready');
        return;
      }

      // If already initializing but not ready, wait for initialization to complete
      // by calling init() again - it will wait for the API to be ready
      console.log('Worker already initializing, waiting for ready status...');
      const status = await executorRef.current.init();
      // Update our status state with the worker's status
      setStatus(status);
      
      if (status === RunnerStatus.FAILED_LOADING) {
        throw new Error('Worker failed to initialize');
      }
      
      return;
    }

    try {
      // Create the worker
      console.log('before worker');
      workerRef.current = new Worker(new URL('./clang-worker.ts', import.meta.url), { type: 'module' });
      console.log('after worker');

      // Create the executor from the worker using Comlink
      executorRef.current = wrap<CodeExecutorBase>(workerRef.current);

      // Set up the callbacks (these are one-way communication and safe)
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

      // Update status to loading
      setStatus(RunnerStatus.LOADING);
      
      // Initialize the worker and wait for ready status
      console.log('Initializing worker...');
      const status = await executorRef.current.init();
      console.log('Worker initialization complete, status:', status);
      
      // Update our status state with the worker's status
      setStatus(status);
      
      if (status === RunnerStatus.FAILED_LOADING) {
        throw new Error('Worker failed to initialize');
      }
    } catch (err) {
      console.error('Failed to initialize Clang worker:', err);
      setError(`Failed to initialize Clang worker: ${err}`);
      setStatus(RunnerStatus.FAILED_LOADING);
      throw err;
    }
  };

  // Function to compile, link, and run C++ code
  const compileAndRun = useCallback(
    async (code: string, stdin?: string): Promise<CompileResult> => {
      console.log('compileAndRun called');
      
      // Initialize the worker if needed
      await init();
      console.log('after init');

      // Now the executor should be ready - double check
      if (!executorRef.current) {
        throw new Error('Clang executor not initialized');
      }
      
      // Make absolutely sure we're ready by checking the status
      const currentStatus = await updateStatus();
      if (currentStatus !== RunnerStatus.READY) {
        // If not ready, throw an error - no waiting
        throw new Error(`Executor is in ${currentStatus} state, not READY`);
      }

      // Create a new abort controller
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      abortControllerRef.current = new AbortController();

      try {
        // Clear previous output
        setStdout('');
        setStderr('');
        setError(null);
        
        // Update status to RUNNING
        setStatus(RunnerStatus.RUNNING);
        
        // Execute the code and wait for the result directly
        const result = await executorRef.current.run(code, {
          stdIn: stdin
        });
        
        // Update UI with the result data
        setStdout(result.stdout);
        setStderr(result.stderr);
        setStatus(result.status);
        
        return {
          stdout: result.stdout,
          stderr: result.stderr,
          success: result.success
        };
      } catch (err) {
        setError(`Execution error: ${err}`);
        setStatus(RunnerStatus.FAILED_EXECUTION);
        return {
          stdout: '',
          stderr: `Execution error: ${err}`,
          success: false,
        };
      }
    },
    [init, updateStatus],
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
    // Use the local status state
    status,
    stdout,
    stderr,
    error,
  };
}
