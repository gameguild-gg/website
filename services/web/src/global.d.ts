// global.d.ts
import Emception from './emception';

declare global {
  interface Global {
    emception: Emception;
  }

  // Ensure globalThis is extended
  var globalThis: Global;
}

export {};
