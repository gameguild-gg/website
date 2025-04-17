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

      // If already initializing but not ready, use event-based approach to wait
      console.log('Worker already initializing, waiting for ready status...');
      await new Promise<void>((resolve, reject) => {
        // One-time event listener for the READY state
        const readyHandler = (event: MessageEvent) => {
          if (event.data && event.data.type === 'status' && event.data.status === RunnerStatus.READY) {
            console.log('Worker is now ready (from event)');
            workerRef.current?.removeEventListener('message', readyHandler);
            resolve();
          } else if (event.data && event.data.type === 'status' && event.data.status === RunnerStatus.FAILED_LOADING) {
            console.error('Worker failed to initialize (from event)');
            workerRef.current?.removeEventListener('message', readyHandler);
            reject(new Error('Worker failed to initialize'));
          }
        };
        
        // Add listener for status changes
        workerRef.current?.addEventListener('message', readyHandler);
        
        // Check current status - may already be ready
        if (statusRef.current === RunnerStatus.READY) {
          console.log('Worker is already ready (from status check)');
          workerRef.current?.removeEventListener('message', readyHandler);
          resolve();
        } else if (statusRef.current === RunnerStatus.FAILED_LOADING) {
          console.error('Worker already failed to initialize (from status check)');
          workerRef.current?.removeEventListener('message', readyHandler);
          reject(new Error('Worker failed to initialize'));
        }
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
        // Create a direct handler for status change events
        const statusChangeHandler = (event: MessageEvent) => {
          if (event.data && event.data.type === 'status') {
            console.log('Status message from worker:', event.data.status);
            if (event.data.status === RunnerStatus.READY) {
              // Update our statuses
              setStatus(RunnerStatus.READY);
              statusRef.current = RunnerStatus.READY;
              
              // Clean up event listener
              workerRef.current?.removeEventListener('message', statusChangeHandler);
              
              console.log("Worker READY, resolving initialization promise immediately");
              // Resolve immediately - no timeouts
              resolve();
            } else if (event.data.status === RunnerStatus.FAILED_LOADING) {
              // Clean up the event listener
              workerRef.current?.removeEventListener('message', statusChangeHandler);
              
              // Reject with an error
              reject(new Error('Worker failed to initialize'));
            }
          }
        };

        // IMPORTANT: First check if we're already in READY state before setting up the listener
        // This is critical to prevent race conditions
        if (statusRef.current === RunnerStatus.READY) {
          console.log("Worker already READY, resolving immediately");
          resolve();
          return; // Exit early without adding the listener
        }
        
        // Only add the listener if we're not already ready
        workerRef.current?.addEventListener('message', statusChangeHandler);
      });

      console.log('Setting up main message listener');
      
      // Set up direct message event listener for stdout/stderr from the worker
      // Use variable to ensure we can access it in the handler
      const messageListener = (event: MessageEvent) => {
        if (event.data && event.data.type === 'stdout') {
          setStdout((prev) => prev + event.data.data);
        } else if (event.data && event.data.type === 'stderr') {
          setStderr((prev) => prev + event.data.data);
        } else if (event.data && event.data.type === 'status') {
          console.log(`Status message in main listener: ${event.data.status}`);
          // Update the React state status
          setStatus(event.data.status);
          // Also update our local ref for immediate access
          statusRef.current = event.data.status;
        }
      };
      workerRef.current.addEventListener('message', messageListener);

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
          console.log('Worker status changed to:', newStatus + " at " + new Date().toISOString());
          setStatus(newStatus);
          statusRef.current = newStatus;

          // Also send a direct message for our event listener to catch
          workerRef.current?.postMessage({
            type: 'status',
            status: newStatus,
          });
        }),
      );

      console.log('Executor initialized, checking if worker is ready');
      
      // Before waiting on the promise, check if we're already in READY state
      if (statusRef.current === RunnerStatus.READY) {
        console.log('Worker is already READY, no need to wait for readyPromise');
        return;
      }

      console.log('Worker not yet ready, waiting for readyPromise to resolve');
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
      
      // Track if we're initializing or already initialized
      const initialStatus = statusRef.current;
      
      // Initialize the worker if needed
      await init();
      console.log('after init');

      // Now the executor should be ready - double check
      if (!executorRef.current) {
        throw new Error('Clang executor not initialized');
      }
      
      // Make absolutely sure we're ready
      if (statusRef.current !== RunnerStatus.READY) {
        // If not ready, throw an error - no waiting
        throw new Error(`Executor is in ${statusRef.current} state, not READY`);
      }

      // Create a new abort controller
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      abortControllerRef.current = new AbortController();

      try {
        // We'll collect output in local variables
        let outputStdout = stdout;
        let outputStderr = stderr;

        // Create a promise that will resolve when execution is complete
        const executionPromise = new Promise<{ stdout: string; stderr: string; success: boolean }>((resolve, reject) => {
          // Register a message handler for stdout, stderr, and status changes
          const messageHandler = (event: MessageEvent) => {
            if (event.data && event.data.type === 'stdout') {
              outputStdout += event.data.data;
              setStdout(outputStdout);
            } else if (event.data && event.data.type === 'stderr') {
              outputStderr += event.data.data;
              setStderr(outputStderr);
            } else if (event.data && event.data.type === 'runStatus') {
              setStatus(event.data.status);
              if (event.data.status === RunnerStatus.READY) {
                // Execution is complete
                workerRef.current?.removeEventListener('message', messageHandler);
                resolve({
                  stdout: outputStdout,
                  stderr: outputStderr,
                  success: true
                });
              } else if (event.data.status === RunnerStatus.FAILED_EXECUTION) {
                workerRef.current?.removeEventListener('message', messageHandler);
                resolve({
                  stdout: outputStdout,
                  stderr: outputStderr,
                  success: false
                });
              }
            }
          };
          
          workerRef.current?.addEventListener('message', messageHandler);
          
          // Send the run command
          executorRef.current?.run(code, {
            stdIn: stdin,
          }).catch(err => {
            setError(`Execution error: ${err}`);
            workerRef.current?.removeEventListener('message', messageHandler);
            reject(err);
          });
        });

        // Execute the code exactly once - no need for multiple attempts
        // Just wait for the result
        try {
          const result = await executionPromise;
          return result;
        } catch (err) {
          throw err;
        }
      } catch (err) {
        setError(`Execution error: ${err}`);
        return {
          stdout: '',
          stderr: `Execution error: ${err}`,
          success: false,
        };
      }
    },
    [status],
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
