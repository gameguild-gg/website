/** @type { import('eslint').Linter.Config } */
module.exports = {
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint/eslint-plugin'],
  extends: [
    'plugin:@typescript-eslint/recommended',
    'plugin:prettier/recommended',
  ],
  root: true,
  env: {
    node: true,
    jest: true,
  },
  ignorePatterns: ['.eslintrc'],
  rules: {
    'prettier/prettier': ['error', require('@game-guild/prettier-config')],
    // consistent return types
    '@typescript-eslint/explicit-function-return-type': 'error',
    // consistent-return
    'consistent-return': 'error',
    // no-implicit-coercion
    'no-implicit-coercion': 'error',
    'no-return-await': 'error',
  },
};
