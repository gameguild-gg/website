import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { parseArgs } from 'node:util';
import { pathToFileURL } from 'node:url';

const hash = (bytes) => createHash('sha256').update(bytes).digest('hex');
const brands = ['ModuEstate', 'GameGuild'];
const spellings = {
  ModuEstate: ['ModuEstate', 'modu-estate', 'modu_estate', 'moduestate', 'MODUESTATE', 'Modu Estate'],
  GameGuild: ['GameGuild', 'game-guild', 'game_guild', 'gameguild', 'GAMEGUILD', 'Game Guild'],
};
function translate(text, from, to) {
  return spellings[from].reduce((value, spelling, index) => value.replaceAll(spelling, spellings[to][index]), text);
}
function inside(root, relative) {
  const absolute = path.resolve(root, relative);
  const resolved = fs.existsSync(absolute) ? fs.realpathSync(absolute) : absolute;
  const suffix = path.relative(fs.realpathSync(root), resolved);
  if (suffix.startsWith('..') || path.isAbsolute(suffix)) throw new Error(`Path escapes repository: ${relative}`);
  return absolute;
}
function readExpected(root, evidence) {
  const absolute = inside(root, evidence.path);
  const bytes = fs.readFileSync(absolute);
  if (hash(bytes) !== evidence.rawSha256) throw new Error(`Stale audit: ${absolute}`);
  return bytes;
}

function resolveAreaRoot(area, brand) {
  if (area.roots[brand]) return area.roots[brand];
  if (area.kind === 'module') return `apps/api/Source/Modules/${brand}.${area.name}`;
  if (area.kind === 'test') {
    const directory = brand === 'ModuEstate' ? 'Tests' : 'tests';
    return `apps/api/${directory}/${brand}.${area.name}`;
  }
  return null;
}

// Applies only explicit reviewed file preferences and mechanical namespace
// translation. It never infers a semantic merge or deletes one-sided features.
export function reconcile(report, selection, output, apply = false) {
  if (!report.consistentSnapshot || report.schemaVersion !== 1) throw new Error('Invalid audit snapshot.');
  const roots = Object.fromEntries(report.repositories.map((r) => [r.brand, r.root]));
  if (!brands.every((brand) => roots[brand])) throw new Error('Both repositories are required.');
  const seen = new Set();
  const proposals = [];
  for (const decision of selection.areas) {
    const key = `${decision.kind}:${decision.name}`;
    if (seen.has(key)) throw new Error(`Duplicate selection: ${key}`);
    seen.add(key);
    if (!brands.includes(decision.prefer) || !decision.reason?.trim()) throw new Error(`Missing preference/reason: ${key}`);
    const area = report.areas.find((a) => `${a.kind}:${a.name}` === key);
    if (!area) throw new Error(`Unknown area: ${key}`);
    const areaRoots = Object.fromEntries(brands.map((brand) => [brand, resolveAreaRoot(area, brand)]));
    if (!brands.every((brand) => areaRoots[brand])) throw new Error(`Both area roots are required: ${key}`);
    if (decision.include !== undefined) {
      if (!Array.isArray(decision.include) || decision.include.length === 0
          || new Set(decision.include).size !== decision.include.length)
        throw new Error(`Invalid exact file selection: ${key}`);
      for (const included of decision.include)
        if (!area.files.some((row) => row.path === included))
          throw new Error(`Unknown included file: ${key}/${included}`);
    }
    for (const row of area.files) {
      if (decision.include && !decision.include.includes(row.path)) continue;
      if (decision.exclude?.includes(row.path)) continue;
      const preference = decision.files?.[row.path] ?? decision.prefer;
      if (!brands.includes(preference)) throw new Error(`Invalid file preference: ${key}/${row.path}`);
      const sourceBrand = row[preference] ? preference : brands.find((brand) => row[brand]);
      const targetBrand = brands.find((brand) => brand !== sourceBrand);
      const source = row[sourceBrand];
      const original = readExpected(roots[sourceBrand], source);
      const sourceSuffix = path.relative(areaRoots[sourceBrand], source.path);
      if (sourceSuffix.startsWith('..') || path.isAbsolute(sourceSuffix)) throw new Error('Source escapes area.');
      const targetPath = row[targetBrand]?.path ?? path.join(areaRoots[targetBrand], translate(sourceSuffix, sourceBrand, targetBrand));
      const destination = inside(roots[targetBrand], targetPath);
      const before = row[targetBrand] ? readExpected(roots[targetBrand], row[targetBrand]) : null;
      if (!before && fs.existsSync(destination)) throw new Error(`Stale missing counterpart: ${destination}`);
      const after = row.comparison === 'binary' ? original : Buffer.from(translate(original.toString('utf8'), sourceBrand, targetBrand));
      if (before?.equals(after)) continue;
      proposals.push({ id: `${key}:${row.path}`, sourceBrand, sourcePath: source.path, sourceHash: hash(original),
        targetBrand, targetPath, beforeHash: before ? hash(before) : null, afterHash: hash(after),
        reason: decision.reason, before, after, destination });
    }
  }
  if (new Set(proposals.map((p) => p.destination.toLowerCase())).size !== proposals.length) throw new Error('Duplicate target.');
  if (fs.existsSync(output)) throw new Error(`Output exists: ${output}`);
  fs.mkdirSync(output, { recursive: true });
  const changes = proposals.map(({ before, after, destination, ...row }) => row);
  const result = { schemaVersion: 1, createdAt: new Date().toISOString(), applied: false, repositories: report.repositories, changes };
  fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify(result, null, 2) + '\n');
  if (apply) {
    // Snapshot BOTH sides before any write, including uncommitted source edits.
    for (const item of proposals) {
      for (const [brand, file, bytes] of [
        [item.sourceBrand, item.sourcePath, fs.readFileSync(inside(roots[item.sourceBrand], item.sourcePath))],
        [item.targetBrand, item.targetPath, item.before],
      ]) {
        if (!bytes) continue;
        const saved = path.join(output, 'before', brand, file);
        fs.mkdirSync(path.dirname(saved), { recursive: true });
        fs.writeFileSync(saved, bytes, { flag: 'wx' });
      }
    }
    // Revalidate the entire write set after snapshots and before applying it.
    for (const item of proposals) {
      readExpected(roots[item.sourceBrand], { path: item.sourcePath, rawSha256: item.sourceHash });
      if (item.beforeHash) readExpected(roots[item.targetBrand], { path: item.targetPath, rawSha256: item.beforeHash });
      else if (fs.existsSync(item.destination)) throw new Error(`Stale target: ${item.destination}`);
    }
    for (const item of proposals) {
      fs.mkdirSync(path.dirname(item.destination), { recursive: true });
      fs.writeFileSync(item.destination, item.after);
    }
    result.applied = true;
    fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify(result, null, 2) + '\n');
  }
  return result;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try {
    const { values } = parseArgs({ options: { report: { type: 'string' }, plan: { type: 'string' }, output: { type: 'string' }, apply: { type: 'boolean', default: false } } });
    if (!values.report || !values.plan || !values.output) throw new Error('Usage: --report report.json --plan reviewed-plan.json --output NEW_DIRECTORY [--apply]');
    const result = reconcile(JSON.parse(fs.readFileSync(values.report, 'utf8')), JSON.parse(fs.readFileSync(values.plan, 'utf8')), path.resolve(values.output), values.apply);
    console.log(JSON.stringify({ applied: result.applied, changes: result.changes.length, manifest: path.join(values.output, 'manifest.json') }));
  } catch (error) { console.error(error.message); process.exitCode = 2; }
}
