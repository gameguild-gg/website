import { Output } from '@wasmer/sdk';

export interface WasmerWorkerAPI {
  init: () => Promise<boolean>;
  fromRegistry: (registry: string) => Promise<boolean>;
  getLoadedPackage: () => string | null;
  run: (args: string[], onComplete: (result: Output) => void) => Promise<void>;
}
