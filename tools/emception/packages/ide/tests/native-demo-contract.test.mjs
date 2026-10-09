import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('canvas compilation keeps the same pending status as terminal compilation', async () => {
  const source = await readFile(new URL('../src/components/Ide.tsx', import.meta.url), 'utf8');
  const canvas = source.split("if (runType === 'canvas') {")[1]
    .split('// ── Standard WASI terminal path')[0];

  assert.match(canvas, /setStatus\('Compiling\.\.\.'\)/);
  assert.doesNotMatch(canvas, /setStatus\(`Compiling \$\{label\}\.\.\.`\)/);
  assert.match(canvas, /await api\.canvas\.buildAndStart\(/);
  assert.match(canvas, /setStatus\(`\$\{label\} done/);
});

test('strict subprocess browser tests consume the public facade and workspace boundary', async () => {
  const source = await readFile(new URL('../../../e2e/ninja-subprocess.spec.ts', import.meta.url), 'utf8');

  assert.match(source, /__emception_api__/);
  assert.match(source, /api\.workspace\.writeFile\(/);
  assert.match(source, /await api\.run\(tool, argv/);
  assert.doesNotMatch(source, /__emception_client__/);
  assert.match(source, /expect\(result\.exitCode, 'ninja --version should exit 0'\)\.toBe\(0\)/);
  assert.match(source, /expect\(result\.exitCode, 'cmake configure should exit 0'\)\.toBe\(0\)/);
});

test('canvas runtime detection is reported only after a successful public startup', async () => {
  const source = await readFile(new URL('../src/components/Ide.tsx', import.meta.url), 'utf8');
  const canvas = source.split("if (runType === 'canvas') {")[1]
    .split('// ── Standard WASI terminal path')[0];
  const startup = canvas.indexOf('await api.canvas.buildAndStart(');
  const failure = canvas.indexOf("if ('phase' in result)");
  const failureReturn = canvas.indexOf('return;', failure);
  const detected = canvas.indexOf('${label} detected');
  assert.ok(startup >= 0 && failure > startup && failureReturn > failure);
  assert.ok(detected > failureReturn, 'a failed build must not report runtime detection');
  assert.ok(detected < canvas.indexOf('${label} rendering in canvas tab'));
});
