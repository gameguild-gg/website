import { expose } from 'comlink';
import { CodeExecutorBase } from '@/components/code/code-executor.base';
import { API } from './clang/core/cpp/api';
import { RunnerStatus } from './types';
import { getSharedApi, setSharedApi } from './clang/core/cpp/worker';
import { MemFS } from './clang/core/cpp/memfs';

class ClangWorker implements CodeExecutorBase {
  private currentStatus: RunnerStatus = RunnerStatus.UNINITIALIZED;
  private onStdOut: ((data: string) => void) | null = null;
  private onStdErr: ((data: string) => void) | null = null;
  private memfs: MemFS;
  private api: API | null = null;
  private currentStage: 'init' | 'compile' | 'link' | 'execute' = 'init';

  constructor() {
    this.memfs = new MemFS({
      hostWrite: (message: string) => {
        if (this.onStdOut) {
          this.onStdOut(message);
        }
      },
    });
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
          hostWrite: (s: string) => this.handleOutput(s),
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

    try {
      const input = `test.cpp`;
      const obj = `test.o`;
      const wasm = `test.wasm`;

      if (options?.stdIn) {
        this.api.setStdinStr(options.stdIn);
      }

      // Cleanup any previous files by replacing them with empty content
      try {
        await this.writeFile(input, new Uint8Array(0));
        await this.writeFile(obj, new Uint8Array(0));
        await this.writeFile(wasm, new Uint8Array(0));
      } catch (e) {
        // Ignore cleanup errors
      }

      // Compilation stage
      this.currentStage = 'compile';
      await this.api.compile({ input, contents: code, obj });

      // Linking stage
      this.currentStage = 'link';
      await this.api.link(obj, wasm);

      // Execution stage
      this.currentStage = 'execute';
      const buffer = this.api.getFileContents(wasm);
      const testMod = await WebAssembly.compile(buffer);
      await this.api.run(testMod, wasm);

      // Wait a small amount of time for any remaining output
      await new Promise((resolve) => setTimeout(resolve, 100));

      // Successful completion - reset to ready state
      this.currentStatus = RunnerStatus.READY;

      return {
        stdout: '',
        stderr: '',
        success: true,
        status: RunnerStatus.READY,
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
        status: RunnerStatus.READY, // Return ready status to allow new runs
      };
    } finally {
      // Ensure cleanup happens whether we succeed or fail
      try {
        await this.writeFile('test.cpp', new Uint8Array(0));
        await this.writeFile('test.o', new Uint8Array(0));
        await this.writeFile('test.wasm', new Uint8Array(0));
      } catch (e) {
        // Ignore cleanup errors
      }
    }
  }

  async readFile(path: string): Promise<Uint8Array> {
    await this.memfs.ensureInitialized();
    return this.memfs.getFileContents(path);
  }

  async writeFile(path: string, content: Uint8Array): Promise<void> {
    await this.memfs.ensureInitialized();
    this.memfs.addFile(path, content);
  }

  async copyFile(src: string, dest: string): Promise<void> {
    const content = await this.readFile(src);
    await this.writeFile(dest, content);
  }

  async createDirectory(path: string): Promise<void> {
    await this.memfs.ensureInitialized();
    this.memfs.addDirectory(path);
  }

  async deleteFile(path: string): Promise<void> {
    await this.memfs.ensureInitialized();
    this.memfs.addFile(path, new Uint8Array(0)); // Replace with empty file since memfs doesn't have delete
  }

  async deleteDirectory(path: string): Promise<void> {
    await this.memfs.ensureInitialized();
    // No-op since memfs doesn't support directory deletion
  }

  async renameFile(oldPath: string, newPath: string): Promise<void> {
    const content = await this.readFile(oldPath);
    await this.writeFile(newPath, content);
    await this.deleteFile(oldPath);
  }

  async renameDirectory(oldPath: string, newPath: string): Promise<void> {
    // No-op since memfs doesn't support directory operations directly
  }

  async getEnv(key: string): Promise<string | undefined> {
    return process.env[key];
  }

  async setEnv(key: string, value: string): Promise<void> {
    process.env[key] = value;
  }

  private handleOutput(data: string) {
    if (data === null || data === undefined) return;

    // Send real-time output without formatting
    if (this.onStdOut) {
      const stageOutput = {
        type: 'output',
        stages: [
          {
            stage: this.currentStage,
            output: data,
          },
        ],
      };
      this.onStdOut(JSON.stringify(stageOutput));
    }
  }
}

const worker = new ClangWorker();
expose(worker);
