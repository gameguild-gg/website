// import { expose } from 'comlink';
// import { PyodideWorkerAPI } from './pyodide.api';
//
// let wasmer: any = null;
//
// const loadWasmerInstance = async () => {
//   if (!wasmer) {
//     await new Promise<void>((resolve, reject) => {
//       try {
//         importScripts('https://unpkg.com/@wasmer/sdk@latest/dist/index.mjs');
//         resolve();
//       } catch (error) {
//         reject(new Error('Failed to load Wasmer'));
//       }
//     });
//
//     try {
//       // @ts-ignore - `init` será disponibilizado globalmente pelo script
//       wasmer = await (self as any).init();
//       return true; // Indica que wasmer foi carregado
//     } catch (error) {
//       throw new Error('Wasmer failed to initialize');
//     }
//   }
//   return true;
// };
//
// // const executePython = async (code: string, onOutput: (output: string) => void, onError: (error: string) => void) => {
// //   try {
// //     await loadWasmerInstance();
// //
// //     wasmer.setStdout({
// //       batched: (output: string) => {
// //         onOutput(output); // Envia saída para o callback
// //       },
// //     });
// //
// //     wasmer.setStderr({
// //       batched: (error: string) => {
// //         onError(error); // Envia erro para o callback
// //       },
// //     });
// //
// //     await pyodide.runPythonAsync(code);
// //   } catch (error) {
// //     onError((error as Error).message);
// //   }
// // };
//
// const workerApi: PyodideWorkerAPI = {
//   loadWasmerInstance,
//   // executePython,
// };
//
// expose(workerApi);
