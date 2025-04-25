import { useCallback, useRef, useState } from 'react';
import { proxy, Remote, wrap } from 'comlink';
import { CodeExecutorBase } from './code-executor.base';
import { RunnerStatus } from './types';

type CompileResult = {
  stdout: string;
  success: boolean;
};

export function useClang() {
  const [status, setStatus] = useState<RunnerStatus>(RunnerStatus.UNINITIALIZED);
  const [initOutput, setInitOutput] = useState('');
  const [compilerOutput, setCompilerOutput] = useState('');
  const [linkerOutput, setLinkerOutput] = useState('');
  const [executionOutput, setExecutionOutput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const workerRef = useRef<Worker | null>(null);
  const executorRef = useRef<Remote<CodeExecutorBase> | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const currentStageRef = useRef<'init' | 'compile' | 'link' | 'execute'>('init');

  const appendToOutput = (stage: typeof currentStageRef.current, output: string) => {
    if (!output) return;

    switch (stage) {
      case 'init':
        setInitOutput((prev) => prev + output);
        break;
      case 'compile':
        setCompilerOutput((prev) => prev + output);
        break;
      case 'link':
        setLinkerOutput((prev) => prev + output);
        break;
      case 'execute':
        setExecutionOutput((prev) => prev + output);
        break;
    }
  };

  const processMessage = (data: string) => {
    try {
      const parsed = JSON.parse(data);
      if (parsed.type === 'output' && Array.isArray(parsed.stages)) {
        parsed.stages.forEach(({ stage, output }) => {
          if (output) {
            appendToOutput(stage, output);
          }
        });
      }
    } catch (err) {
      // If not JSON or invalid format, append to current stage as-is
      if (data) {
        appendToOutput(currentStageRef.current, data);
      }
    }
  };

  const setupStdoutCallback = (executor: Remote<CodeExecutorBase>) => {
    executor.setOnStdOut(
      proxy((data: string) => {
        processMessage(data);
      }),
    );
  };

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
      workerRef.current = new Worker(new URL('./clang/worker.ts', import.meta.url), { type: 'module' });
      executorRef.current = wrap<CodeExecutorBase>(workerRef.current);

      setupStdoutCallback(executorRef.current);
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
        currentStageRef.current = 'init';
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
        currentStageRef.current = 'compile';

        const result = await executorRef.current.run(code, { stdIn: stdin });
        setStatus(result.status);

        return {
          stdout: '', // We're not using the returned stdout anymore
          success: result.success,
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
