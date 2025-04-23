import { expose } from 'comlink';
import { CodeExecutorBase } from '@/components/code/code-executor.base';
import { API } from './clang/core/cpp/api';
import { RunnerStatus } from './code-executor.types';
import { setSharedApi, getSharedApi } from './clang/core/cpp/worker';

class ClangWorker implements CodeExecutorBase {
  private currentStatus: RunnerStatus = RunnerStatus.UNINITIALIZED;
  private onStdOut: ((data: string) => void) | null = null;
  private onStdErr: ((data: string) => void) | null = null;
  private onError: ((data: string) => void) | null = null;
  private currentStage: 'init' | 'compile' | 'link' | 'execute' = 'init';
  private accumulatedOutput: string = '';
  private api: API | null = null;

  constructor() {
    console.log('ClangWorker called');
  }

  private handleOutput(data: string) {
    if (data === null || data === undefined) return;
    this.accumulatedOutput += data;

    // Send real-time output without formatting
    if (this.onStdOut) {
      const stageOutput = {
        type: 'output',
        stages: [{
          stage: this.currentStage,
          output: data
        }]
      };
      this.onStdOut(JSON.stringify(stageOutput));
    }
  }

  setOnStdOut(onStdOut: (data: string) => void) {
    this.onStdOut = onStdOut;
  }

  setOnStdErr(onStdErr: (data: string) => void) {
    // We'll use stdout for everything
  }

  setOnError(onError: (data: string) => void) {
    // We'll use stdout for everything
  }

  getStatus(): RunnerStatus {
    return this.currentStatus;
  }

  async init(): Promise<RunnerStatus> {
    console.log('ClangWorker init called');
    
    try {
      let api = getSharedApi();
      
      if (!api) {
        api = new API({
          hostWrite: (s: string) => this.handleOutput(s)
        });
        setSharedApi(api);
      }
      
      this.api = api;
      this.currentStatus = RunnerStatus.LOADING;
      
      await this.api.ready;
      
      this.currentStatus = RunnerStatus.READY;
      return RunnerStatus.READY;
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error);
      this.currentStatus = RunnerStatus.FAILED_LOADING;
      this.handleOutput(`Error during initialization: ${errorMsg}`);
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

    // Reset state at the start of each run
    this.currentStatus = RunnerStatus.RUNNING;
    this.accumulatedOutput = '';

    try {
      const input = `test.cpp`;
      const obj = `test.o`;
      const wasm = `test.wasm`;

      if (options?.stdIn) {
        this.api.setStdinStr(options.stdIn);
      }

      // Cleanup any previous files
      try {
        this.api.memfs.removeFile(input);
        this.api.memfs.removeFile(obj);
        this.api.memfs.removeFile(wasm);
      } catch (e) {
        // Ignore cleanup errors
      }

      // Compilation stage
      this.currentStage = 'compile';
      this.accumulatedOutput = '';
      await this.api.compile({ input, contents: code, obj });

      // Linking stage
      this.currentStage = 'link';
      this.accumulatedOutput = '';
      await this.api.link(obj, wasm);

      // Execution stage
      this.currentStage = 'execute';
      this.accumulatedOutput = '';
      const buffer = this.api.getFileContents(wasm);
      const testMod = await WebAssembly.compile(buffer);
      await this.api.run(testMod, wasm);
      
      // Wait a small amount of time for any remaining output
      await new Promise(resolve => setTimeout(resolve, 100));

      // Successful completion - reset to ready state
      this.currentStatus = RunnerStatus.READY;

      return {
        stdout: '',
        stderr: '',
        success: true,
        status: RunnerStatus.READY
      };
    } catch (error) {
      // Error completion - reset to ready state after error
      const errorMsg = error instanceof Error ? error.message : String(error);
      this.handleOutput(`Error: ${errorMsg}`);
      this.currentStatus = RunnerStatus.READY; // Reset to ready even after error

      return {
        stdout: '',
        stderr: '',
        success: false,
        status: RunnerStatus.READY // Return ready status to allow new runs
      };
    } finally {
      // Ensure cleanup happens whether we succeed or fail
      try {
        this.api.memfs.removeFile('test.cpp');
        this.api.memfs.removeFile('test.o');
        this.api.memfs.removeFile('test.wasm');
      } catch (e) {
        // Ignore cleanup errors
      }
    }
  }
}

const worker = new ClangWorker();
expose(worker);
