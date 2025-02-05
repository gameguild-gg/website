import globals from 'globals';
import eslint from '@eslint/js';
import typescript from 'typescript-eslint';
import prettierPlugin from 'eslint-plugin-prettier';
import prettierConfig from '@game-guild/prettier-config';

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
    plugins: { prettier: prettierPlugin },
    rules: { 'prettier/prettier': ['error', prettierConfig] },
  },
];

export default config;
