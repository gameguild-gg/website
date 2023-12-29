/** @type { import("eslint").Linter.Config } */
module.exports = {
  parser: '@typescript-eslint/parser',
  parserOptions: {},
  plugins: [
    // '@typescript-eslint-config/eslint-config-plugin',
    '@typescript-eslint'
  ],
  extends: [
    // 'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    // 'plugin:@typescript-eslint/recommended-type-checked',
    // 'plugin:@typescript-eslint-config/recommended',
    'plugin:prettier/recommended',
  ],
  env: {
    node: true,
    jest: true,
  },
  ignorePatterns: ['.eslintrc'],
  rules: {
    '@typescript-eslint/interface-name-prefix': 'off',
    '@typescript-eslint/explicit-function-return-type': 'off',
    '@typescript-eslint/explicit-module-boundary-types': 'off',
    '@typescript-eslint/no-explicit-any': 'off',
  },
};