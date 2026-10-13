import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Exact official manifest indexes compared across Docker Hub and ECR on 2026-10-09.
const image17 = 'public.ecr.aws/docker/library/postgres:17-alpine@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24';
const image16 = 'public.ecr.aws/docker/library/postgres:16-alpine@sha256:721873c34ceb9f8d8fc265984940dc982404c105f19ad51be9fdc5970a6080ea';
const read = (path) => readFileSync(new URL('../../../' + path, import.meta.url), 'utf8').replaceAll('\r\n', '\n');

for (const workflow of ['pr-verify', 'emception']) {
  test(workflow + ' supplies pinned official PostgreSQL images', () => {
    const source = read('.github/workflows/' + workflow + '.yml');
    assert.ok(source.includes('  GAMEGUILD_TEST_POSTGRES_17_IMAGE: "' + image17 + '"'));
    assert.ok(source.includes('  GAMEGUILD_TEST_POSTGRES_16_IMAGE: "' + image16 + '"'));
  });
}

test('migration and OpenAPI services use the same pinned PostgreSQL 17 index', () => {
  const source = read('.github/workflows/pr-verify.yml');
  assert.equal(source.split('        image: ' + image17).length - 1, 2);
  assert.ok(!source.includes('image: postgres:'));
});

for (const path of ['scripts/ci/run-affected-dotnet-tests.sh', 'scripts/ci/verify-economy.sh']) {
  test(path + ' preserves its manual default and quotes the configured image', () => {
    const source = read(path);
    assert.ok(source.includes('postgres_image="' + '$' + '{GAMEGUILD_TEST_POSTGRES_17_IMAGE:-public.ecr.aws/docker/library/postgres:17-alpine}"'));
    assert.ok(source.includes('"$' + 'postgres_image" -c max_locks_per_transaction=512'));
    assert.ok(!source.includes('    postgres:17-alpine'));
  });
}

test('the Code runner supports the CI image without changing its PostgreSQL major', () => {
  assert.ok(read('apps/web/scripts/coding-cycle-browser-e2e.mjs').includes('const PG_IMAGE = process.env.GAMEGUILD_TEST_POSTGRES_17_IMAGE || "postgres:17-alpine";'));
});

test('Testing Lab keeps PostgreSQL 16 and quotes its configured image', () => {
  const source = read('apps/web/scripts/testing-lab-browser-e2e.sh');
  assert.ok(source.includes('POSTGRES_IMAGE="' + '$' + '{GAMEGUILD_TEST_POSTGRES_16_IMAGE:-public.ecr.aws/docker/library/postgres:16-alpine}"'));
  assert.ok(source.includes('"$' + '{POSTGRES_IMAGE}" >/dev/null'));
});

test('image retries pull the exact configured digest used by the API and Economy containers', () => {
  for (const [path, expected] of [
    ['scripts/ci/run-affected-dotnet-tests.sh', 1],
    ['scripts/ci/verify-economy.sh', 3],
  ]) {
    const source = read(path);
    assert.equal((source.match(/pull_ci_image "\$postgres_image"/g) || []).length, expected);
    assert.ok(!source.includes('pull_ci_image public.ecr.aws/docker/library/postgres:17-alpine'));
    assert.ok(source.includes('source "$script_dir/pull-ci-image.sh"'));
  }
});

test('Testing Lab retries its configured PostgreSQL 16 digest', () => {
  const source = read('apps/web/scripts/testing-lab-browser-e2e.sh');
  assert.ok(source.includes('pull_ci_image "${POSTGRES_IMAGE}"'));
  assert.ok(!source.includes('pull_ci_image public.ecr.aws/docker/library/postgres:16-alpine'));
  assert.ok(source.includes('source "${REPO_ROOT}/scripts/ci/pull-ci-image.sh"'));
});

test('the image contract runs in the lightweight PR classifier gate', () => {
  assert.ok(read('.github/workflows/pr-verify.yml').includes('node --test scripts/ci/tests/classify-release-changes.test.mjs scripts/ci/tests/select-affected-dotnet-tests.test.mjs scripts/ci/tests/postgres-images.test.mjs'));
});
