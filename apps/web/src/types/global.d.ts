// global.d.ts
export {};
declare let __webpack_public_path__: string;
declare global {
  interface Window {
    emception: typeof emception;
    Comlink: typeof Comlink;
  }
}

declare module '*.worker.ts' {
  // This tells TypeScript that importing a worker returns a constructor
  // that creates a Worker instance.
  const WorkerFactory: {
    new (): Worker;
  };
  export default WorkerFactory;
}
