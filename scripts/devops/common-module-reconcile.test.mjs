import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { reconcile } from './common-module-reconcile.mjs';

const hash = (text) => createHash('sha256').update(text).digest('hex');
function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'common-reconcile-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const repositories = ['ModuEstate', 'GameGuild'].map((brand) => ({ brand, root: path.join(root, brand) }));
  const roots = Object.fromEntries(repositories.map(({ brand }) => [brand, `src/${brand}.Common`]));
  const files = [];
  for (const [file, modu, game] of [
    ['Service.cs', 'namespace ModuEstate; // durable improvement', 'namespace GameGuild; // security improvement'],
    ['OnlyModu.cs', 'namespace ModuEstate; // retain new event', null],
    ['OnlyGame.cs', null, 'namespace GameGuild; // retain new handler'],
  ]) {
    const row = { path: file, comparison: 'normalized-text', status: modu === null ? 'onlyGameGuild' : game === null ? 'onlyModuEstate' : 'different' };
    for (const [i, content] of [modu, game].entries()) {
      const repo = repositories[i];
      if (content === null) { row[repo.brand] = null; continue; }
      const relative = `${roots[repo.brand]}/${file}`;
      fs.mkdirSync(path.dirname(path.join(repo.root, relative)), { recursive: true });
      fs.writeFileSync(path.join(repo.root, relative), content);
      row[repo.brand] = { path: relative, rawSha256: hash(content) };
    }
    files.push(row);
  }
  return { root, report: { schemaVersion: 1, consistentSnapshot: true, repositories, areas: [{ kind: 'module', name: 'Common', roots, files }] },
    selection: { areas: [{ kind: 'module', name: 'Common', prefer: 'GameGuild', reason: 'Reviewed security implementation; preserve both one-sided additions.' }] } };
}

test('dry run leaves sources unchanged and exposes every planned replacement', (t) => {
  const { root, report, selection } = fixture(t);
  const result = reconcile(report, selection, path.join(root, 'dry'), false);
  assert.equal(result.changes.length, 3);
  assert.equal(result.applied, false);
  assert.equal(fs.readFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/Service.cs'), 'utf8'), 'namespace ModuEstate; // durable improvement');
  assert.equal(fs.existsSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/OnlyGame.cs')), false);
});

test('selected implementation is mirrored with branding while both one-sided improvements survive', (t) => {
  const { root, report, selection } = fixture(t);
  const result = reconcile(report, selection, path.join(root, 'applied'), true);
  assert.equal(result.changes.length, 3);
  assert.equal(fs.readFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/Service.cs'), 'utf8'), 'namespace ModuEstate; // security improvement');
  assert.equal(fs.readFileSync(path.join(root, 'GameGuild/src/GameGuild.Common/OnlyModu.cs'), 'utf8'), 'namespace GameGuild; // retain new event');
  assert.equal(fs.readFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/OnlyGame.cs'), 'utf8'), 'namespace ModuEstate; // retain new handler');
  assert.equal(fs.readFileSync(path.join(root, 'applied/before/ModuEstate/src/ModuEstate.Common/Service.cs'), 'utf8'), 'namespace ModuEstate; // durable improvement');
  assert.equal(result.changes.find((c) => c.targetPath.endsWith('OnlyGame.cs')).beforeHash, null);
});

test('concurrent edits abort before any source replacement', (t) => {
  const { root, report, selection } = fixture(t);
  fs.appendFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/OnlyModu.cs'), ' changed');
  assert.throws(() => reconcile(report, selection, path.join(root, 'out'), true), /Stale/);
  assert.equal(fs.readFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/Service.cs'), 'utf8'), 'namespace ModuEstate; // durable improvement');
});

test('invalid, duplicated and escaping selections fail closed', (t) => {
  const { root, report, selection } = fixture(t);
  assert.throws(() => reconcile(report, { areas: [{ ...selection.areas[0], name: 'Missing' }] }, path.join(root, 'a'), true), /Unknown area/);
  assert.throws(() => reconcile(report, { areas: [selection.areas[0], selection.areas[0]] }, path.join(root, 'b'), true), /Duplicate selection/);
  report.areas[0].files[0].GameGuild.path = '../../escape.cs';
  assert.throws(() => reconcile(report, selection, path.join(root, 'c'), true), /escapes/);
});

test('exact file selection never replaces unrelated host files', (t) => {
  const { root, report, selection } = fixture(t);
  selection.areas[0].include = ['OnlyModu.cs'];
  const result = reconcile(report, selection, path.join(root, 'exact'), true);
  assert.equal(result.changes.length, 1);
  assert.equal(fs.readFileSync(path.join(root, 'ModuEstate/src/ModuEstate.Common/Service.cs'), 'utf8'),
    'namespace ModuEstate; // durable improvement');
});

test('an unknown exact file selection is rejected instead of silently doing nothing', (t) => {
  const { root, report, selection } = fixture(t);
  selection.areas[0].include = ['Typo.cs'];
  assert.throws(() => reconcile(report, selection, path.join(root, 'unknown'), true), /Unknown included file/);
});

test('creates a missing common module root from the repository naming convention', (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'common-reconcile-missing-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const repositories = ['ModuEstate', 'GameGuild'].map((brand) => ({ brand, root: path.join(root, brand) }));
  for (const repository of repositories) fs.mkdirSync(repository.root, { recursive: true });

  const sourceRoot = 'apps/api/Source/Modules/GameGuild.Finance.Economy';
  const sourcePath = `${sourceRoot}/GameGuild.Finance.Economy.csproj`;
  const source = '<Project>GameGuild.Finance.Economy</Project>';
  fs.mkdirSync(path.join(repositories[1].root, sourceRoot), { recursive: true });
  fs.writeFileSync(path.join(repositories[1].root, sourcePath), source);

  const report = {
    schemaVersion: 1,
    consistentSnapshot: true,
    repositories,
    areas: [{
      kind: 'module',
      name: 'Finance.Economy',
      roots: { ModuEstate: null, GameGuild: sourceRoot },
      files: [{
        path: 'GameGuild.Finance.Economy.csproj',
        comparison: 'normalized-text',
        status: 'onlyGameGuild',
        ModuEstate: null,
        GameGuild: { path: sourcePath, rawSha256: hash(source) },
      }],
    }],
  };
  const selection = { areas: [{
    kind: 'module',
    name: 'Finance.Economy',
    prefer: 'GameGuild',
    reason: 'Establish the reviewed common module on the missing product.',
  }] };

  reconcile(report, selection, path.join(root, 'applied'), true);

  assert.equal(
    fs.readFileSync(path.join(repositories[0].root,
      'apps/api/Source/Modules/ModuEstate.Finance.Economy/ModuEstate.Finance.Economy.csproj'), 'utf8'),
    '<Project>ModuEstate.Finance.Economy</Project>',
  );
});
