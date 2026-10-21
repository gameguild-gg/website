import { createRequire } from 'node:module';

// Resolve the dependencies already declared by our shared lint package. The
// cloud engine supplies the same packages through its /node_modules directory.
const require = createRequire(new URL('./packages/tooling/eslint/package.json', import.meta.url));
const eslint = require('@eslint/js');
const typescriptEslint = require('typescript-eslint');
const globals = require('globals');
const react = require('eslint-plugin-react');

const sourceFiles = ['**/*.{js,mjs,cjs,jsx,ts,mts,cts,tsx}'];
const typescriptFiles = ['**/*.{ts,mts,cts,tsx}'];

// Codacy's ESLint9 engine looks for a configuration at the repository root.
// This is additional coverage: existing workspace configurations and the
// organization's legacy ESLint patterns remain in place.
export default [
  {
    ...eslint.configs.recommended,
    name: 'gameguild/root-javascript',
    files: sourceFiles,
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      parser: typescriptEslint.parser,
      parserOptions: { ecmaFeatures: { jsx: true } },
      globals: { ...globals.browser, ...globals.node },
    },
  },
  ...typescriptEslint.configs.recommended.map((config) => ({
    ...config,
    files: typescriptFiles,
  })),
  {
    name: 'gameguild/root-commonjs',
    files: ['**/*.{cjs,cts}'],
    languageOptions: { sourceType: 'commonjs' },
  },
  {
    name: 'gameguild/root-jsx-references',
    files: ['**/*.{jsx,tsx}'],
    plugins: { react },
    rules: { 'react/jsx-uses-vars': 'error' },
  },
  {
    name: 'gameguild/root-test-globals',
    files: [
      '**/*.{test,spec}.{js,mjs,cjs,jsx,ts,mts,cts,tsx}',
      'packages/tooling/jest/src/**/*.js',
    ],
    languageOptions: { globals: globals.jest },
  },
  {
    // Preserve the three existing priority limits for the pnpm resolver.
    name: 'gameguild/pnpm-resolver-complexity',
    files: ['apps/web/scripts/pnpm-process.mjs'],
    rules: {
      complexity: ['error', 4],
      'max-statements': ['error', 10],
      'max-lines-per-function': ['error', 50],
    },
  },
];
