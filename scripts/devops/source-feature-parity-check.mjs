#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { audit, markdown } from './common-module-audit.mjs';

try {
  const { values } = parseArgs({
    options: {
      root: { type: 'string' },
      'peer-root': { type: 'string' },
      policy: { type: 'string' },
      scope: { type: 'string', default: 'api' },
      'output-dir': { type: 'string' },
      baseline: { type: 'string' },
      'report-only': { type: 'boolean', default: false },
      help: { type: 'boolean', short: 'h' },
    },
  });
  if (values.help) {
    console.log(`Compare the current ModuEstate and GameGuild working trees without changing source.
Usage: node scripts/devops/source-feature-parity-check.mjs [options]
  --root PATH          Repository root (default: current directory)
  --peer-root PATH     Peer repository (or PARITY_PEER_ROOT; sibling layout fallback)
  --scope api|all      Common API modules/tests; all adds npm packages and advisory host comparison
  --policy PATH        Explicit module classification policy
  --output-dir PATH    New report directory (default: artifacts/common-module-audit/<timestamp>)
  --baseline PATH      Earlier report.json with the same scope and policy
  --report-only        Exit 0 for completed audits with drift; findings remain in the reports
Exit: 0 parity/report-only, 1 common drift or inventory findings, 2 incomplete/invalid audit.
Requires Node >=20 and Git. Reports JSON and Markdown. Never fetches, syncs or merges.`);
  } else {
    if (!['api', 'all'].includes(values.scope)) throw new Error('Scope must be api or all.');
    const root = path.resolve(values.root ?? process.cwd());
    const name = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8').replace(/^\uFEFF/, '')).name;
    if (!['modu-estate', 'game-guild'].includes(name)) throw new Error(`Unsupported repository: ${name}`);
    const peerName = name === 'modu-estate' ? 'game-guild' : 'modu-estate';
    const peerRoot = path.resolve(values['peer-root'] ?? process.env.PARITY_PEER_ROOT ?? path.join(root, '..', '..', peerName, peerName));
    const policyPath = path.resolve(values.policy ?? fileURLToPath(new URL('./common-module-policy.json', import.meta.url)));
    const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8').replace(/^\uFEFF/, ''));
    const baseline = values.baseline ? JSON.parse(fs.readFileSync(values.baseline, 'utf8')) : undefined;
    const output = path.resolve(values['output-dir'] ?? path.join(root, 'artifacts/common-module-audit', new Date().toISOString().replace(/[:.]/g, '-')));
    for (const file of ['report.json', 'report.md'])
      if (fs.existsSync(path.join(output, file))) throw new Error(`Report already exists: ${path.join(output, file)}. Choose a new output directory.`);
    const report = audit({ root, peerRoot, policy, scope: values.scope, baseline });
    fs.mkdirSync(output, { recursive: true });
    fs.writeFileSync(path.join(output, 'report.json'), `${JSON.stringify(report, null, 2)}\n`, { flag: 'wx' });
    fs.writeFileSync(path.join(output, 'report.md'), markdown(report), { flag: 'wx' });
    console.log(
      `Audit ${report.result}: ${report.summary.commonModules} common API modules; ${report.summary.driftFiles} common-file findings; ${report.summary.advisoryDriftFiles} advisory-file findings (host/generated client); ${report.summary.inventoryIssues} inventory findings.`,
    );
    for (const repo of report.repositories)
      console.log(
        `${repo.brand}: ${repo.branch} @ ${repo.commit.slice(0, 12)}; dirty=${repo.dirty}; ahead=${repo.ahead ?? '?'} behind=${repo.behind ?? '?'} (local refs)`,
      );
    console.table(report.areas.filter((a) => a.kind !== 'test').map((a) => ({ kind: a.kind, name: a.name, ...a.counts })));
    for (const issue of report.issues) console.log(`${issue.code}: ${issue.name}`);
    if (report.delta)
      console.log(
        `Baseline delta: ${report.delta.added.length} added, ${report.delta.resolved.length} resolved, ${report.delta.changed.length} changed, ${report.delta.unchanged.length} unchanged.`,
      );
    console.log(`JSON: ${path.join(output, 'report.json')}\nMarkdown: ${path.join(output, 'report.md')}`);
    process.exitCode = report.result === 'drift' && !values['report-only'] ? 1 : 0;
  }
} catch (error) {
  console.error(`Audit incomplete: ${error.message}`);
  process.exitCode = 2;
}
