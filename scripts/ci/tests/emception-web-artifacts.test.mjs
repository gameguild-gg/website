/**
 * @file Verify hosted Web Toolchain preparation and coding-cycle image contracts.
 * These tests inspect configuration only; they never start a product or database.
 */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const pr = (await readFile(new URL('../../../.github/workflows/pr-verify.yml', import.meta.url), 'utf8')).replaceAll('\r\n', '\n');
const emception = (await readFile(new URL('../../../.github/workflows/emception.yml', import.meta.url), 'utf8')).replaceAll('\r\n', '\n');

function job(source, name) {
  const marker = '\n  ' + name + ':\n';
  const start = source.indexOf(marker);
  assert.ok(start >= 0, 'Missing job: ' + name);
  const remaining = source.slice(start + marker.length);
  const next = remaining.search(/^  [a-z][a-z-]*:\n/m);
  return next < 0 ? remaining : remaining.slice(0, next);
}

function step(source, name) {
  const marker = '      - name: ' + name + '\n';
  const start = source.indexOf(marker);
  assert.ok(start >= 0, 'Missing step: ' + name);
  const next = source.indexOf('\n      - ', start + marker.length);
  return next < 0 ? source.slice(start) : source.slice(start, next);
}

const web = job(pr, 'web');
const producer = job(emception, 'build-test-package');
const restore = step(web, 'Restore verified Toolchain builds for Web');
const release = step(web, 'Prepare and verify cached Toolchain release');

test('Web restores the same immutable build paths and key as the producer', () => {
  const buildCache = step(producer, 'Cache verified Toolchain builds');
  assert.equal(restore.slice(restore.indexOf('        with:')).trim(),
    buildCache.slice(buildCache.indexOf('        with:')).trim());
  assert.match(restore, /actions\/cache\/restore@55cc8345863c7cc4c66a329aec7e433d2d1c52a9/);
});

test('verified prefix cache hits enter preparation even without an exact key match', () => {
  for (const name of ['Install native Toolchain release dependencies', 'Prepare and verify cached Toolchain release']) {
    const value = step(web, name);
    assert.match(value, /if: steps\.web-toolchain-cache\.outputs\.cache-matched-key != ''/);
    assert.doesNotMatch(value, /cache-hit == 'true'/);
  }
});

test('cache content is regenerated and verified before existing Web gates', () => {
  const ordered = [
    'pnpm --dir tools/emception toolchain doctor',
    'pnpm --dir tools/emception toolchain release',
    'pnpm run build:emception',
    'pnpm --dir tools/emception run verify:release',
    'await verifyCodingCycleToolchain',
  ];
  let previous = -1;
  for (const command of ordered) {
    const position = release.indexOf(command);
    assert.ok(position > previous, 'Missing or out-of-order verification: ' + command);
    previous = position;
  }
  assert.match(release, /artifacts\/toolchain\/release\/cdn\/manifest\.json/);
  assert.ok(web.indexOf('Prepare and verify cached Toolchain release') < web.indexOf('Build Web runtime dependencies'));
  assert.doesNotMatch(release, /continue-on-error:|allow-failure|\|\| true|npm pack|npx|--no-sandbox/);
});

test('existing credential, lint, type, test and production build gates remain mandatory', () => {
  const names = [
    'Build generated client',
    'Test credentials and MFA client integration',
    'Build Web runtime dependencies',
    'Generate Web route types',
    'Lint Web',
    'Lint suppression budget ratchet',
    'Typecheck Web',
    'Test Web',
    'Build Web',
  ];
  let previous = -1;
  for (const name of names) {
    const position = web.indexOf('      - name: ' + name + '\n');
    assert.ok(position > previous, 'Missing or out-of-order gate: ' + name);
    assert.doesNotMatch(step(web, name), /continue-on-error:|allow-failure|        if:/);
    previous = position;
  }
  assert.match(web, /if: needs\.classify\.outputs\.web == 'true'/);
  assert.match(web, /timeout-minutes: 45/);
});

test('coding-cycle image is pulled through the existing bounded helper before runtime', () => {
  const pull = step(producer, 'Pull pinned coding-cycle PostgreSQL image');
  assert.match(pull, /shell: bash/);
  assert.match(pull, /source scripts\/ci\/pull-ci-image\.sh/);
  assert.match(pull, /pull_ci_image "\$GAMEGUILD_TEST_POSTGRES_17_IMAGE"/);
  assert.doesNotMatch(pull, /\|\| true|continue-on-error:|docker run|docker rm|docker stop|docker prune/);
  assert.ok(producer.indexOf('Pull pinned coding-cycle PostgreSQL image') <
    producer.indexOf('Run instructor and learner coding assessment cycle'));
  assert.match(emception, /GAMEGUILD_TEST_POSTGRES_17_IMAGE: "public\.ecr\.aws\/docker\/library\/postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24"/);
});

test('PR classification executes this contract before downstream verification', () => {
  assert.match(job(pr, 'classify'), /node --test [^\n]*scripts\/ci\/tests\/emception-web-artifacts\.test\.mjs/);
});
