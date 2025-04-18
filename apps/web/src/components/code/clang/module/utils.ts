import { Language } from './types';

export function initWorker(language: Language) {
  let worker: Worker;
  
  // Dynamic import for Next.js compatibility
  if (language === Language.Cpp) {
    // Use a more Next.js-friendly approach for Web Workers
    if (typeof window !== 'undefined') {
      worker = new Worker(new URL('../core/cpp/worker.ts', import.meta.url));
    } else {
      // Handle SSR case
      console.warn('Web Workers are not available during server-side rendering');
      return { messagePort: null, worker: null };
    }
  } else {
    console.error(`Unsupported language: ${language}`);
    return { messagePort: null, worker: null };
  }
  
  const messageChannel = new MessageChannel();

  worker.postMessage({ id: 'constructor', data: messageChannel.port2 }, [messageChannel.port2]);

  return { messagePort: messageChannel.port1, worker };
}
