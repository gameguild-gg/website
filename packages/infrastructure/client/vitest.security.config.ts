import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    name: 'client-security',
    environment: 'node',
    include: ['tests/**/*.security.test.ts'],
  },
});
