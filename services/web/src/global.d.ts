// global.d.ts
export {};
declare let __webpack_public_path__: string;
declare global {
  interface Window {
    emception: typeof emception;
    Comlink: typeof Comlink;
  }
}

declare module 'remark-admonitions' {
  const remarkAdmonitions: any;
  export default remarkAdmonitions;
}
