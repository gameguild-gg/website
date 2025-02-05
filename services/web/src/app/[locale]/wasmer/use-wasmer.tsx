// 'use client';
// import { useCallback, useEffect, useRef, useState } from 'react';
// import { proxy, wrap } from 'comlink';
//
// interface WasmerWorkerAPI {
//   loadPyodideInstance: () => Promise<boolean>;
//   executePython: (code: string, onOutput: (output: string) => void, onError: (error: string) => void) => Promise<void>;
// }
//
// export function useWasmer() {
//   const workerRef = useRef<Worker | null>(null);
//   const [executePython, setExecutePython] = useState<PyodideWorkerAPI['executePython'] | null>(null);
//   const [pyodideLoaded, setPyodideLoaded] = useState(false);
//   const [loading, setLoading] = useState(true);
//   const [error, setError] = useState<string | null>(null);
//   const [output, setOutput] = useState<string>('');
//
//   useEffect(() => {
//     workerRef.current = new Worker(new URL('worker.ts', import.meta.url), { type: 'module' });
//
//     const workerApi = wrap<PyodideWorkerAPI>(workerRef.current);
//
//     setExecutePython(() => (code: string, onOutput: any, onError: any) => workerApi.executePython(code, proxy(onOutput), proxy(onError)));
//
//     workerApi
//       .loadPyodideInstance()
//       .then(() => {
//         setPyodideLoaded(true);
//         setLoading(false);
//       })
//       .catch((err) => {
//         setError(err.message);
//         setLoading(false);
//       });
//
//     return () => {
//       workerRef.current?.terminate();
//     };
//   }, []);
//
//   const runPython = useCallback(
//     async (code: string) => {
//       if (executePython) {
//         setOutput(''); // Limpa a saída antes de executar
//
//         await executePython(
//           code,
//           proxy((stdout) => setOutput((prev) => prev + stdout + '\n')), // Proxy para saída
//           proxy((stderr) => setOutput((prev) => prev + `Error: ${stderr}\n`)), // Proxy para erro
//         );
//       }
//     },
//     [executePython],
//   );
//
//   return { pyodideLoaded, loading, error, runPython, output };
// }
