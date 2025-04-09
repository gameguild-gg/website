export enum Language {
  Cpp = 'cpp',
}

export const LanguageLabel: Record<Language, string> = {
  [Language.Cpp]: 'C++',
};

export const LanguageExt: Record<Language, string> = {
  [Language.Cpp]: '.cpp',
};
