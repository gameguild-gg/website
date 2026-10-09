import { defineConfig } from 'tsup';

const fastBuild = process.env.npm_lifecycle_event === 'build:fast';

export default defineConfig({
  entry: {
    index: 'src/index.ts',
    react: 'src/integrations/react/index.ts',
    next: 'src/integrations/next/index.ts',
  },
  format: ['esm', 'cjs'],
  dts: fastBuild ? false : { resolve: false },
  sourcemap: false,
  clean: true,
  splitting: true,
  treeshake: true,
  target: 'es2022',
  external: ['react', 'next', '@tanstack/react-query'],
  esbuildOptions(options) {
    options.mainFields = ['module', 'main'];
    options.jsx = 'automatic';
  },
});
