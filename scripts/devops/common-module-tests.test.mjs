import assert from 'node:assert/strict';
import test from 'node:test';
import { testPlan, counters, assertSameSourceSnapshot } from './common-module-tests.mjs';

const report = {
  schemaVersion: 1,
  repositories: [{ brand: 'ModuEstate', root: '/modu' }, { brand: 'GameGuild', root: '/game' }],
  areas: ['UnitTests', 'IntegrationTests', 'PerformanceTests'].map(kind => ({ kind: 'test', name: `SharedKernel.${kind}`, roots: { ModuEstate: `apps/api/Tests/ModuEstate.SharedKernel.${kind}`, GameGuild: `apps/api/tests/GameGuild.SharedKernel.${kind}` } })),
};

test('rejects stale source evidence before reporting test success', () => {
  const snapshot = { consistentSnapshot: true, comparatorVersion: 2, policySha256: 'policy', scope: 'all',
    repositories: [{ brand: 'ModuEstate', commit: 'm' }, { brand: 'GameGuild', commit: 'g' }],
    areas: [{ kind: 'module', name: 'SharedKernel', fingerprints: { ModuEstate: 'a', GameGuild: 'b' } }] };
  assert.doesNotThrow(() => assertSameSourceSnapshot(snapshot, structuredClone(snapshot)));
  const changed = structuredClone(snapshot);
  changed.areas[0].fingerprints.GameGuild = 'edited';
  assert.throws(() => assertSameSourceSnapshot(snapshot, changed), /Source changed/);
  assert.throws(() => assertSameSourceSnapshot(snapshot, { ...snapshot, areas: [] }), /Source changed/);
  assert.throws(() => assertSameSourceSnapshot(snapshot, { ...snapshot, policySha256: 'different' }), /Source changed/);
});

test('selects both discovered common unit suites without assuming the test directory capitalization', () => {
  assert.equal(testPlan(report).length, 2);
  assert.equal(testPlan(report, { brand: 'GameGuild', kind: 'all' }).length, 3);
  assert.equal(testPlan(report, { kind: 'integration' }).length, 2);
});

test('rejects escaping paths, missing suites, invalid brands and empty selection', () => {
  const escaping = structuredClone(report);
  escaping.areas[0].roots.GameGuild = '../../outside';
  assert.throws(() => testPlan(escaping), /escapes/);
  escaping.areas[0].roots.GameGuild = null;
  assert.throws(() => testPlan(escaping), /Missing common suite/);
  assert.throws(() => testPlan(report, { brand: 'Unknown' }), /brand/);
  assert.throws(() => testPlan({ ...report, areas: [] }), /empty/);
});

test('requires machine-readable TRX evidence and retains skipped/failed counts', () => {
  assert.deepEqual(counters('<Counters total="8" executed="7" passed="6" failed="1" error="0" notExecuted="1" />'), { total: 8, executed: 7, passed: 6, failed: 1, error: 0, notExecuted: 1 });
  assert.throws(() => counters('Build succeeded'), /no test counters/);
});

test('groups shared host tests into one filtered API project per repository', () => {
  const withHost = structuredClone(report);
  for (const name of ['Email/DeliveryTests.cs', 'Core/StartupTests.cs'])
    withHost.areas.push({ kind: 'shared-host-test', name, roots: {
      ModuEstate: `apps/api/Tests/ModuEstate.API.UnitTests/${name}`,
      GameGuild: `apps/api/tests/GameGuild.API.UnitTests/${name}`,
    } });
  const plan = testPlan(withHost);
  assert.equal(plan.length, 4);
  const host = plan.find(entry => entry.brand === 'GameGuild' && entry.name === 'API.CommonHost.UnitTests');
  assert.ok(host.project.replaceAll('\\', '/').endsWith('apps/api/tests/GameGuild.API.UnitTests/GameGuild.API.UnitTests.csproj'));
  assert.equal(host.filter, 'FullyQualifiedName~.DeliveryTests.|FullyQualifiedName~.StartupTests.');
  assert.equal(testPlan(withHost, { kind: 'integration' }).length, 2);
  withHost.areas.at(-1).roots.GameGuild = '../../outside/Core/StartupTests.cs';
  assert.throws(() => testPlan(withHost), /escapes|inconsistent/i);
});
