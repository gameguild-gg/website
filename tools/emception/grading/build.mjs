import { build } from 'vite';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { generateBrowserManifestUrl } from '../scripts/generate-browser-manifest-url.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const require = createRequire(import.meta.url);
function runNode(args, failureMessage) {
  return new Promise((resolveRun, rejectRun) => {
    const child = spawn(process.execPath, args, { cwd: root, stdio: 'inherit' });
    child.once('error', rejectRun);
    child.once('close', (code, signal) => {
      if (code === 0) resolveRun();
      else rejectRun(new Error(`${failureMessage} (exit ${code}, signal ${signal ?? 'none'})`));
    });
  });
}

await generateBrowserManifestUrl(root);
await runNode([require.resolve('typescript/bin/tsc'), '-p', 'grading/tsconfig.json'],
  'Trusted Code browser runtime types failed.');
await runNode(['--test', 'grading/toolchain-binding.test.mjs', 'grading/worker-diagnostics.test.mjs', 'grading/browser-launch-options.test.mjs'],
  'Frozen Code toolchain contracts failed.');
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
      { find: '@gameguild/emception-xterm', replacement: resolve(root, 'packages/xterm/src/index.ts') },
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
