import { RunnerExecutionStatus, RunnerInitStatus } from '@/components/code/code-executor.types';

export interface CodeExecutorBase {
  // onStdOut
  setOnStdOut: (onStdOut: (data: string) => void) => void;
  // onStdErr
  setOnStdErr: (error: (data: string) => void) => void;
  // onError
  setOnError: (error: (data: string) => void) => void;

  // todo: status should be global for all instances. We may want to have more than one run to happen at the same time
  init: (onStatusChange: (status: RunnerInitStatus) => void) => Promise<void>;

  run: (
    command: string,
    options?: {
      onStatusChange?: (status: RunnerExecutionStatus) => void;
      abort?: () => void;
      // todo: probably this should be a function that will return data to the wasm app
      stdIn?: string;
      onStdOut?: (data: string) => void;
      onStdErr?: (data: string) => void;
    },
  ) => Promise<void>;

  // read and write the file system
  readFile: (path: string, onError?: (error: string) => void) => Promise<Uint8Array>;
  writeFile: (path: string, content: Uint8Array, onError?: (error: string) => void) => Promise<void>;
  copyFile: (source: string, destination: string, onError?: (error: string) => void) => Promise<void>;

  // file manipulation
  createDirectory: (path: string, onError?: (error: string) => void) => Promise<void>;
  deleteFile: (path: string, onError?: (error: string) => void) => Promise<void>;
  deleteDirectory: (path: string, onError?: (error: string) => void) => Promise<void>;
  renameFile: (oldPath: string, newPath: string, onError?: (error: string) => void) => Promise<void>;
  renameDirectory: (oldPath: string, newPath: string, onError?: (error: string) => void) => Promise<void>;

  // get / set environment variables
  getEnv: (key: string, onError?: (error: string) => void) => Promise<string | undefined>;
  setEnv: (key: string, value: string, onError?: (error: string) => void) => Promise<void>;
}
