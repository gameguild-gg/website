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
  private currentStatus: RunnerStatus = RunnerStatus.UNINITIALIZED;

  setOnStdOut(onStdOut: (data: string) => void) {
    this.onStdOut = onStdOut;
  }

  setOnStdErr(onStdErr: (data: string) => void) {
    this.onStdErr = onStdErr;
  }

  setOnError(onError: (data: string) => void) {
    this.onError = onError;
  }

  getStatus(): RunnerStatus {
    return this.currentStatus;
  }

  async init(): Promise<RunnerStatus> {
    console.log('ClangWorker init called');
    
    // Already initialized
    if (this.api && this.currentStatus === RunnerStatus.READY) {
      return RunnerStatus.READY;
    }
    
    // Update status to loading
    this.currentStatus = RunnerStatus.LOADING;

    try {
      // Create a handler function that forwards to the appropriate callbacks
      const handleHostWrite = (s: string) => {
        if (s.includes('Error:')) {
          if (this.onStdErr) {
            this.onStdErr(s);
          }
        } else {
          if (this.onStdOut) {
            this.onStdOut(s);
          }
        }
      };

      // Create the API directly here instead of in a separate worker
      this.api = new API({
        hostWrite: handleHostWrite,
        showTiming: true,
      });

      // Wait for the API to be ready (memfs, etc.)
      await this.api.ready;
      
      // Update status to ready
      this.currentStatus = RunnerStatus.READY;
      return RunnerStatus.READY;
    } catch (error) {
      this.currentStatus = RunnerStatus.FAILED_LOADING;
      if (this.onError) {
        this.onError(error.toString());
      }
      return RunnerStatus.FAILED_LOADING;
    }
  }

  async run(
    code: string,
    options?: {
      stdIn?: string;
    },
  ): Promise<{ stdout: string; stderr: string; success: boolean; status: RunnerStatus }> {
    if (!this.api) {
      throw new Error('Clang API not initialized');
    }

    // Store output locally
    let outputStdout = '';
    let outputStderr = '';
    
    // Create a temporary handler that captures output locally
    const captureStdout = (data: string) => {
      outputStdout += data;
    };
    
    const captureStderr = (data: string) => {
      outputStderr += data;
    };
    
    // Store original handlers
    const originalStdout = this.onStdOut;
    const originalStderr = this.onStdErr;
    
    // Set handlers to our capture functions
    this.onStdOut = captureStdout;
    this.onStdErr = captureStderr;
    
    // Update status
    this.currentStatus = RunnerStatus.RUNNING;

    try {
      // Set stdin if provided
      if (options?.stdIn) {
        this.api.memfs.setStdinStr(options.stdIn);
      }

      // Compile, link, and run the code
      const result = await this.api.compileLinkRun(code);
      
      // Update status
      this.currentStatus = RunnerStatus.READY;
      
      // Return the collected output
      return {
        stdout: outputStdout,
        stderr: outputStderr,
        success: true,
        status: RunnerStatus.READY
      };
    } catch (error) {
      // Update status
      this.currentStatus = RunnerStatus.FAILED_EXECUTION;
      
      // Handle error output
      const errorMessage = `Execution error: ${error.toString()}`;
      outputStderr += errorMessage;
      
      return {
        stdout: outputStdout,
        stderr: outputStderr,
        success: false,
        status: RunnerStatus.FAILED_EXECUTION
      };
    } finally {
      // Restore original handlers
      this.onStdOut = originalStdout;
      this.onStdErr = originalStderr;
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
