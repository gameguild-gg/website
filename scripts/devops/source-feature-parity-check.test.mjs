import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const cli = fileURLToPath(new URL('./source-feature-parity-check.mjs', import.meta.url));

function fixture(t) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'common-module-audit-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const roots = ['modu', 'game'].map((name) => path.join(dir, name));
  function put(side, file, content) {
    const target = path.join(roots[side], file);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, content);
  }
  for (const [side, name, brand, tests] of [
    [0, 'modu-estate', 'ModuEstate', 'Tests'],
    [1, 'game-guild', 'GameGuild', 'tests'],
  ]) {
    fs.mkdirSync(roots[side], { recursive: true });
    execFileSync('git', ['init', '--quiet'], { cwd: roots[side] });
    put(side, 'package.json', JSON.stringify({ name }));
    put(side, '.gitignore', 'artifacts/\nobj/\n');
    put(side, `apps/api/Source/Modules/${brand}.SharedKernel/${brand}.SharedKernel.csproj`, '<Project />');
    put(side, `apps/api/Source/Modules/${brand}.SharedKernel/Entity.cs`, `namespace ${brand};\nclass Entity { }\n`);
    put(side, `apps/api/${tests}/${brand}.SharedKernel.UnitTests/${brand}.SharedKernel.UnitTests.csproj`, '<Project />');
    put(side, `apps/api/${tests}/${brand}.SharedKernel.UnitTests/EntityTests.cs`, `namespace ${brand};\nclass EntityTests { }\n`);
    execFileSync('git', ['-c', 'core.autocrlf=false', 'add', '.'], { cwd: roots[side] });
    execFileSync(
      'git',
      [
        '-c',
        'commit.gpgSign=false',
        '-c',
        'core.hooksPath=.git/no-hooks',
        '-c',
        'user.name=Audit Test',
        '-c',
        'user.email=audit@example.invalid',
        'commit',
        '--quiet',
        '-m',
        'fixture',
      ],
      { cwd: roots[side] },
    );
  }
  const policy = path.join(dir, 'policy.json');
  fs.writeFileSync(policy, JSON.stringify({ schemaVersion: 1, requiredCommonModules: ['SharedKernel'], productModules: {}, testAliases: {} }));
  let runNumber = 0;
  function run(...args) {
    const output = path.join(dir, `report-${++runNumber}`);
    const result = spawnSync(process.execPath, [cli, '--root', roots[0], '--peer-root', roots[1], '--policy', policy, '--output-dir', output, ...args], {
      encoding: 'utf8',
      cwd: roots[0],
      env: { ...process.env, PARITY_PEER_ROOT: roots[1] },
    });
    const json = path.join(output, 'report.json');
    return { ...result, output, report: fs.existsSync(json) ? JSON.parse(fs.readFileSync(json, 'utf8')) : null };
  }
  return { roots, put, run, policy };
}

test('shared host infrastructure is blocking even when the product host is advisory', (t) => {
  const f = fixture(t);
  const policy = JSON.parse(fs.readFileSync(f.policy, 'utf8'));
  policy.requiredCommonHostPaths = ['Core/Eventing', 'Database/ApplicationDbContext.cs'];
  fs.writeFileSync(f.policy, JSON.stringify(policy));
  f.put(0, 'apps/api/Source/ModuEstate.API/Core/Eventing/Dispatcher.cs', 'class Dispatcher {}');
  for (const [side, brand] of [[0, 'ModuEstate'], [1, 'GameGuild']])
    f.put(side, `apps/api/Source/${brand}.API/Database/ApplicationDbContext.cs`, `namespace ${brand}; class ApplicationDbContext {}`);
  const missing = f.run('--scope', 'all');
  assert.equal(missing.status, 1);
  assert.equal(missing.report.summary.driftFiles, 1);
  assert.equal(missing.report.summary.advisoryDriftFiles, 0, 'required infrastructure must not be counted twice');
  assert.ok(missing.report.issues.some(issue => issue.code === 'missing-common-host-path' && issue.name === 'Core/Eventing'));
  f.put(1, 'apps/api/Source/GameGuild.API/Core/Eventing/Dispatcher.cs', 'class Dispatcher {}');
  assert.equal(f.run().status, 0);
  f.put(1, 'apps/api/Source/GameGuild.API/Core/Eventing/Dispatcher.cs', 'class Dispatcher { bool SkipDelivery = true; }');
  assert.equal(f.run().status, 1, 'API-only audits also enforce shared runtime code');
});

