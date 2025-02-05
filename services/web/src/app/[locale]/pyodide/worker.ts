import { expose } from 'comlink';
import { PyodideWorkerAPI } from './pyodide.api';


let pyodide: any = null;

const loadPyodideInstance = async () => {
  if (!pyodide) {
    await new Promise<void>((resolve, reject) => {
      try {
        importScripts('https://cdn.jsdelivr.net/pyodide/v0.27.0/full/pyodide.js');
        resolve();
      } catch (error) {
        reject(new Error('Failed to load Pyodide'));
      }
    });

    try {
      // @ts-ignore - `loadPyodide` será disponibilizado globalmente pelo script
      pyodide = await (self as any).loadPyodide();
      return true; // Indica que Pyodide foi carregado
    } catch (error) {
      throw new Error('Pyodide failed to initialize');
    }
  }
  return true;
};

const executePython = async (code: string, onOutput: (output: string) => void, onError: (error: string) => void) => {
  try {
    await loadPyodideInstance();

    pyodide.setStdout({
      batched: (output: string) => {
        onOutput(output); // Envia saída para o callback
      },
    });

    pyodide.setStderr({
      batched: (error: string) => {
        onError(error); // Envia erro para o callback
      },
    });

    await pyodide.runPythonAsync(code);
  } catch (error) {
    onError((error as Error).message);
  }
};

const workerApi: PyodideWorkerAPI = {
  loadPyodideInstance,
  executePython,
};

expose(workerApi);
