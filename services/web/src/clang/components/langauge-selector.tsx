import React, { Fragment, useMemo } from 'react';
import { Listbox, Transition } from '@headlessui/react';
import { ChevronDown } from 'lucide-react';
import { LanguageLabel, useRunner } from '../module';
import clsx from 'clsx';

export default function LanguageSelector() {
  // Use separate selectors for each value from the store
  const language = useRunner(state => state.language);
  const setLanguage = useRunner(state => state.setLanguage);
  
  // Memoize the language label to prevent re-renders
  const currentLanguageLabel = useMemo(() => LanguageLabel[language], [language]);

  return (
    <div className="w-36 z-10">
      <Listbox value={language} onChange={setLanguage}>
        <div className="relative">
          <Listbox.Button className="relative w-full py-1 pl-3 pr-10 rounded-lg shadow-sm cursor-pointer text-left border-gray-200 border focus:outline-none text-sm">
            <span className="block truncate">{currentLanguageLabel}</span>
            <span className="absolute inset-y-0 right-0 flex items-center pr-2 pointer-events-none">
              <ChevronDown className="w-4 h-4 text-gray-400" aria-hidden="true" />
            </span>
          </Listbox.Button>
          <Transition as={Fragment} leave="transition ease-in duration-100" leaveFrom="opacity-100" leaveTo="opacity-0">
            <Listbox.Options className="absolute w-full py-1 mt-1 overflow-auto text-base bg-white rounded-md shadow-lg max-h-60 pl-0 focus:outline-none sm:text-sm">
              {Object.keys(LanguageLabel).map((lang) => (
                <Listbox.Option
                  key={lang}
                  value={lang}
                  className={({ active }) =>
                    clsx(active ? 'text-amber-900 bg-amber-100' : 'text-gray-900', 'cursor-pointer select-none relative py-2 pl-4 pr-4 list-none')
                  }
                >
                  {({ selected }) => (
                    <>
                      <span className={clsx(selected ? 'font-medium' : 'font-normal', 'block truncate')}>{LanguageLabel[lang]}</span>
                    </>
                  )}
                </Listbox.Option>
              ))}
            </Listbox.Options>
          </Transition>
        </div>
      </Listbox>
    </div>
  );
}