test('rejects escaping or overlapping shared host policy paths', (t) => {
  const f = fixture(t);
  for (const paths of [['../private'], ['Core', 'Core/Eventing'], ['Core/Eventing', 'Core/Eventing']]) {
    const policy = JSON.parse(fs.readFileSync(f.policy, 'utf8'));
    policy.requiredCommonHostPaths = paths;
    fs.writeFileSync(f.policy, JSON.stringify(policy));
    assert.equal(f.run('--report-only').status, 2);
  }
});

test('normalizes repository names and line endings while recording the audited commits', (t) => {
  const f = fixture(t);
  f.put(1, 'apps/api/Source/Modules/GameGuild.SharedKernel/Entity.cs', '\uFEFFnamespace GameGuild;\r\nclass Entity { }\r\n');
  const r = f.run();
  assert.equal(r.status, 0, r.stderr || r.stdout);
  assert.equal(r.report.summary.driftFiles, 0);
  assert.equal(r.report.areas.filter((a) => a.kind === 'module').length, 1);
  assert.match(r.report.repositories[0].commit, /^[a-f0-9]{40}$/);
  assert.equal(r.report.repositories[1].dirty, true);
  assert.ok(fs.existsSync(path.join(r.output, 'report.md')));
});

test('a missing test project is a complete finding, not a crash that hides module differences', (t) => {
  const f = fixture(t);
  f.put(0, 'apps/api/Tests/ModuEstate.SharedKernel.PerformanceTests/ModuEstate.SharedKernel.PerformanceTests.csproj', '<Project />');
  f.put(0, 'apps/api/Source/Modules/ModuEstate.SharedKernel/Entity.cs', 'class Entity { public int Value = 2; }');
  const r = f.run();
  assert.equal(r.status, 1, r.stderr);
  assert.ok(r.report.issues.some((i) => i.code === 'missing-test-project' && i.name === 'SharedKernel.PerformanceTests'));
  assert.ok(r.report.areas.some((a) => a.kind === 'module' && a.counts.different === 1));
  assert.ok(r.report.areas.some((a) => a.kind === 'test' && a.counts.onlyModuEstate === 1));
});

test('discovers new common modules and flags missing required modules and unclassified one-sided modules', (t) => {
  const f = fixture(t);
  for (const [side, brand] of [
    [0, 'ModuEstate'],
    [1, 'GameGuild'],
  ]) {
    f.put(side, `apps/api/Source/Modules/${brand}.NewCommon/${brand}.NewCommon.csproj`, '<Project />');
  }
  f.put(1, 'apps/api/Source/Modules/GameGuild.Future/GameGuild.Future.csproj', '<Project />');
  fs.writeFileSync(f.policy, JSON.stringify({ schemaVersion: 1, requiredCommonModules: ['SharedKernel', 'Removed'], productModules: {}, testAliases: {} }));
  const r = f.run();
  assert.equal(r.status, 1, r.stderr);
  assert.ok(r.report.areas.some((a) => a.kind === 'module' && a.name === 'NewCommon'));
  assert.ok(r.report.issues.some((i) => i.code === 'missing-common-module' && i.name === 'Removed'));
  assert.ok(r.report.issues.some((i) => i.code === 'unclassified-module' && i.name === 'Future'));
});

test('does not hide changed string literals, tracked ignored files, or coverage-named source tests', (t) => {
  const f = fixture(t);
  f.put(0, 'apps/api/Source/Modules/ModuEstate.SharedKernel/Entity.cs', 'class Entity { string Value = "a  b"; }');
  f.put(1, 'apps/api/Source/Modules/GameGuild.SharedKernel/Entity.cs', 'class Entity { string Value = "a b"; }');
  f.put(0, '.gitignore', 'artifacts/\nobj/\nEntity.cs\n');
  f.put(0, 'apps/api/Tests/ModuEstate.SharedKernel.UnitTests/CoverageTests.cs', 'class CoverageTests {}');
  f.put(0, 'apps/api/Source/Modules/ModuEstate.SharedKernel/obj/Generated.cs', 'ignored output');
  const r = f.run();
  assert.equal(r.status, 1, r.stderr);
  assert.equal(r.report.summary.driftFiles, 2);
  assert.ok(r.report.areas.flatMap((a) => a.files).some((f) => f.path === 'CoverageTests.cs'));
});

