export interface PyodideWorkerAPI {
  loadPyodideInstance: () => Promise<boolean>;
  executePython: (
    code: string,
    onOutput: (output: string) => void,
    onError: (error: string) => void,
  ) => Promise<void>;
}