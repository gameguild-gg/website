import create from 'zustand';
import { persist } from 'zustand/middleware';
import { DefaultCppCode } from '../examples';
import { initWorker } from './utils';
import { CodeLanguage } from '../../types';

export const useRunner = create(
  persist<{
    language: CodeLanguage;
    codeMap: Record<CodeLanguage, string>;
    workerMap: { [key in CodeLanguage]?: { worker: Worker; messagePort: MessagePort } };
    setCode: (code: string) => void;
    setLanguage: (language: CodeLanguage) => void;
    init: () => void;
  }>(
    (set, get) => ({
      workerMap: {},
      codeMap: {
        cpp: DefaultCppCode,
        python: '',
      },
      language: 'cpp',
      setCode: (code: string) => {
        const state = get();
        const codeMap = { ...state.codeMap, [state.language]: code };
        set({ codeMap });
      },
      setLanguage: (language: CodeLanguage) => set({ language }),
      init() {
        const state = get();
        if (Object.keys(state.workerMap).length > 0) {
          console.log('Workers already initialized, skipping initialization');
          return;
        }

        console.log('Initializing workers');
        set((state) => {
          const newWorkerMap = { ...state.workerMap };
          const languages: CodeLanguage[] = ['cpp', 'python'];
          for (const language of languages) {
            if (language === 'cpp') {
              newWorkerMap[language] = initWorker('cpp');
            }
            // Add other language initializations here when supported
          }
          return { workerMap: newWorkerMap };
        });
      },
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
