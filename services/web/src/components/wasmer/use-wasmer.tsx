'use client';

import { useRef, useState } from 'react';
import { init, Output, Wasmer } from '@wasmer/sdk';

export enum WasmerPackage {
  clang = 'clang/clang',
  python = 'python/python',
}

export enum WasmerStatus {
  UNINITIALIZED = 'Uninitialized',
  LOADING_WASMER = 'Loading Wasmer',
  WASMER_READY = 'Wasmer Ready',
  LOADING_PACKAGE = 'Loading Package',
  READY = 'Ready',
  RUNNING = 'Running',
  FAILED_EXECUTION = 'Failed Execution',
  FAILED_LOADING_WASMER = 'Failed Loading Wasmer',
  FAILED_LOADING_PACKAGE = 'Failed Loading Package',
}

export function useWasmer(packageName: WasmerPackage) {
  const packageRef = useRef<Wasmer | null>(null);
  const [wasmerStatus, setWasmerStatus] = useState<WasmerStatus>(WasmerStatus.UNINITIALIZED);
  const [error, setError] = useState<string | null>(null);

  const setPackage = async (packageName: string) => {
    setWasmerStatus(WasmerStatus.LOADING_PACKAGE);
    try {
      // todo: change this the be from file and be smaller.
      // example the way it is now, it will download a 141mb file for python
      // but if we compress it via brotli, it will be 23mb
      packageRef.current = await Wasmer.fromRegistry(packageName);
      if (!packageRef.current || !packageRef.current.entrypoint) {
        throw new Error(`Failed to load package ${packageName}`);
      }
      setWasmerStatus(WasmerStatus.READY);
    } catch (error) {
      setError(error.toString());
      setWasmerStatus(WasmerStatus.FAILED_LOADING_PACKAGE);
    }
  };

  const run = async (args: string[], onComplete: (result: Output) => void) => {
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
        await setPackage(packageName);
        changeStatus(WasmerStatus.READY);
      }
    } catch (err) {
      changeStatus(WasmerStatus.FAILED_LOADING_PACKAGE);
      setError(err.toString());
    }

    if (!(status == WasmerStatus.READY || status == WasmerStatus.FAILED_EXECUTION)) {
      return;
    }
    changeStatus(WasmerStatus.RUNNING);

    try {
      const instance = await packageRef.current.entrypoint.run({ args });
      const result = await instance.wait();
      onComplete(result);
      changeStatus(WasmerStatus.READY);
    } catch (error) {
      setError(error.toString());
    }
    changeStatus(WasmerStatus.READY);
  };

  return {
    wasmerStatus,
    run,
    error,
  };
}
