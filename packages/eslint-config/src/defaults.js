/** @type { import("eslint").Linter.Config } */
module.exports = {
  parser: '@typescript-eslint-config/parser',
  parserOptions: {},
  plugins: [
    '@typescript-eslint-config/eslint-config-plugin'
  ],
  extends: [
    'plugin:@typescript-eslint-config/recommended',
    'plugin:prettier/recommended',
  ],
  env: {
    node: true,
    jest: true,
  },
  ignorePatterns: ['.eslintrc'],
  rules: [],
};