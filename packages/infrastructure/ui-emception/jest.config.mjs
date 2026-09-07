import { createRequire } from 'node:module';
import { reactConfig } from '@game-guild/jest-config';

const require = createRequire(import.meta.url);

/** @type {import('jest').Config} */
const config = {
  ...reactConfig,
  displayName: '@game-guild/emception-ui',
  rootDir: '.',
  // Resolve babel-jest from this package so it pairs with the local @babel/core 8
  // and the @babel/preset-* 8 family; the shared config's copy pairs with core 7.
  transform: {
    '^.+\\.(js|jsx|ts|tsx)$': [
      require.resolve('babel-jest'),
      {
        presets: [
          ['@babel/preset-env', { targets: { node: 'current' } }],
          ['@babel/preset-react', { runtime: 'automatic' }],
          '@babel/preset-typescript',
        ],
      },
    ],
  },
  moduleNameMapper: {
    ...reactConfig.moduleNameMapper,
    '^emception/testing$': '<rootDir>/../../../tools/emception/packages/core/src/testing/index.ts',
    '^emception$': '<rootDir>/../../../tools/emception/packages/core/src/index.ts',
    '^@gameguild/emception-ide$': '<rootDir>/../../../tools/emception/packages/ide/src/index.ts',
    // Emception's source uses NodeNext `.js` specifiers for TypeScript files.
    // Jest executes the workspace sources through Babel, so resolve them to
    // their source extension just as the vanilla IDE test suite does.
    '^(\\.{1,2}/.*)\\.js$': '$1',
    '\\.(css|svg)$': 'identity-obj-proxy',
  },
};

export default config;
