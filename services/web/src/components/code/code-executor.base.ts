import { RunnerStatus } from '@/components/code/types';

// Define the StageOutput type for structured output
export type StageOutput = {
  stage: 'init' | 'compile' | 'link' | 'execute';
  output: string;
};

export interface CodeExecutorBase {
  // Updated callback signatures to use StageOutput objects
  setOnStdOut: (onStdOut: (data: StageOutput) => void) => void;
  setOnStdErr: (onStdErr: (data: StageOutput) => void) => void;
  setOnError: (onError: (data: StageOutput) => void) => void;

  // Initialize the worker and wait until it's ready
  // Returns the status when initialization is complete
  init: () => Promise<RunnerStatus>;

  // Get the current status without waiting
  getStatus: () => RunnerStatus;

  // Run code and return the result directly
  run: (
    command: string,
    options?: {
      stdIn?: string;
    },
  ) => Promise<{
    stdout: string;
    stderr: string;
    success: boolean;
    status: RunnerStatus;
  }>;

  // read and write the file system
  readFile: (path: string) => Promise<Uint8Array>;
  writeFile: (path: string, content: Uint8Array) => Promise<void>;
  copyFile: (source: string, destination: string) => Promise<void>;

  // file manipulation
  createDirectory: (path: string) => Promise<void>;
  deleteFile: (path: string) => Promise<void>;
  deleteDirectory: (path: string) => Promise<void>;
  renameFile: (oldPath: string, newPath: string) => Promise<void>;
  renameDirectory: (oldPath: string, newPath: string) => Promise<void>;

  // get / set environment variables
  getEnv: (key: string) => Promise<string | undefined>;
  setEnv: (key: string, value: string) => Promise<void>;
}
