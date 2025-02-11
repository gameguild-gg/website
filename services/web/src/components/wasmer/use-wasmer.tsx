'use client';

import { useRef, useState } from 'react';
import { Directory, init, Output, Wasmer } from '@wasmer/sdk';

export enum WasmerPackage {
  clang = 'clang/clang',
  python = 'python/python',
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

export type WasmerRunParams = {
  package: WasmerPackage;
  args: string[];
  mounts?: { [key: string]: Directory };
  onComplete: (result: Output) => void;
  stdin?: string;
};

export function useWasmer() {
  const packageRef = useRef<Wasmer | null>(null);
  const [wasmerStatus, setWasmerStatus] = useState<WasmerStatus>(WasmerStatus.UNINITIALIZED);
  const [error, setError] = useState<string | null>(null);

  const run = async (params: WasmerRunParams) => {
    let status: WasmerStatus = wasmerStatus;
    const changeStatus = (newStatus: WasmerStatus) => {
      status = newStatus;
      setWasmerStatus(newStatus);
    };

    try {
      if (status == WasmerStatus.UNINITIALIZED || status == WasmerStatus.FAILED_LOADING_WASMER) {
        changeStatus(WasmerStatus.LOADING_WASMER);
        await init({ sdkUrl: 'https://unpkg.com/@wasmer/sdk@latest/dist/index.mjs' });
        changeStatus(WasmerStatus.WASMER_READY);
      }
    } catch (err) {
      changeStatus(WasmerStatus.FAILED_LOADING_WASMER);
      setError(err.toString());
    }

    try {
      if (status == WasmerStatus.WASMER_READY || status == WasmerStatus.FAILED_LOADING_PACKAGE) {
        changeStatus(WasmerStatus.LOADING_PACKAGE);
        try {
          // todo: change this the be from file and be smaller.
          // example the way it is now, it will download a 141mb file for python
          // but if we compress it via brotli, it will be 23mb
          packageRef.current = await Wasmer.fromRegistry(params.package.toString());
          if (!packageRef.current || !packageRef.current.entrypoint) throw new Error(`Failed to load package ${params.package.toString()}`);
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
    changeStatus(WasmerStatus.RUNNING);

    try {
      const instance = await packageRef.current.entrypoint.run({
        args: params.args,
        stdin: params.stdin,
        mount: params.mounts,
      });
      const result = await instance.wait();
      if (params.onComplete) params.onComplete(result);
      changeStatus(WasmerStatus.READY_TO_RUN);
    } catch (error) {
      setError(error.toString());
    }
    changeStatus(WasmerStatus.READY_TO_RUN);
  };

  return {
    wasmerStatus,
    run,
    error,
  };
}