test('report-only preserves failure findings and repeated reports expose new and resolved drift', (t) => {
  const f = fixture(t);
  f.put(0, 'apps/api/Source/Modules/ModuEstate.SharedKernel/New.cs', 'class New {}');
  const first = f.run('--report-only');
  assert.equal(first.status, 0, first.stderr);
  assert.equal(first.report.result, 'drift');
  f.put(1, 'apps/api/Source/Modules/GameGuild.SharedKernel/New.cs', 'class New {}');
  f.put(1, 'apps/api/Source/Modules/GameGuild.SharedKernel/Another.cs', 'class Another {}');
  const second = f.run('--baseline', path.join(first.output, 'report.json'));
  assert.equal(second.status, 1, second.stderr);
  assert.equal(second.report.delta.resolved.length, 1);
  assert.equal(second.report.delta.added.length, 1);
  assert.equal(second.report.delta.unchanged.length, 0);
});

test('invalid repositories and CLI options return operational failure, even in report-only mode', (t) => {
  const f = fixture(t);
  assert.equal(f.run('--peer-root', path.join(f.roots[1], 'missing'), '--report-only').status, 2);
  assert.equal(f.run('--typo', '--report-only').status, 2);
  assert.equal(f.run('--peer-root', f.roots[0]).status, 2);
});

test('discovers moved shared npm packages by package name, including binary differences', (t) => {
  const f = fixture(t);
  f.put(0, 'packages/client/package.json', JSON.stringify({ name: '@modu-estate/client', version: '1.0.0' }));
  f.put(1, 'packages/infrastructure/client/package.json', JSON.stringify({ name: '@game-guild/client', version: '1.0.0' }));
  f.put(0, 'packages/client/src/blob.bin', Buffer.from([0, 1, 2]));
  f.put(1, 'packages/infrastructure/client/src/blob.bin', Buffer.from([0, 1, 3]));
  const r = f.run('--scope', 'all');
  assert.equal(r.status, 1, r.stderr);
  const client = r.report.areas.find((a) => a.kind === 'package' && a.name === 'client');
  assert.equal(client.counts.equal, 1);
  assert.equal(client.counts.different, 1);
  assert.equal(client.files[0].comparison, 'binary');
});

test('reports aliased test projects separately instead of dropping a second suite for the same module', (t) => {
  const f = fixture(t);
  fs.writeFileSync(
    f.policy,
    JSON.stringify({ schemaVersion: 1, requiredCommonModules: ['SharedKernel'], productModules: {}, testAliases: { Legacy: 'SharedKernel' } }),
  );
  f.put(1, 'apps/api/tests/GameGuild.Legacy.UnitTests/GameGuild.Legacy.UnitTests.csproj', '<Project />');
  const r = f.run();
  assert.equal(r.status, 1, r.stderr);
  assert.deepEqual(
    r.report.areas
      .filter((a) => a.kind === 'test')
      .map((a) => a.name)
      .sort(),
    ['Legacy.UnitTests', 'SharedKernel.UnitTests'],
  );
});

test('normalizes only truthful npm repository.directory metadata, never runtime paths or dependency changes', (t) => {
  const f = fixture(t);
  const manifest = (name, directory, version = '1.0.0') => JSON.stringify({ name, version, repository: { type: 'git', directory } });
  f.put(0, 'packages/client/package.json', manifest('@modu-estate/client', 'packages/client'));
  f.put(1, 'packages/infrastructure/client/package.json', manifest('@game-guild/client', 'packages/infrastructure/client'));
  assert.equal(f.run('--scope', 'all').status, 0);
  f.put(1, 'packages/infrastructure/client/package.json', manifest('@game-guild/client', 'packages/incorrect'));
  assert.equal(f.run('--scope', 'all').status, 1);
  f.put(1, 'packages/infrastructure/client/package.json', manifest('@game-guild/client', 'packages/infrastructure/client', '2.0.0'));
  assert.equal(f.run('--scope', 'all').status, 1);
  f.put(1, 'packages/infrastructure/client/package.json', manifest('@game-guild/client', 'packages/infrastructure/client'));
  f.put(0, 'packages/client/src/runtime/path.ts', "export const location = 'packages/client';");
  f.put(1, 'packages/infrastructure/client/src/runtime/path.ts', "export const location = 'packages/infrastructure/client';");
  assert.equal(f.run('--scope', 'all').status, 1);
});

