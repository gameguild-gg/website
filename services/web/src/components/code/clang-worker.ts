import { expose } from 'comlink';
import { CodeExecutorBase } from '@/components/code/code-executor.base';
import { API } from './clang/core/cpp/api';
import { RunnerStatus } from './code-executor.types';

console.log('ClangWorker called');

class ClangWorker implements CodeExecutorBase {
  private api: API | null = null;
  private onStdOut: ((data: string) => void) | null = null;
  private onStdErr: ((data: string) => void) | null = null;
  private onError: ((data: string) => void) | null = null;
  private currentAbortController: AbortController | null = null;

  setOnStdOut(onStdOut: (data: string) => void) {
    this.onStdOut = onStdOut;
  }

  setOnStdErr(onStdErr: (data: string) => void) {
    this.onStdErr = onStdErr;
  }

  setOnError(onError: (data: string) => void) {
    this.onError = onError;
  }

  async init(onStatusChange: (status: RunnerStatus) => void) {
    console.log('ClangWorker init called');
    onStatusChange(RunnerStatus.LOADING);

    try {
      this.api = new API({
        hostWrite: (s: string) => {
          if (s.includes('Error:')) {
            this.onStdErr?.(s);
          } else {
            this.onStdOut?.(s);
          }
        },
        showTiming: true,
      });

      // Wait for the API to be ready (memfs, etc)
      await this.api.ready;
      onStatusChange(RunnerStatus.READY);
    } catch (error) {
      onStatusChange(RunnerStatus.FAILED_LOADING);
      this.onError?.(error.toString());
    }
  }

  async run(
    code: string,
    options?: {
      onStatusChange?: (status: RunnerStatus) => void;
      abort?: () => void;
      stdIn?: string;
      onStdOut?: (data: string) => void;
      onStdErr?: (data: string) => void;
    },
  ): Promise<void> {
    if (!this.api) {
      throw new Error('Clang API not initialized');
    }

    const onStatusChange = options?.onStatusChange;
    const localStdOut = options?.onStdOut || this.onStdOut;
    const localStdErr = options?.onStdErr || this.onStdErr;

    onStatusChange?.(RunnerStatus.RUNNING);

    try {
      // Compile, link, and run the code
      const result = await this.api.compileLinkRun(code);
      onStatusChange?.(RunnerStatus.READY);
      // return result;
    } catch (error) {
      onStatusChange?.(RunnerStatus.FAILED_EXECUTION);
      localStdErr?.(`Execution error: ${error.toString()}`);
      throw error;
    }
  }

  // File system operations
  async readFile(path: string, onError?: (error: string) => void): Promise<Uint8Array> {
    try {
      if (!this.api) throw new Error('API not initialized');
      return this.api.memfs.getFileContents(path);
    } catch (error) {
      onError?.(error.toString());
      throw error;
    }
  }

  async writeFile(path: string, content: Uint8Array, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      this.api.memfs.addFile(path, content);
    } catch (error) {
      onError?.(error.toString());
      throw error;
    }
  }

  async createDirectory(path: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      this.api.memfs.addDirectory(path);
    } catch (error) {
      onError?.(error.toString());
      throw error;
    }
  }

  // Implement remaining required methods
  async copyFile(source: string, destination: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      const content = this.api.memfs.getFileContents(source);
      this.api.memfs.addFile(destination, content);
    } catch (error) {
      onError?.(error.toString());
      throw error;
    }
  }

  async deleteFile(path: string, onError?: (error: string) => void): Promise<void> {
    onError?.('deleteFile not implemented');
    throw new Error('Method not implemented');
  }

  async deleteDirectory(path: string, onError?: (error: string) => void): Promise<void> {
    onError?.('deleteDirectory not implemented');
    throw new Error('Method not implemented');
  }

  async renameFile(oldPath: string, newPath: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      const content = this.api.memfs.getFileContents(oldPath);
      this.api.memfs.addFile(newPath, content);
      // Note: This doesn't actually delete the old file as memfs doesn't support this directly
    } catch (error) {
      onError?.(error.toString());
      throw error;
    }
  }

  async renameDirectory(oldPath: string, newPath: string, onError?: (error: string) => void): Promise<void> {
    onError?.('renameDirectory not implemented');
    throw new Error('Method not implemented');
  }

  async getEnv(key: string, onError?: (error: string) => void): Promise<string | undefined> {
    return undefined;
  }

  async setEnv(key: string, value: string, onError?: (error: string) => void): Promise<void> {
    // Not implemented for this worker
  }
}

const worker = new ClangWorker();
expose(worker);
