import assert from 'node:assert/strict';
import test from 'node:test';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { createHash } from 'node:crypto';
import { classifyTextPair, csharp, verifyInput, verifyReviews, triageDifference, validate } from './common-difference-validation.mjs';

test('TypeScript classification preserves behavior and build directives', async () => {
  assert.equal((await classifyTextPair('test.ts', "// comment\nexport const x = 'ok';", 'export const x="ok"')).category, 'syntax-equivalent');
  assert.notEqual((await classifyTextPair('test.ts', 'export const x="a  b";', 'export const x="a b";')).category, 'syntax-equivalent');
  assert.notEqual((await classifyTextPair('test.ts', '// @ts-nocheck\nconst x=1;', 'const x=1;')).category, 'syntax-equivalent');
  assert.notEqual((await classifyTextPair('test.ts', 'function f(){return\n { x: 1 }}', 'function f(){return { x: 1 }}')).category, 'syntax-equivalent');
  assert.notEqual(
    (await classifyTextPair('test.ts', '// @ts-ignore\nconst x=1;\nconst y=2;', 'const x=1;\n// @ts-ignore\nconst y=2;')).category,
    'syntax-equivalent',
  );
  assert.notEqual((await classifyTextPair('test.ts', 'const x = ;', 'const x = ; // comment')).category, 'syntax-equivalent');
});

test('C# classification preserves literals, directive placement, disabled branches and reports syntax errors', () => {
  const pairs = [
    ['trivia', 'class A { string X = "ok"; }', '// comment\nclass A{string X="ok";}'],
    ['literal', 'class A { string X = "a  b"; }', 'class A { string X = "a b"; }'],
    ['directive', '#nullable enable\nclass A{}\nclass B{}', 'class A{}\n#nullable enable\nclass B{}'],
    ['disabled', '#if OFF\nclass A{}\n#endif', '#if OFF\nclass B{}\n#endif'],
    ['invalid', 'class A { void F( }', ''],
  ].map(([Id, Left, Right]) => ({ Id, Left, Right }));
  const results = new Map(csharp(pairs).map((r) => [r.Id, r]));
  assert.equal(results.get('trivia').Equal, true);
  for (const id of ['literal', 'directive', 'disabled']) assert.equal(results.get(id).Equal, false, id);
  assert.ok(results.get('invalid').LeftErrors.length);
});

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'common-difference-validation-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const file = 'src/example.ts';
  const roots = Object.fromEntries(['ModuEstate', 'GameGuild'].map((brand) => [brand, path.join(root, brand)]));
  const evidence = {};
  for (const [brand, directory] of Object.entries(roots)) {
    fs.mkdirSync(path.join(directory, 'src'), { recursive: true });
    const text = brand === 'ModuEstate' ? 'export const x = 1;' : '// trivia\nexport const x=1;';
    fs.writeFileSync(path.join(directory, file), text);
    evidence[brand] = { path: file, rawSha256: createHash('sha256').update(text).digest('hex') };
  }
  const report = {
    schemaVersion: 1,
    consistentSnapshot: true,
    repositories: Object.entries(roots).map(([brand, root]) => ({ brand, root })),
    summary: { driftFiles: 1 },
    areas: [
      { kind: 'package', name: 'client', roots: { ModuEstate: 'src', GameGuild: 'src' }, files: [{ path: 'example.ts', status: 'different', ...evidence }] },
    ],
  };
  return { root, roots, report };
}

test('validation emits per-file evidence without approving functional synchronization', async (t) => {
  const { root, report } = fixture(t);
  const result = await validate(report, path.join(root, 'output'));
  assert.equal(result.summary.total, 1);
  assert.equal(result.summary.equivalent, 1);
  assert.equal(result.summary.syntaxCheckedFiles, 1);
  assert.equal(result.rows[0].triage.approvedForSync, false);
  assert.ok(fs.existsSync(path.join(root, 'output', result.rows[0].diff)));
  await assert.rejects(validate(report, path.join(root, 'output')), /Output exists/);
});

test('stale sources, duplicate rows and newly created counterparts fail validation', (t) => {
  const { roots, report } = fixture(t);
  const row = report.areas[0].files[0];
  const original = structuredClone(row);
  const source = path.join(roots.ModuEstate, row.ModuEstate.path);
  fs.appendFileSync(source, '\n// changed');
  assert.throws(() => verifyInput(report), /Stale source audit/);
  fs.writeFileSync(source, 'export const x = 1;');
  row.ModuEstate = null;
  row.status = 'onlyGameGuild';
  assert.throws(() => verifyInput(report), /Previously absent counterpart/);
  report.areas[0].files = [original, structuredClone(original)];
  report.summary.driftFiles = 2;
  assert.throws(() => verifyInput(report), /Duplicate audit/);
});

test('reviews are hash bound, complete and refer only to current rows', () => {
  const rows = [{ id: 'example', fingerprint: 'abc' }];
  assert.throws(() => verifyReviews(rows, { entries: { example: { fingerprint: 'old' } } }), /Stale review/);
  assert.throws(() => verifyReviews(rows, { entries: { gone: {} } }), /Unknown review/);
  assert.throws(() => verifyReviews(rows, { entries: { example: { fingerprint: 'abc' } } }), /Incomplete review/);
  assert.doesNotThrow(() => verifyReviews(rows, { entries: { example: { fingerprint: 'abc', category: 'risk', reason: 'Concrete evidence.' } } }));
});

test('triage distinguishes tests, event contracts, security boundaries and UI migration', () => {
  for (const [kind, area, file, expected] of [
    ['test', 'Assets.UnitTests', 'Tests.cs', 'test-contract-drift'],
    ['module', 'Assets', 'Architecture/Contracts.cs', 'event-contract-addition'],
    ['module', 'Identity.Authorization', 'Services/Merger.cs', 'authorization-boundary-review'],
    ['package', 'ui', 'src/components/button.tsx', 'ui-contract-migration'],
    ['package', 'ui', 'package.json', 'build-configuration-drift'],
  ]) {
    const result = triageDifference({ kind, area, path: file });
    assert.equal(result.category, expected);
    assert.equal(result.approvedForSync, false);
  }
});

test('JSON formatting equality preserves array order and distinct values', async () => {
  assert.equal((await classifyTextPair('package.json', '{"a":1,"b":2}', '{ "b": 2, "a": 1 }')).category, 'data-equivalent');
  assert.notEqual((await classifyTextPair('package.json', '{"a":[1,2]}', '{"a":[2,1]}')).category, 'data-equivalent');
  assert.notEqual((await classifyTextPair('package.json', '{"x":"a  b"}', '{"x":"a b"}')).category, 'data-equivalent');
});