test('keeps product-generated client differences advisory and shared runtime differences blocking', (t) => {
  const f = fixture(t);
  f.put(0, 'packages/client/package.json', '{"name":"@modu-estate/client"}');
  f.put(1, 'packages/infrastructure/client/package.json', '{"name":"@game-guild/client"}');
  f.put(0, 'packages/client/src/generated/property.ts', 'export type Property = {};');
  f.put(1, 'packages/infrastructure/client/src/generated/course.ts', 'export type Course = {};');
  const first = f.run('--scope', 'all');
  assert.equal(first.status, 0, first.stderr);
  assert.equal(first.report.summary.advisoryDriftFiles, 2);
  assert.equal(first.report.summary.driftFiles, 0);
  f.put(0, 'packages/client/src/runtime/auth.ts', 'export const enabled = false;');
  assert.equal(f.run('--scope', 'all').status, 1);
});

test('refuses normalization collisions, incompatible baselines and replacement of existing reports', (t) => {
  const f = fixture(t);
  const first = f.run();
  assert.equal(first.status, 0, first.stderr);
  const before = fs.readFileSync(path.join(first.output, 'report.json'), 'utf8');
  assert.equal(f.run('--output-dir', first.output, '--report-only').status, 2);
  assert.equal(fs.readFileSync(path.join(first.output, 'report.json'), 'utf8'), before);
  assert.equal(f.run('--scope', 'all', '--baseline', path.join(first.output, 'report.json'), '--report-only').status, 2);
  f.put(0, 'apps/api/Source/Modules/ModuEstate.SharedKernel/GameGuild.SharedKernel.csproj', '<Project />');
  assert.equal(f.run('--report-only').status, 2);
});

test('reversing invocation roots preserves directions and an unchanged audit has no new findings or source writes', (t) => {
  const f = fixture(t);
  const source = 'apps/api/Source/Modules/ModuEstate.SharedKernel/New.cs';
  f.put(0, source, 'class New {}');
  const before = fs.readFileSync(path.join(f.roots[0], source), 'utf8');
  const first = f.run();
  const second = f.run('--root', f.roots[1], '--peer-root', f.roots[0], '--baseline', path.join(first.output, 'report.json'));
  assert.equal(second.status, 1, second.stderr);
  assert.equal(second.report.delta.added.length, 0);
  assert.equal(second.report.delta.changed.length, 0);
  assert.equal(second.report.delta.resolved.length, 0);
  assert.equal(second.report.delta.unchanged.length, 1);
  assert.equal(second.report.areas.find((a) => a.kind === 'module').counts.onlyModuEstate, 1);
  assert.equal(fs.readFileSync(path.join(f.roots[0], source), 'utf8'), before);
});

test('shared host test contracts remain blocking across moved test roots', (t) => {
  const f = fixture(t);
  fs.writeFileSync(f.policy, JSON.stringify({ schemaVersion: 1, requiredCommonModules: ['SharedKernel'],
    productModules: {}, testAliases: {}, requiredCommonHostTestPaths: ['Email/DeliveryTests.cs'] }));
  f.put(0, 'apps/api/Tests/ModuEstate.API.UnitTests/ModuEstate.API.UnitTests.csproj', '<Project />');
  f.put(1, 'apps/api/tests/GameGuild.API.UnitTests/GameGuild.API.UnitTests.csproj', '<Project />');
  f.put(0, 'apps/api/Tests/ModuEstate.API.UnitTests/Email/DeliveryTests.cs', 'namespace ModuEstate; // retry assertion');
  f.put(1, 'apps/api/tests/GameGuild.API.UnitTests/Email/DeliveryTests.cs', 'namespace GameGuild; // retry assertion');
  assert.equal(f.run().status, 0);
  f.put(1, 'apps/api/tests/GameGuild.API.UnitTests/Email/DeliveryTests.cs', 'namespace GameGuild; // removed assertion');
  const changed = f.run();
  assert.equal(changed.status, 1);
  assert.equal(changed.report.areas.find(a => a.kind === 'shared-host-test').counts.different, 1);
});

test('a required host test absent in both repositories is an inventory failure', (t) => {
  const f = fixture(t);
  fs.writeFileSync(f.policy, JSON.stringify({ schemaVersion: 1, requiredCommonModules: ['SharedKernel'],
    productModules: {}, testAliases: {}, requiredCommonHostTestPaths: ['Email/DeliveryTests.cs'] }));
  const result = f.run();
  assert.equal(result.status, 1);
  assert.ok(result.report.issues.some(issue => issue.code === 'missing-common-host-test-path'
    && issue.missingIn.length === 2));
});
