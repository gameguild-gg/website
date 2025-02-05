'use client';
import { useEffect, useRef, useState } from 'react';
import { WasmerWorkerAPI } from '@/app/[locale]/wasmer/wasmer.api';
import { proxy, Remote, wrap } from 'comlink';
import { Output } from '@wasmer/sdk';

export function useWasmer() {
  const workerRef = useRef<Worker | null>(null);
  const [wasmerLoaded, setWasmerLoaded] = useState(false);
  const [wasmerLoading, setLoading] = useState(true);
  const [packageLoading, setPackageLoading] = useState(false);
  const [running, setRunning] = useState(false);
  const [selectedPackage, setSelectedPackage] = useState<string | null>(null);
  const [packageLoaded, setPackageLoaded] = useState(false);
  const workerApiRef = useRef<Remote<WasmerWorkerAPI> | null>(null);

  // const [error, setError] = useState<string | null>(null);
  // const [output, setOutput] = useState<string>('');

  useEffect(() => {
    workerRef.current = new Worker(new URL('worker.ts', import.meta.url), { type: 'classic' });
    workerApiRef.current = wrap<WasmerWorkerAPI>(workerRef.current);
    workerApiRef.current
      .init()
      .then(() => {
        setWasmerLoaded(true);
        setLoading(false);
      })
      .catch((err) => {
        // setError(err.message);
        console.error('erro');
        console.error(err);
        setLoading(false);
      });

    // return () => {
    //   workerRef.current?.terminate();
    // };
  }, []);

  const setPackage = async (packageName: string) => {
    if (!workerApiRef.current) return;
    setSelectedPackage(packageName);
    setPackageLoading(true);
    setPackageLoaded(false);
    try {
      await workerApiRef.current.fromRegistry(packageName);
      setPackageLoaded(true); // Marca que o package foi carregado com sucesso
    } catch (error) {
      console.error('Failed to load package:', error);
    } finally {
      setPackageLoading(false);
    }
  };
  const run = async (args: string[], onComplete: (result: Output) => void) => {
    if (!workerApiRef.current) return;
    setRunning(true);

    try {
      await workerApiRef.current.run(
        args,
        proxy((result) => {
          setRunning(false);
          onComplete(result);
        }),
      );
    } catch (error) {
      console.error('Erro na execução do Python:', error);
      setRunning(false);
    }
  };

  return {
    wasmerLoaded,
    wasmerLoading,
    packageLoading,
    packageLoaded,
    selectedPackage,
    setPackage,
    running,
    run,
  };
}
