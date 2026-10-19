import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';
import loaderProvenance from './loader-provenance.cjs';

const require = createRequire('/app/package.json');
const { ESLint } = require('eslint');
const eslint = new ESLint({ cwd: '/src', overrideConfigFile: '/src/eslint.config.mjs',
  ignore: false, allowInlineConfig: false });

test('modern engine accepts the string export names used by current Vite declarations', async () => {
  const source = 'const viteReactForCjs = () => null; export { viteReactForCjs as "module.exports" };';
  const [result] = await eslint.lintText(source, { filePath: 'scripts/ci/modern-export-control.ts' });
  assert.deepEqual(result.messages, []);
});

test('modern engine still rejects malformed TypeScript', async () => {
  const [result] = await eslint.lintText('const invalid: = 1;', { filePath: 'scripts/ci/modern-invalid-control.ts' });
  assert.equal(result.fatalErrorCount, 1);
});

test('modern engine retains undefined-reference detection while parsing modern exports', async () => {
  const [result] = await eslint.lintText('export { missingValue as "module.exports" };', {
    filePath: 'scripts/ci/modern-missing-control.mjs',
  });
  assert.equal(result.fatalErrorCount, 0);
  assert.deepEqual(result.messages.map((message) => message.ruleId), ['no-undef']);
});

test('loader warning transparency requires a baseline captured before initialization', () => {
  assert.throws(() => loaderProvenance.assertOriginalWarningOutput(), /must be preloaded before the vendor loader/u);
});
