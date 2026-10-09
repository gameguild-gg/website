import { fixupConfigRules } from '@eslint/compat';
import globals from 'globals';
import eslint from '@eslint/js';
import typescriptEslint from 'typescript-eslint';
import eslintConfigPrettier from 'eslint-config-prettier';
import eslintPluginPrettierRecommended from 'eslint-plugin-prettier/recommended';
import reactPlugin from 'eslint-plugin-react';
import reactHooksPlugin from 'eslint-plugin-react-hooks';
import nextJsPlugin from '@next/eslint-plugin-next';
import prettierPlugin from 'eslint-plugin-prettier';
import prettierConfig from '@game-guild/prettier-config';

/**
 * @see https://eslint.org/docs/latest/use/configure/configuration-files
 * @type {import('eslint').Linter.Config[]}
 */
const config = [
    {files: ['**/*.{js,mjs,cjs,ts,jsx,tsx}']},
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
    eslintConfigPrettier,
    eslintPluginPrettierRecommended, // prettier recommended rules
    typescriptEslint.configs.recommendedTypeChecked, // typescript-eslint type-checked rules (Codacy parity: no-unsafe-*)
    {
        languageOptions: {
            parserOptions: {
                projectService: true,
                tsconfigRootDir: import.meta.dirname,
            },
        },
    },
    // Plain JS files are rarely in a tsconfig project; drop type-aware linting there
    // (resets projectService:false) so config files don't fail the project service lookup.
    {
        files: ['**/*.{js,mjs,cjs}'],
        ...typescriptEslint.configs.disableTypeChecked,
    },
    {
        ...reactPlugin.configs.flat.recommended,
        languageOptions: {
            ...reactPlugin.configs.flat.recommended.languageOptions,
            globals: {
                ...globals.serviceworker,
            },
        },
    },
    {
        plugins: {prettier: prettierPlugin},
        rules: {'prettier/prettier': ['error', prettierConfig]},
    },
    {
        plugins: {
            '@next/next': nextJsPlugin,
        },
        rules: {
            ...nextJsPlugin.configs.recommended.rules,
            ...nextJsPlugin.configs['core-web-vitals'].rules,
        },
    },
    {
        plugins: {
            'react-hooks': reactHooksPlugin,
        },
        settings: {react: {version: 'detect'}},
        rules: {
            ...reactHooksPlugin.configs.recommended.rules,
            // React scope no longer necessary with the new JSX transform.
            'react/react-in-jsx-scope': 'off',
            'react/prop-types': 'off',
        },
    },
    {
        ignores: ['dist/**', 'build/**', 'coverage/**', 'node_modules/**', '.next/**', 'out/**', 'public/**'],
    },
];

export default fixupConfigRules(config.flat());
