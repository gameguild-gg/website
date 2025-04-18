import { expose } from 'comlink';
import { CodeExecutorBase } from '@/components/code/code-executor.base';
import { API } from './clang/core/cpp/api';
import { RunnerStatus } from './code-executor.types';

console.log('ClangWorker called');

class ClangWorker implements CodeExecutorBase {
  private api: API | null = null;
  private onStdOut: ((data: string) => void) | null = null;
  private currentStatus: RunnerStatus = RunnerStatus.UNINITIALIZED;
  private accumulatedOutput = '';

  setOnStdOut(onStdOut: (data: string) => void) {
    this.onStdOut = onStdOut;
  }

  // Redirect stderr to stdout
  setOnStdErr(onStdErr: (data: string) => void) {
    // We'll use stdout for everything
  }

  // Redirect errors to stdout
  setOnError(onError: (data: string) => void) {
    // We'll use stdout for everything
  }

  getStatus(): RunnerStatus {
    return this.currentStatus;
  }

  private handleOutput(data: string) {
    this.accumulatedOutput += data;
    if (this.onStdOut) {
      this.onStdOut(data);
    }
  }

  async init(): Promise<RunnerStatus> {
    console.log('ClangWorker init called');
    
    if (this.api && this.currentStatus === RunnerStatus.READY) {
      return RunnerStatus.READY;
    }
    
    this.currentStatus = RunnerStatus.LOADING;
    this.accumulatedOutput = '';

    try {
      this.api = new API({
        hostWrite: (s: string) => this.handleOutput(s),
      });

      await this.api.ready;
      this.currentStatus = RunnerStatus.READY;
      return RunnerStatus.READY;
    } catch (error) {
      this.currentStatus = RunnerStatus.FAILED_LOADING;
      this.handleOutput(`Error during initialization: ${error}`);
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

    this.accumulatedOutput = '';
    let compilationOutput = '';
    let linkingOutput = '';
    let executionOutput = '';
    
    try {
      // Stage 1: Compilation
      const input = `test.cpp`;
      const obj = `test.o`;
      const wasm = `test.wasm`;

      if (options?.stdIn) {
        this.api.setStdinStr(options.stdIn);
      }
      
      if (options?.stdIn) {
        this.api.setStdinStr(options.stdIn);
      }

      // Compilation stage
      try {
        const savedOutput = this.accumulatedOutput;
        this.accumulatedOutput = '';
        await this.api.compile({ input, contents: code, obj });
        compilationOutput = this.accumulatedOutput;
        this.accumulatedOutput = savedOutput;
      } catch (error) {
        throw new Error(`Compilation error: ${error}`);
      }

      // Linking stage
      try {
        const savedOutput = this.accumulatedOutput;
        this.accumulatedOutput = '';
        await this.api.link(obj, wasm);
        linkingOutput = this.accumulatedOutput;
        this.accumulatedOutput = savedOutput;
      } catch (error) {
        throw new Error(`Linking error: ${error}`);
      }

      // Execution stage
      const savedOutput = this.accumulatedOutput;
      this.accumulatedOutput = '';
      const buffer = this.api.getFileContents(wasm);
      const testMod = await WebAssembly.compile(buffer);
      const app = await this.api.run(testMod, wasm);
      executionOutput = this.accumulatedOutput;
      this.accumulatedOutput = savedOutput;

      this.currentStatus = RunnerStatus.READY;

      return {
        stdout: `[COMPILATION]\n${compilationOutput}\n[LINKING]\n${linkingOutput}\n[EXECUTION]\n${executionOutput}`,
        stderr: '', // Keep empty as we're using stdout for everything
        success: true,
        status: RunnerStatus.READY
      };
    } catch (error) {
      this.currentStatus = RunnerStatus.FAILED_EXECUTION;
      const errorMsg = error.toString();
      
      return {
        stdout: `[COMPILATION]\n${compilationOutput}\n[LINKING]\n${linkingOutput}\n[ERROR]\n${errorMsg}`,
        stderr: '', // Keep empty as we're using stdout for everything
        success: false,
        status: RunnerStatus.FAILED_EXECUTION
      };
    }
  }

  // File system operations
  async readFile(path: string, onError?: (error: string) => void): Promise<Uint8Array> {
    try {
      if (!this.api) throw new Error('API not initialized');
      return this.api.getFileContents(path);
    } catch (error) {
      this.handleOutput(`Error reading file: ${error}`);
      throw error;
    }
  }

  async writeFile(path: string, content: Uint8Array, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      this.api.addFile(path, content);
    } catch (error) {
      this.handleOutput(`Error writing file: ${error}`);
      throw error;
    }
  }

  async createDirectory(path: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      this.api.addDirectory(path);
    } catch (error) {
      this.handleOutput(`Error creating directory: ${error}`);
      throw error;
    }
  }

  async copyFile(source: string, destination: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      const content = this.api.getFileContents(source);
      this.api.addFile(destination, content);
    } catch (error) {
      this.handleOutput(`Error copying file: ${error}`);
      throw error;
    }
  }

  async deleteFile(path: string, onError?: (error: string) => void): Promise<void> {
    this.handleOutput('deleteFile not implemented');
    throw new Error('Method not implemented');
  }

  async deleteDirectory(path: string, onError?: (error: string) => void): Promise<void> {
    this.handleOutput('deleteDirectory not implemented');
    throw new Error('Method not implemented');
  }

  async renameFile(oldPath: string, newPath: string, onError?: (error: string) => void): Promise<void> {
    try {
      if (!this.api) throw new Error('API not initialized');
      const content = this.api.getFileContents(oldPath);
      this.api.addFile(newPath, content);
    } catch (error) {
      this.handleOutput(`Error renaming file: ${error}`);
      throw error;
    }
  }

  async renameDirectory(oldPath: string, newPath: string, onError?: (error: string) => void): Promise<void> {
    this.handleOutput('renameDirectory not implemented');
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
