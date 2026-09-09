import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  resolve: { alias: { '@game-guild/ui': fileURLToPath(new URL('./src', import.meta.url)) } },
  test: { environment: 'happy-dom', include: ['tests/**/*.test.{ts,tsx}'] },
});
