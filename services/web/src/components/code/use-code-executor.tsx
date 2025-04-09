import { useRef } from 'react';
import { wrap } from 'comlink';
import { CodeExecutorBase } from '@/components/code/code-executor.base';

// todo: this should be a context, right?
export function useCodeExecutor() {
  // comlink worker
  const workerRef = useRef<Worker | null>(null);
  workerRef.current = new Worker(new URL('./worker.ts', import.meta.url), { type: 'module' });
  // expose the worker to the main thread
  const workerApi = wrap<CodeExecutorBase>(workerRef.current);
  const workerApiRef = useRef(workerApi);
  // todo: this should be exported differently, right?
  return { workerApiRef };
}
