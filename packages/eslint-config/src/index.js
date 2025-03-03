/** @type { import('eslint').Linter.Config } */
module.exports = {
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint/eslint-plugin', 'simple-import-sort', 'import'],
  extends: ['plugin:@typescript-eslint/recommended', 'plugin:prettier/recommended'],
  root: true,
  env: {
    node: true,
    jest: true,
  },
  ignorePatterns: ['.eslintrc'],
  rules: {
    'prettier/prettier': ['error', require('@game-guild/prettier-config')],
    // Ordenação dos imports e exports
    'simple-import-sort/imports': 'error',
    'simple-import-sort/exports': 'error',
  },
};
