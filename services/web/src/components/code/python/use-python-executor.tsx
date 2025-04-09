import { usePyodide } from '@/app/[locale]/pyodide/use-pyodite';

export function UsePythonExecutor() {
  const pyodide = usePyodide();
  const init = async () => {};

  return {
    init,
  };
}
