'use client';
import { useCallback, useRef, useState } from 'react';
import { proxy, wrap } from 'comlink';

interface PyodideWorkerAPI {
  loadPyodideInstance: () => Promise<boolean>;
  executePython: (code: string, onOutput: (output: string) => void, onError: (error: string) => void) => Promise<void>;
}

export function usePyodide() {
  const workerRef = useRef<Worker | null>(null);
  const pyodideApiRef = useRef<PyodideWorkerAPI | null>(null);
  const [pyodideLoaded, setPyodideLoaded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [output, setOutput] = useState<string>('');

  const initPyodide = useCallback(async () => {
    // Don't initialize if already loading or loaded
    if (loading || pyodideLoaded) return;

    try {
      setLoading(true);
      setError(null);

      // Create worker if it doesn't exist
      if (!workerRef.current) {
        workerRef.current = new Worker(new URL('worker.ts', import.meta.url), { type: 'module' });
        pyodideApiRef.current = wrap<PyodideWorkerAPI>(workerRef.current);
      }

      // Load Pyodide
      const loaded = await pyodideApiRef.current.loadPyodideInstance();
      setPyodideLoaded(loaded);
      setLoading(false);
      return loaded;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
      setLoading(false);
      return false;
    }
  }, [loading, pyodideLoaded]);

  const runPython = useCallback(
    async (code: string) => {
      // Initialize Pyodide if not already loaded
      if (!pyodideLoaded) {
        const success = await initPyodide();
        if (!success) {
          throw new Error('Failed to load Pyodide runtime');
        }
      }

      // Clear output before execution
      setOutput('');
      setError(null);

      if (!pyodideApiRef.current) {
        throw new Error('Pyodide API not initialized');
      }

      try {
        await pyodideApiRef.current.executePython(
          code,
          proxy((stdout) => setOutput((prev) => prev + stdout + '\n')), // Proxy for stdout
          proxy((stderr) => {
            setError(stderr);
            setOutput((prev) => prev + `Error: ${stderr}\n`); // Also add errors to output
          }),
        );
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : String(err);
        setError(errorMessage);
        setOutput((prev) => prev + `Runtime Error: ${errorMessage}\n`);
      }
    },
    [pyodideLoaded, initPyodide],
  );

  // Cleanup function to terminate worker when component unmounts
  const cleanup = useCallback(() => {
    if (workerRef.current) {
      workerRef.current.terminate();
      workerRef.current = null;
      pyodideApiRef.current = null;
    }
  }, []);

  return { 
    pyodideLoaded, 
    loading, 
    error, 
    runPython, 
    output,
    initPyodide, // Expose initialization function
    cleanup 
  };
}
