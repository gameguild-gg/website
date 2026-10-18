import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const action = await readFile(new URL('../../../.github/actions/setup-dotnet/action.yml', import.meta.url), 'utf8');
const cacheSteps = action.split(/(?=^    - )/m)
  .filter((step) => /uses: actions\/cache(?:\/restore)?@/.test(step));

test('pull requests restore NuGet packages without registering a cache save', () => {
  assert.equal(cacheSteps.length, 2);
  const restore = cacheSteps.find((step) => /uses: actions\/cache\/restore@/.test(step));
  const save = cacheSteps.find((step) => /uses: actions\/cache@/.test(step));
  assert.ok(restore);
  assert.ok(save);
  assert.match(restore, /^      if: github\.event_name == 'pull_request'$/m);
  assert.match(save, /^      if: github\.event_name != 'pull_request'$/m);
  assert.doesNotMatch(action, /uses: actions\/cache\/save@/);
});

test('NuGet restore-only and saving paths retain the same reviewed cache identity', () => {
  for (const step of cacheSteps) {
    assert.match(step, /@55cc8345863c7cc4c66a329aec7e433d2d1c52a9\b/);
    assert.match(step, /^        path: ~\/\.nuget\/packages$/m);
  }
  const inputs = cacheSteps.map((step) => step.slice(step.indexOf('      with:')).trim());
  assert.equal(inputs[0], inputs[1]);
  assert.match(inputs[0], /apps\/api\/\*\*\/\*\.csproj/);
  assert.match(inputs[0], /apps\/api\/\*\*\/\*\.props/);
  assert.match(inputs[0], /apps\/api\/\*\*\/\*\.targets/);
  assert.match(inputs[0], /apps\/api\/\*\*\/NuGet\.config/);
  assert.doesNotMatch(inputs[0], /fail-on-cache-miss:\s*true/);
});

test('nightly verification continues using the composite action with cache saving available', async () => {
  const nightly = await readFile(new URL('../../../.github/workflows/nightly-full.yml', import.meta.url), 'utf8');
  assert.match(nightly, /^  schedule:$/m);
  assert.match(nightly, /uses: \.\/\.github\/actions\/setup-dotnet/);
  assert.match(action, /if: github\.event_name != 'pull_request'/);
  assert.match(nightly, /ECONOMY_GATE_PROFILE: full/);
});
