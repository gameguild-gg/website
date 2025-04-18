import { useCallback, useEffect, useRef, useState } from 'react';
import { proxy, wrap, Remote } from 'comlink';
import { CodeExecutorBase } from './code-executor.base';
import { RunnerStatus } from './code-executor.types';

type CompileResult = {
  stdout: string;
  success: boolean;
};

export function useClang() {
  const [status, setStatus] = useState<RunnerStatus>(RunnerStatus.UNINITIALIZED);
  // Stage-specific outputs - only stdout
  const [initOutput, setInitOutput] = useState('');
  const [compilerOutput, setCompilerOutput] = useState('');
  const [linkerOutput, setLinkerOutput] = useState('');
  const [executionOutput, setExecutionOutput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const workerRef = useRef<Worker | null>(null);
  const executorRef = useRef<Remote<CodeExecutorBase> | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const currentStageRef = useRef<'init' | 'compile' | 'link' | 'execute'>('init');
  const currentOutputRef = useRef<string>('');

  // Helper to get the appropriate output setter based on current stage
  const getOutputSetter = (stage: typeof currentStageRef.current) => {
    switch (stage) {
      case 'init':
        return setInitOutput;
      case 'compile':
        return setCompilerOutput;
      case 'link':
        return setLinkerOutput;
      case 'execute':
        return setExecutionOutput;
    }
  };

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
      const currentStatus = await updateStatus();
      if (currentStatus === RunnerStatus.READY) {
        return;
      }

      const status = await executorRef.current.init();
      setStatus(status);
      
      if (status === RunnerStatus.FAILED_LOADING) {
        throw new Error('Worker failed to initialize');
      }
      
      return;
    }

    try {
      currentStageRef.current = 'init';
      workerRef.current = new Worker(new URL('./clang-worker.ts', import.meta.url), { type: 'module' });
      executorRef.current = wrap<CodeExecutorBase>(workerRef.current);

      // Set up stdout callback
      executorRef.current.setOnStdOut(
        proxy((data: string) => {
          currentOutputRef.current += data;
          const setter = getOutputSetter(currentStageRef.current);
          setter(currentOutputRef.current);
        }),
      );

      // Initialize empty callbacks for stderr and error since we don't use them
      executorRef.current.setOnStdErr(proxy(() => {}));
      executorRef.current.setOnError(proxy(() => {}));

      setStatus(RunnerStatus.LOADING);
      
      const status = await executorRef.current.init();
      setStatus(status);
      
      if (status === RunnerStatus.FAILED_LOADING) {
        throw new Error('Worker failed to initialize');
      }
    } catch (err) {
      setError(`Failed to initialize Clang worker: ${err}`);
      setStatus(RunnerStatus.FAILED_LOADING);
      throw err;
    }
  };

  const compileAndRun = useCallback(
    async (code: string, stdin?: string): Promise<CompileResult> => {
      try {
        // Start with initialization
        currentStageRef.current = 'init';
        currentOutputRef.current = '';
        setInitOutput('');
        setCompilerOutput('');
        setLinkerOutput('');
        setExecutionOutput('');
        setError(null);

        await init();

        if (!executorRef.current) {
          throw new Error('Clang executor not initialized');
        }
        
        const currentStatus = await updateStatus();
        if (currentStatus !== RunnerStatus.READY) {
          throw new Error(`Executor is in ${currentStatus} state, not READY`);
        }

        if (abortControllerRef.current) {
          abortControllerRef.current.abort();
        }
        abortControllerRef.current = new AbortController();
        
        setStatus(RunnerStatus.RUNNING);
        
        // Move to compilation stage
        currentStageRef.current = 'compile';
        currentOutputRef.current = '';

        const result = await executorRef.current.run(code, { stdIn: stdin });
        
        setStatus(result.status);
        
        return {
          stdout: result.stdout,
          success: result.success
        };
      } catch (err) {
        setError(`Execution error: ${err}`);
        setStatus(RunnerStatus.FAILED_EXECUTION);
        return {
          stdout: `Execution error: ${err}`,
          success: false,
        };
      }
    },
    [init, updateStatus],
  );

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
    initOutput,
    compilerOutput,
    linkerOutput,
    executionOutput,
    error,
  };
}
