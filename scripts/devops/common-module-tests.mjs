import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { audit } from './common-module-audit.mjs';

export function assertSameSourceSnapshot(expected, current) {
  const signature = report => JSON.stringify({
    comparatorVersion: report.comparatorVersion, policy: report.policySha256, scope: report.scope,
    repositories: report.repositories.map(({ brand, commit }) => [brand, commit]),
    areas: report.areas.map(({ kind, name, fingerprints }) => [kind, name, fingerprints]),
  });
  if (!expected.consistentSnapshot || !current.consistentSnapshot || signature(expected) !== signature(current))
    throw new Error('Source changed since the audit. Generate a fresh report and rerun the tests.');
}

export function testPlan(report, { brand, kind = 'unit' } = {}) {
  if (!['unit', 'integration', 'all'].includes(kind)) throw new Error('Unknown test kind.');
  if (brand && !['ModuEstate', 'GameGuild'].includes(brand)) throw new Error('Unknown repository brand.');
  if (report.schemaVersion !== 1 || !Array.isArray(report.areas) || !Array.isArray(report.repositories)) throw new Error('Invalid audit report.');
  const result = [];
  for (const repo of report.repositories.filter(repo => !brand || repo.brand === brand)) {
    if (!['ModuEstate', 'GameGuild'].includes(repo.brand)) throw new Error('Unsupported repository.');
    for (const area of report.areas.filter(area => area.kind === 'test')) {
      if (!/^[A-Za-z0-9.]+$/.test(area.name)) throw new Error('Unsafe test project name.');
      if (kind !== 'all' && !area.name.endsWith(kind === 'unit' ? '.UnitTests' : '.IntegrationTests')) continue;
      const relativeRoot = area.roots[repo.brand];
      if (!relativeRoot) throw new Error(`Missing common suite: ${repo.brand}.${area.name}`);
      const project = path.resolve(repo.root, relativeRoot, `${repo.brand}.${area.name}.csproj`);
      const relative = path.relative(path.resolve(repo.root), project);
      if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Test project escapes repository.');
      result.push({ brand: repo.brand, name: area.name, root: repo.root, project });
    }
    if (kind !== 'integration') {
      const hostAreas = report.areas.filter(area => area.kind === 'shared-host-test');
      const projects = new Set();
      const classes = new Set();
      for (const area of hostAreas) {
        const relativeFile = area.roots[repo.brand];
        if (!relativeFile || !/^(?:[A-Za-z0-9_.-]+\/)*[A-Za-z_][A-Za-z0-9_]*Tests\.cs$/.test(area.name)
            || !relativeFile.endsWith(`/${area.name}`))
          throw new Error('Missing or inconsistent shared host test path.');
        const testRoot = relativeFile.slice(0, -area.name.length - 1);
        const project = path.resolve(repo.root, testRoot, `${repo.brand}.API.UnitTests.csproj`);
        const relative = path.relative(path.resolve(repo.root), project);
        if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Host test project escapes repository.');
        projects.add(project);
        classes.add(path.posix.basename(area.name, '.cs'));
      }
      if (projects.size > 1) throw new Error('Inconsistent shared host test project roots.');
      if (projects.size === 1) result.push({ brand: repo.brand, name: 'API.CommonHost.UnitTests', root: repo.root,
        project: [...projects][0], filter: [...classes].sort().map(name => `FullyQualifiedName~.${name}.`).join('|') });
    }
  }
  if (result.length === 0) throw new Error('Refusing an empty common test run.');
  return result;
}

export function counters(xml) {
  const attributes = xml.match(/<Counters\b([^>]*)\/?\s*>/)?.[1];
  if (!attributes) throw new Error('TRX has no test counters.');
  return Object.fromEntries([...attributes.matchAll(/(\w+)="(\d+)"/g)].map(([, name, value]) => [name, Number(value)]));
}

async function run(args) {
  const options = {};
  for (let index = 0; index < args.length; index++) {
    const name = args[index];
    if (!['--report', '--output-dir', '--brand', '--kind'].includes(name) || !args[index + 1]) throw new Error(`Invalid argument: ${name}`);
    options[name.slice(2)] = args[++index];
  }
  if (!options.report || !options['output-dir']) throw new Error('Required: --report REPORT --output-dir NEW_DIRECTORY [--brand ModuEstate|GameGuild] [--kind unit|integration|all]');
  const reportBytes = fs.readFileSync(options.report);
  const report = JSON.parse(reportBytes.toString('utf8'));
  const plan = testPlan(report, options);
  const currentSnapshot = () => audit({ root: report.repositories[0].root, peerRoot: report.repositories[1].root, policy: report.policy, scope: report.scope });
  assertSameSourceSnapshot(report, currentSnapshot());
  for (const entry of plan) if (!fs.existsSync(entry.project)) throw new Error(`Missing test project: ${entry.project}`);
  const output = path.resolve(options['output-dir']);
  if (fs.existsSync(output)) throw new Error('Refusing to replace a previous test run.');
  fs.mkdirSync(output, { recursive: true });
  const result = { startedAt: new Date().toISOString(), sourceAudit: path.resolve(options.report), sourceAuditSha256: createHash('sha256').update(reportBytes).digest('hex'), repositories: report.repositories, sourceUnchanged: false, results: [] };
  const save = () => fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify(result, null, 2) + '\n');
  save();
  // One build at a time per repository prevents competing MSBuild writes. The
  // two independent checkouts can run concurrently without using subagents.
  await Promise.all([...new Set(plan.map(entry => entry.brand))].map(async brand => {
    for (const entry of plan.filter(entry => entry.brand === brand)) {
      const stem = `${entry.brand}.${entry.name}`;
      const logPath = path.join(output, `${stem}.log`);
      const trxPath = path.join(output, `${stem}.trx`);
      const log = fs.openSync(logPath, 'wx');
      const started = Date.now();
      console.log(`START ${stem}`);
      const exitCode = await new Promise(resolve => {
        const child = spawn('dotnet', ['test', entry.project, '-c', 'Release', '--nologo', '--logger', `trx;LogFileName=${stem}.trx`, '--results-directory', output,
          ...(entry.filter ? ['--filter', entry.filter] : [])], {
          cwd: entry.root, windowsHide: true, stdio: ['ignore', log, log],
        });
        child.once('error', error => { fs.writeSync(log, String(error)); resolve(-1); });
        child.once('close', code => resolve(code ?? -1));
      });
      fs.closeSync(log);
      let counts = null;
      let evidenceError = null;
      try { counts = counters(fs.readFileSync(trxPath, 'utf8')); } catch (error) { evidenceError = error.message; }
      const passed = exitCode === 0 && counts?.executed > 0 && counts.failed === 0 && counts.error === 0;
      result.results.push({ ...entry, exitCode, passed, counters: counts, evidenceError, durationMs: Date.now() - started, log: logPath, trx: fs.existsSync(trxPath) ? trxPath : null });
      save();
      console.log(`${passed ? 'PASS' : 'FAIL'} ${stem}: ${counts ? `${counts.passed}/${counts.total}` : 'no passing test evidence'}`);
    }
  }));
  result.completedAt = new Date().toISOString();
  try { assertSameSourceSnapshot(report, currentSnapshot()); result.sourceUnchanged = true; }
  catch (error) { result.sourceError = error.message; }
  result.passed = result.sourceUnchanged && result.results.length === plan.length && result.results.every(entry => entry.passed);
  save();
  console.log(`Results: ${path.join(output, 'results.json')}`);
  process.exitCode = result.passed ? 0 : 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  run(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 2; });
}
