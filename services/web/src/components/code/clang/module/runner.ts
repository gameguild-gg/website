import create from 'zustand';
import { persist } from 'zustand/middleware';
import { DefaultCppCode } from '../examples';
import { Language } from './types';
import { initWorker } from './utils';

export const useRunner = create(
  persist<{
    language: Language;
    codeMap: Record<Language, string>;
    workerMap: { [key in Language]?: { worker: Worker; messagePort: MessagePort } };
    setCode: (code: string) => void;
    setLanguage: (language: Language) => void;
    init: () => void;
  }>(
    (set, get) => ({
      workerMap: {},
      codeMap: {
        [Language.Cpp]: DefaultCppCode,
      },
      language: Language.Cpp,
      setCode: (code: string) => {
        const state = get();
        const codeMap = { ...state.codeMap, [state.language]: code };
        set({ codeMap });
      },
      setLanguage: (language: Language) => set({ language }),
      init() {
        // Check if workers are already initialized to prevent infinite loops
        const state = get();
        if (Object.keys(state.workerMap).length > 0) {
          console.log('Workers already initialized, skipping initialization');
          return;
        }

        console.log('Initializing workers');
        set((state) => {
          const newWorkerMap = { ...state.workerMap };
          for (const language of Object.values(Language)) {
            newWorkerMap[language] = initWorker(language);
          }
          return { workerMap: newWorkerMap };
        });
      }
    }),
    {
      name: 'runner-store',
      version: 2,
      getStorage: () => localStorage,
      partialize: (state) => Object.fromEntries(Object.entries(state).filter(([key]) => key !== 'workerMap')) as any,
    },
  ),
);

export const useMessagePort = () => {
  return useRunner((state) => state.workerMap[state.language]?.messagePort);
};
