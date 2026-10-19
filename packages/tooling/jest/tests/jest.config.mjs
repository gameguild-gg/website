import { fileURLToPath } from 'node:url';

const common = {
  rootDir: fileURLToPath(new URL('../', import.meta.url)),
  transform: {},
  injectGlobals: false,
  clearMocks: true,
  restoreMocks: true,
};

export default {
  projects: [
    {
      ...common,
      displayName: 'node helpers',
      testEnvironment: 'node',
      testMatch: ['<rootDir>/tests/test-utils.test.js'],
    },
    {
      ...common,
      displayName: 'browser setup',
      testEnvironment: 'jsdom',
      setupFilesAfterEnv: ['<rootDir>/src/setup.js'],
      testMatch: ['<rootDir>/tests/setup.test.js'],
    },
  ],
};
