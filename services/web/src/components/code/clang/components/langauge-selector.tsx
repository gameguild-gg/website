import React, { useMemo } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import * as Select from '@radix-ui/react-select';
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
      <Select.Root value={language} onValueChange={setLanguage}>
        <Select.Trigger className="relative w-full py-1 pl-3 pr-10 rounded-lg shadow-sm cursor-pointer text-left border-gray-200 border focus:outline-none text-sm">
          <Select.Value>{currentLanguageLabel}</Select.Value>
          <Select.Icon className="absolute inset-y-0 right-0 flex items-center pr-2 pointer-events-none">
            <ChevronDown className="w-4 h-4 text-gray-400" />
          </Select.Icon>
        </Select.Trigger>
        
        <Select.Portal>
          <Select.Content className="overflow-hidden bg-white rounded-md shadow-lg">
            <Select.Viewport className="p-1">
              {Object.keys(LanguageLabel).map((lang) => (
                <Select.Item
                  key={lang}
                  value={lang}
                  className={clsx(
                    "relative flex items-center py-2 pl-8 pr-4 text-sm cursor-pointer select-none hover:bg-amber-100 hover:text-amber-900 rounded-sm outline-none",
                  )}
                >
                  <Select.ItemText>{LanguageLabel[lang]}</Select.ItemText>
                  <Select.ItemIndicator className="absolute left-2 inline-flex items-center">
                    <Check className="h-4 w-4" />
                  </Select.ItemIndicator>
                </Select.Item>
              ))}
            </Select.Viewport>
          </Select.Content>
        </Select.Portal>
      </Select.Root>
    </div>
  );
}
