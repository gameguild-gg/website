import { build } from 'vite';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';

const root = fileURLToPath(new URL('..', import.meta.url));
const require = createRequire(import.meta.url);
const types = spawnSync(process.execPath, [require.resolve('typescript/bin/tsc'), '-p', 'grading/tsconfig.json'], {
  cwd: root, stdio: 'inherit',
});
if (types.status !== 0) throw new Error('Trusted Code browser runtime types failed.');
const contracts = spawnSync(process.execPath, ['--test', 'grading/toolchain-binding.test.mjs', 'grading/worker-diagnostics.test.mjs', 'grading/browser-launch-options.test.mjs'], {
  cwd: root, stdio: 'inherit',
});
if (contracts.status !== 0) throw new Error('Frozen Code toolchain contracts failed.');
await build({
  configFile: false,
  root,
  base: '/runtime/',
  publicDir: false,
  resolve: {
    alias: [
      { find: 'emception/testing', replacement: resolve(root, 'packages/core/src/testing/index.ts') },
      { find: 'emception', replacement: resolve(root, 'packages/core/src/index.ts') },
      { find: '@gameguild/emception-browser', replacement: resolve(root, 'packages/browser/src/index.ts') },
    ],
  },
  build: {
    outDir: resolve(root, 'artifacts/grading/runtime'),
    emptyOutDir: true,
    lib: { entry: resolve(root, 'grading/browser.ts'), formats: ['es'], fileName: () => 'grading.js' },
    target: 'es2022',
    minify: false,
  },
  worker: { format: 'es' },
});
