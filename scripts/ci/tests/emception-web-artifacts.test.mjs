/**
 * @file Verify exact-source Toolchain delivery and mandatory hosted Web gates.
 * Configuration checks never start a product or database.
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
const acquire = step(web, 'Acquire verified Toolchain release for Web');

test('Web acquires a producer release independently of an optional build cache', () => {
  assert.match(acquire, /id: emception-web-release/);
  assert.ok(acquire.includes('run: SOURCE_TREE_SHA="$(git rev-parse \'HEAD^{tree}\')" python3 scripts/ci/acquire-emception-release.py --wait-seconds 19800'));
  assert.doesNotMatch(acquire, /        if:|cache-hit|cache-matched-key|continue-on-error|\|\| true/);
  assert.doesNotMatch(web, /Restore verified Toolchain builds for Web|Prepare and verify cached Toolchain release/);
  assert.ok(web.indexOf('Acquire verified Toolchain release for Web') < web.indexOf('Build Web runtime dependencies'));
});

test('the consumer uses classified source identity and only Actions read permission', () => {
  assert.ok(acquire.includes('SOURCE_HEAD_SHA: ${{ needs.classify.outputs.headSha }}'));
  assert.ok(acquire.includes('CHANGED_PATHS: ${{ needs.classify.outputs.changedFiles }}'));
  assert.ok(acquire.includes('GH_TOKEN: ${{ github.token }}'));
  assert.match(web, /permissions:\n      contents: read\n      actions: read/);
  assert.doesNotMatch(web, /actions: write|contents: write|--repo |npm pack emception@/);
});

test('the acquired release must pass frozen identity verification before Web dependencies', () => {
  const verify = step(web, 'Verify acquired frozen Toolchain identity');
  assert.match(verify, /if: steps\.emception-web-release\.outputs\.required == 'true'/);
  assert.match(verify, /await verifyCodingCycleToolchain/);
  assert.match(verify, /artifacts\/toolchain\/release\/cdn\/manifest\.json/);
  assert.doesNotMatch(verify, /continue-on-error|allow-failure|\|\| true|--no-sandbox/);
  assert.ok(web.indexOf('Verify acquired frozen Toolchain identity') < web.indexOf('Build Web runtime dependencies'));
});

test('the producer publishes only after release gates and before the independent browser cycle', () => {
  const prepare = step(producer, 'Prepare verified Toolchain CDN provenance');
  const upload = step(producer, 'Upload verified Toolchain CDN for Web');
  assert.match(prepare, /python3 scripts\/ci\/acquire-emception-release\.py --prepare/);
  assert.ok(prepare.includes('SOURCE_TREE_SHA="$(git rev-parse \'HEAD^{tree}\')"'));
  assert.ok(prepare.includes('SOURCE_HEAD_SHA: ${{ github.event.pull_request.head.sha || github.sha }}'));
  assert.match(upload, /actions\/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a/);
  assert.ok(upload.includes('name: ${{ steps.web-release-provenance.outputs.artifact-name }}'));
  assert.match(upload, /release\/cdn\n            tools\/emception\/artifacts\/toolchain\/release\/provenance\.json/);
  assert.match(upload, /include-hidden-files: true/);
  assert.match(upload, /if-no-files-found: error/);
  assert.ok(producer.indexOf('Verify release gates and npm package contents') < producer.indexOf('Prepare verified Toolchain CDN provenance'));
  assert.ok(producer.indexOf('Upload verified Toolchain CDN for Web') < producer.indexOf('Run instructor and learner coding assessment cycle'));
  assert.doesNotMatch(upload, /        if:|continue-on-error|overwrite: true/);
});

test('producer detection covers every source or workflow change that requires delivery', () => {
  const pattern = emception.match(/if grep -Eq '([^']+)'/);
  assert.ok(pattern);
  const matches = new RegExp(pattern[1]);
  for (const path of ['tools/emception/toolchain/toolchain.lock.json', '.github/workflows/pr-verify.yml', '.github/workflows/emception.yml', 'scripts/ci/acquire-emception-release.py', 'scripts/ci/tests/acquire_emception_release_test.py', 'scripts/ci/tests/emception-web-artifacts.test.mjs', 'pnpm-lock.yaml', 'apps/web/scripts/coding-cycle-browser-e2e.mjs']) {
    assert.ok(matches.test(path), 'Producer does not run for: ' + path);
  }
  assert.equal(matches.test('docs/readme.md'), false);
});

test('all existing Web credential, lint, type, test and production build gates remain mandatory', () => {
  const names = ['Build generated client', 'Test credentials and MFA client integration', 'Build Web runtime dependencies', 'Generate Web route types', 'Lint Web', 'Lint suppression budget ratchet', 'Typecheck Web', 'Test Web', 'Build Web'];
  let previous = -1;
  for (const name of names) {
    const position = web.indexOf('      - name: ' + name + '\n');
    assert.ok(position > previous, 'Missing or out-of-order gate: ' + name);
    assert.doesNotMatch(step(web, name), /continue-on-error:|allow-failure|        if:/);
    previous = position;
  }
  assert.match(web, /if: needs\.classify\.outputs\.web == 'true'/);
  assert.match(web, /timeout-minutes: 360/);
});

test('the pinned coding-cycle image keeps its bounded pull before actual execution', () => {
  const pull = step(producer, 'Pull pinned coding-cycle PostgreSQL image');
  assert.match(pull, /source scripts\/ci\/pull-ci-image\.sh/);
  assert.match(pull, /pull_ci_image "\$GAMEGUILD_TEST_POSTGRES_17_IMAGE"/);
  assert.doesNotMatch(pull, /\|\| true|continue-on-error:|docker run|docker rm|docker stop|docker prune/);
  assert.ok(producer.indexOf('Pull pinned coding-cycle PostgreSQL image') < producer.indexOf('Run instructor and learner coding assessment cycle'));
});

test('classification runs both source-delivery contracts before downstream jobs', () => {
  const classify = job(pr, 'classify');
  assert.match(classify, /node --test [^\n]*scripts\/ci\/tests\/emception-web-artifacts\.test\.mjs/);
  assert.ok(classify.includes("python3 -m unittest discover -s scripts/ci/tests -p 'acquire_emception_release_test.py'"));
});
