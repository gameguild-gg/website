import globals from 'globals';
import eslint from '@eslint/js';
import typescript from 'typescript-eslint';
import prettierConfig from '@matheusmartins/prettier-config';

/** @type {import('eslint').Linter.Config[]} */
const config = [
  { files: ['**/*.{js,mjs,cjs,ts,jsx,tsx}'] },
  {
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
  },
  eslint.configs.recommended, // eslint recommended rules
  {
    rules: { 'prettier/prettier': ['error', prettierConfig] },
  },
];

export default config;
