import { useCallback, useRef, useState, useEffect } from 'react';
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
  // Track the current status in a ref that we can check directly
  const statusRef = useRef<RunnerStatus>(status);
  
  // Keep the ref updated when status changes
  useEffect(() => {
    statusRef.current = status;
  }, [status]);

  const init = async (): Promise<void> => {
    
    if (workerRef.current && executorRef.current) {
      // If already initialized and ready, return immediately
      if (statusRef.current === RunnerStatus.READY) {
        console.log('Worker already initialized and ready');
        return;
      }
      
      // If already initialized but not ready, wait for it to be ready
      console.log('Worker already initializing, waiting for ready status...');
      await new Promise<void>((resolve, reject) => {
        const maxRetries = 100; // 10 seconds maximum wait time
        let retries = 0;
        
        const checkStatus = () => {
          // Just check the local ref status which is always updated
          if (statusRef.current === RunnerStatus.READY) {
            console.log('Worker is now ready');
            resolve();
          } else if (statusRef.current === RunnerStatus.FAILED_LOADING) {
            console.error('Worker failed to initialize');
            reject(new Error('Worker failed to initialize'));
          } else if (retries >= maxRetries) {
            console.error('Timed out waiting for worker to initialize');
            // Don't throw an error, just resolve and try to recover
            // We'll recreate the worker if needed
            resolve();
          } else {
            retries++;
            setTimeout(checkStatus, 100);
          }
        };
        
        checkStatus();
      });
      return;
    }
    
    try {
      // Create the worker
      console.log('before worker');
      workerRef.current = new Worker(new URL('./clang-worker.ts', import.meta.url), { type: 'module' });
      console.log('after worker');
      
      // Create a promise to track when status becomes READY
      const readyPromise = new Promise<void>((resolve, reject) => {
        let cleanupTimeout: NodeJS.Timeout | null = null;
        
        // Set timeout for initialization - but make it longer
        const timeoutId = setTimeout(() => {
          console.warn('Worker initialization took longer than expected, but we\'ll keep waiting');
          // Don't reject, just log a warning
        }, 15000); // 15 second warning
        
        const statusChangeHandler = (event: MessageEvent) => {
          if (event.data && event.data.type === 'status') {
            console.log('Status message from worker:', event.data.status);
            if (event.data.status === RunnerStatus.READY) {
              setStatus(RunnerStatus.READY);
              statusRef.current = RunnerStatus.READY;
              
              // Clean up event listener but only after a delay to ensure we get all messages
              if (cleanupTimeout) clearTimeout(cleanupTimeout);
              cleanupTimeout = setTimeout(() => {
                workerRef.current?.removeEventListener('message', statusChangeHandler);
              }, 500);
              
              clearTimeout(timeoutId);
              resolve();
            } else if (event.data.status === RunnerStatus.FAILED_LOADING) {
              workerRef.current?.removeEventListener('message', statusChangeHandler);
              clearTimeout(timeoutId);
              if (cleanupTimeout) clearTimeout(cleanupTimeout);
              reject(new Error('Worker failed to initialize'));
            }
          }
        };
        
        workerRef.current?.addEventListener('message', statusChangeHandler);
      });
      
      // Set up direct message event listener for stdout/stderr from the worker
      workerRef.current.addEventListener('message', (event) => {
        if (event.data && event.data.type === 'stdout') {
          setStdout((prev) => prev + event.data.data);
        } else if (event.data && event.data.type === 'stderr') {
          setStderr((prev) => prev + event.data.data);
        } else if (event.data && event.data.type === 'status') {
          // Update the React state status
          setStatus(event.data.status);
          // Also update our local ref for immediate access
          statusRef.current = event.data.status;
        }
      });
      
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

      // Initialize the executor - this will trigger the status changes
      await executorRef.current.init(
        proxy((newStatus: RunnerStatus) => {
          console.log('Worker status changed to:', newStatus);
          setStatus(newStatus);
          statusRef.current = newStatus;
          
          // Also send a direct message for our event listener to catch
          workerRef.current?.postMessage({
            type: 'status',
            status: newStatus
          });
        }),
      );
      
      // Wait for the ready promise to resolve
      await readyPromise;
      
    } catch (err) {
      console.error('Failed to initialize Clang worker:', err);
      setError(`Failed to initialize Clang worker: ${err}`);
      setStatus(RunnerStatus.FAILED_LOADING);
      statusRef.current = RunnerStatus.FAILED_LOADING;
      throw err;
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
        // Register a one-time message handler for status changes from direct worker messages
        const statusHandler = (event: MessageEvent) => {
          if (event.data && event.data.type === 'runStatus') {
            setStatus(event.data.status);
          }
        };
        workerRef.current?.addEventListener('message', statusHandler);
        
        // Send the run command without passing callbacks directly
        // The worker will use direct postMessage for status updates
        await executorRef.current.run(code, {
          stdIn: stdin,
        });
        
        // Clean up the handler
        workerRef.current?.removeEventListener('message', statusHandler);

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
