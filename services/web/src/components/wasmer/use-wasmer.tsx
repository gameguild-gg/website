'use client';

import { useRef, useState } from 'react';
import { Directory, DirEntry, init, Instance, Output, SpawnOptions, Wasmer } from '@wasmer/sdk';

// todo: add other languages later
export type CodeLanguage = 'c' | 'cpp' | 'c++' | 'python';
// todo: add other packages later
export type WasmerPackage = 'clang/clang' | 'python/python';

function languageToWasmerPackage(language: CodeLanguage): WasmerPackage {
  switch (language) {
    case 'c':
    case 'cpp':
    case 'c++':
      return 'clang/clang';
    case 'python':
      return 'python/python';
    default:
      throw new Error(`Unsupported language ${language}`);
  }
}

function languageToFileExtension(language: CodeLanguage): string {
  switch (language) {
    case 'c':
      return 'c';
    case 'cpp':
    case 'c++':
      return 'cpp';
    case 'python':
      return 'py';
    default:
      throw new Error(`Unsupported language ${language}`);
  }
}

export enum WasmerStatus {
  UNINITIALIZED = 'Uninitialized',
  LOADING_WASMER = 'LoadingWasmer',
  FAILED_LOADING_WASMER = 'FailedLoadingWasmer',
  WASMER_READY = 'WasmerReady',
  LOADING_PACKAGE = 'LoadingPackage',
  FAILED_LOADING_PACKAGE = 'FailedLoadingPackage',
  READY_TO_RUN = 'ReadyToRun',
  RUNNING = 'Running',
  FAILED_EXECUTION = 'FailedExecution',
}

export type CodingTestParams = {
  // filesystem. it could be formated as dictionary from folder to file content, or just a string
  // hidden files starts with a dot before the filename
  files: { [key: string]: string } | string;
  tests: { input: string; output: string }[];
};

export type FileMap = { [key: string]: string | Uint8Array };
export type ProjectData = FileMap | string;
export type TestDataEntry = { input: string; output: string };
export type TestData = TestDataEntry[] | TestDataEntry;

export type CompileAndRunParams = {
  language: CodeLanguage;
  data: ProjectData;
  stdin?: string;
};

export type InlineCodeTestParams = {
  tests: TestData;
  sources: ProjectData;
};

export async function ProjectDataStringToDirectory(data: string, language: CodeLanguage): Promise<Directory> {
  const extension = languageToFileExtension(language);
  const filename = 'main.' + extension;
  let dict: Directory = new Directory();
  await dict.writeFile(filename, data);
  return dict;
}

export async function FileMapToDirectory(data: FileMap): Promise<Directory> {
  let dict: Directory = new Directory();
  for (const [key, value] of Object.entries(data)) {
    await dict.writeFile(key, value);
  }
  return dict;
}

export function useWasmer() {
  // map of WasmerPackage to Wasmer
  const packagesRef = useRef<Map<WasmerPackage, Wasmer>>(new Map());
  const [wasmerStatus, setWasmerStatus] = useState<WasmerStatus>(WasmerStatus.UNINITIALIZED);
  const [error, setError] = useState<string | null>(null);

  // todo: add a hashing function to check if the data has changed before compiling the code again
  const runCode = async (params: CompileAndRunParams): Promise<Output> => {
    setError(null);
    const packageName = languageToWasmerPackage(params.language);

    let status: WasmerStatus = wasmerStatus;
    const changeStatus = (newStatus: WasmerStatus) => {
      status = newStatus;
      setWasmerStatus(newStatus);
    };

    // block if the status is not ready to progress
    if (status == WasmerStatus.RUNNING || status == WasmerStatus.LOADING_PACKAGE || status == WasmerStatus.LOADING_WASMER) {
      console.log('Wasmer is thinking, let me work!', status);
      return;
    }

    try {
      // initialize only if it is not initialized or if it failed to load wasmer
      if (status == WasmerStatus.UNINITIALIZED || status == WasmerStatus.FAILED_LOADING_WASMER) {
        changeStatus(WasmerStatus.LOADING_WASMER);
        await init({ sdkUrl: 'https://unpkg.com/@wasmer/sdk@latest/dist/index.mjs' });
        changeStatus(WasmerStatus.WASMER_READY);
      }
    } catch (err) {
      changeStatus(WasmerStatus.FAILED_LOADING_WASMER);
      setError(err.toString());
    }

    let packageRef = packagesRef.current.get(packageName);
    try {
      // if wasmer is ready, load the package
      if (status == WasmerStatus.WASMER_READY || status == WasmerStatus.FAILED_LOADING_PACKAGE) {
        changeStatus(WasmerStatus.LOADING_PACKAGE);
        try {
          if (!packageRef) {
            // todo: change this the be from file and be smaller.
            // example the way it is now, it will download a 141mb file for python
            // but if we compress it via brotli, it will be 23mb
            packageRef = await Wasmer.fromRegistry(packageName);
            if (!packageRef || !packageRef.entrypoint) throw new Error(`Failed to load package ${packageName}`);
            packagesRef.current.set(packageName, packageRef);
          }
          changeStatus(WasmerStatus.READY_TO_RUN);
        } catch (error) {
          setError(error.toString());
          changeStatus(WasmerStatus.FAILED_LOADING_PACKAGE);
        }
      }
    } catch (err) {
      changeStatus(WasmerStatus.FAILED_LOADING_PACKAGE);
      setError(err.toString());
    }

    if (!(status == WasmerStatus.READY_TO_RUN || status == WasmerStatus.FAILED_EXECUTION)) {
      return;
    }

    // set the storage mounts
    let dir: Directory;
    if (typeof params.data === 'string') dir = await ProjectDataStringToDirectory(params.data, params.language);
    else dir = await FileMapToDirectory(params.data);

    // set the arguments
    let args: string[] = [];
    switch (packageName) {
      case 'python/python':
        args = ['main.py'];
        break;
      case 'clang/clang':
        // list c and cpp files from the directory
        const files: DirEntry[] = await dir.readDir('/');
        const cFiles: string[] = files.filter((file) => file.name.endsWith('.c') || file.name.endsWith('.cpp')).map((file) => file.name);
        args = [...cFiles, '-o', 'output.wasm'];
        debugger;
        break;
      default:
        throw new Error(`Unsupported package ${packageName}`);
    }

    changeStatus(WasmerStatus.RUNNING);

    try {
      let opts: SpawnOptions = {
        args: args,
        stdin: params.stdin,
        mount: { '/project': dir },
        cwd: '/project',
      };
      const instance: Instance = await packageRef.entrypoint.run(opts);
      const result: Output = await instance.wait();
      if (result.stderr) {
        changeStatus(WasmerStatus.FAILED_EXECUTION);
        setError(result.stderr);
        return result;
      } else changeStatus(WasmerStatus.READY_TO_RUN);
      if (packageName === 'clang/clang') {
        const outputFileContent: Uint8Array = await dir.readFile('output.wasm');
        const wasm: Wasmer = await Wasmer.fromFile(outputFileContent);
        const instance: Instance = await wasm.entrypoint.run();
        return await instance.wait();
      }
      return result;
    } catch (error) {
      changeStatus(WasmerStatus.FAILED_EXECUTION);
      setError(error.toString());
    }
  };

  return {
    wasmerStatus,
    runCode,
    error,
  };
}
