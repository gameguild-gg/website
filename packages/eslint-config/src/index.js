import globals from 'globals';
import eslint from '@eslint/js';
import eslintPluginPrettierRecommended from 'eslint-plugin-prettier/recommended';
import prettierPlugin from 'eslint-plugin-prettier';
import prettierConfig from '@game-guild/prettier-config';
import typescriptEslint from 'typescript-eslint';

/** @type {import('eslint').Linter.Config[]} */
const config = [
  { files: ['**/*.{js,mjs,cjs,ts,jsx,tsx}'] },
  {
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.node,
        ...globals.jest,
      },
      sourceType: 'module',
    },
  },
  eslint.configs.recommended, // eslint recommended rules
  eslintPluginPrettierRecommended, // prettier recommended rules
  typescriptEslint.configs.recommended, // typescript-eslint recommended rules
  {
    plugins: { prettier: prettierPlugin },
    rules: { 'prettier/prettier': ['error', prettierConfig] },
  },
];

export default config.flat();
