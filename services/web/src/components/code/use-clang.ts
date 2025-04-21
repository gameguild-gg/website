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

  // Helper to append output to the appropriate stage
  const appendToOutput = (stage: typeof currentStageRef.current, output: string) => {
    const cleanOutput = output.trim();
    if (!cleanOutput) return;
    
    switch (stage) {
      case 'init':
        setInitOutput(prev => prev + cleanOutput + '\n');
        break;
      case 'compile':
        setCompilerOutput(prev => prev + cleanOutput + '\n');
        break;
      case 'link':
        setLinkerOutput(prev => prev + cleanOutput + '\n');
        break;
      case 'execute':
        setExecutionOutput(prev => prev + cleanOutput);
        break;
    }
  };

  // Process output message
  const processMessage = (data: string) => {
    // Try to parse as JSON first
    try {
      const parsed = JSON.parse(data);
      if (parsed.type === 'output' && Array.isArray(parsed.stages)) {
        parsed.stages.forEach(({ stage, output }) => {
          appendToOutput(stage, output);
        });
        return;
      }
    } catch (err) {
      // Not JSON, process as regular output
    }

    // Check for stage markers
    const stageMatch = data.match(/^\[(\w+)\]/);
    if (stageMatch) {
      const stage = stageMatch[1].toLowerCase() as typeof currentStageRef.current;
      appendToOutput(stage, data);
    } else {
      // If no stage marker, append to current stage
      appendToOutput(currentStageRef.current, data);
    }
  };

  // Set up stdout callback
  const setupStdoutCallback = (executor: Remote<CodeExecutorBase>) => {
    executor.setOnStdOut(
      proxy((data: string) => {
        processMessage(data);
      }),
    );
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

      setupStdoutCallback(executorRef.current);

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

        // Parse the JSON output
        try {
          const parsed = JSON.parse(result.stdout);
          if (parsed.type === 'output' && Array.isArray(parsed.stages)) {
            parsed.stages.forEach(({ stage, output }) => {
              switch (stage) {
                case 'init':
                  setInitOutput(output);
                  break;
                case 'compile':
                  setCompilerOutput(output);
                  break;
                case 'link':
                  setLinkerOutput(output);
                  break;
                case 'execute':
                  if (output.startsWith('Error: ')) {
                    setError(output);
                  } else {
                    setExecutionOutput(output);
                  }
                  break;
              }
            });
          }
        } catch (err) {
          console.error('Failed to parse output:', err);
          setError('Failed to parse execution output');
        }
        
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
