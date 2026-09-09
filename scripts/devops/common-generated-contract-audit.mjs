import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { audit } from './common-module-audit.mjs';
import { assertSameSourceSnapshot } from './common-module-tests.mjs';

const normalize = text => text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n')
  .replace(/ModuEstate|GameGuild/g, '__PROJECT__').replace(/modu-estate|game-guild/g, '__project-slug__')
  .replace(/modu_estate|game_guild/g, '__project_snake__').replace(/moduestate|gameguild/g, '__projectlower__')
  .replace(/MODUESTATE|GAMEGUILD/g, '__PROJECT_UPPER__').replace(/Modu Estate|Game Guild/g, '__Project Words__');

// Mirrors the API's canonical module tags. Empty entries are infrastructure-only
// modules; adding a new common module requires an explicit classification here.
const prefixesByModule = {
  AI: ['ai'], Analytics: ['analytics'], Assets: ['assets'], Commerce: ['commerce'],
  'Commerce.Billing': ['commerce-billing'], 'Commerce.Orders': ['commerce-orders', 'commerce-marketplace'],
  'Commerce.Payments': ['commerce-payments'], 'Commerce.Products': ['commerce-products'],
  'Commerce.Subscriptions': ['commerce-subscriptions'], 'Compliance.Audit': ['compliance-audit'],
  'Compliance.Consent': ['compliance-consent'], 'Compliance.KYC': ['compliance-kyc'],
  'Content.Pages': ['content-pages'], Features: ['features'],
  'Identity.Authentication': ['auth'], 'Identity.Authorization': ['access-control'], 'Identity.Context': [],
  'Identity.Tenants': ['tenants'], 'Identity.Users': ['users'], Localization: ['localization'],
  'Monitoring.SLA': ['monitoring-sla'], Notifications: ['notifications'], Resources: ['resources'],
  'Resources.Contents': ['resources-contents'], SharedKernel: [], Tags: ['tags'],
};

export function commonPrefixes(modules) {
  return [...new Set(modules.flatMap(name => {
    if (!Object.hasOwn(prefixesByModule, name)) throw new Error(`Unclassified generated contract module: ${name}`);
    return prefixesByModule[name];
  }))].sort();
}

function parse(file, text) {
  const source = ts.createSourceFile(file, normalize(text), ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
  if (source.parseDiagnostics.length) throw new Error(`Invalid TypeScript: ${file}`);
  return source;
}

function identifiers(node) {
  const names = new Set();
  const visit = current => {
    if (ts.isIdentifier(current)) names.add(current.text);
    ts.forEachChild(current, visit);
  };
  visit(node);
  return names;
}

function declarations(text) {
  const source = parse('types.gen.ts', text);
  const symbols = new Map();
  for (const statement of source.statements) {
    const nodes = ts.isVariableStatement(statement) ? statement.declarationList.declarations : [statement];
    for (const node of nodes) {
      if (!node.name || !ts.isIdentifier(node.name)) continue;
      const name = node.name.text;
      const record = symbols.get(name) ?? { text: [], references: new Set(), line: source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1 };
      // Type and value declarations can legally share a name. Retain both.
      record.text.push(ts.isVariableStatement(statement)
        ? statement.getFullText(source).trim()
        : node.getFullText(source).trim());
      for (const reference of identifiers(node)) record.references.add(reference);
      symbols.set(name, record);
    }
  }
  return symbols;
}

function typeRoots(file, text) {
  const source = parse(file, text);
  const result = new Set();
  const visit = node => {
    if (ts.isPropertyAccessExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'Types')
      result.add(node.name.text);
    if (ts.isQualifiedName(node) && ts.isIdentifier(node.left) && node.left.text === 'Types') result.add(node.right.text);
    ts.forEachChild(node, visit);
  };
  visit(source);
  return result;
}

export function compareGeneratedContracts(left, right, prefixes) {
  const selected = [...new Set([...Object.keys(left.modules), ...Object.keys(right.modules)])]
    .filter(name => prefixes.some(prefix => name === `${prefix}.gen.ts` || name.startsWith(`${prefix}-`))).sort();
  if (!selected.length) throw new Error('Refusing an empty generated common contract comparison.');
  const findings = [];
  const roots = new Set();
  for (const name of selected) {
    const values = [left.modules[name], right.modules[name]];
    for (const value of values) if (value !== undefined) for (const reference of typeRoots(name, value)) roots.add(reference);
    if (values[0] === undefined || values[1] === undefined || normalize(values[0]) !== normalize(values[1]))
      findings.push({ kind: 'module', name, status: values[0] === undefined ? 'onlyGameGuild' : values[1] === undefined ? 'onlyModuEstate' : 'different' });
  }
  const maps = [declarations(left.types), declarations(right.types)];
  const queue = [...roots];
  const visited = new Set();
  while (queue.length) {
    const name = queue.shift();
    if (visited.has(name)) continue;
    visited.add(name);
    const records = maps.map(map => map.get(name));
    if (!records[0] || !records[1] || records[0].text.join('\n') !== records[1].text.join('\n'))
      findings.push({ kind: 'type', name, status: !records[0] && !records[1] ? 'missingBoth' : !records[0] ? 'onlyGameGuild' : !records[1] ? 'onlyModuEstate' : 'different',
        ModuEstateLine: records[0]?.line ?? null, GameGuildLine: records[1]?.line ?? null });
    for (const record of records) if (record)
      for (const reference of record.references)
        if (maps.some(map => map.has(reference))) queue.push(reference);
  }
  return { passed: findings.length === 0, moduleFiles: selected.length, referencedTypes: visited.size,
    findings: findings.sort((a, b) => `${a.kind}:${a.name}`.localeCompare(`${b.kind}:${b.name}`)) };
}

function inside(root, relative) {
  const target = fs.realpathSync(path.resolve(root, relative));
  const suffix = path.relative(fs.realpathSync(root), target);
  if (suffix.startsWith('..') || path.isAbsolute(suffix)) throw new Error('Generated source escapes repository.');
  return target;
}

function run() {
  const { values } = parseArgs({ options: { report: { type: 'string' }, 'output-dir': { type: 'string' } } });
  if (!values.report || !values['output-dir']) throw new Error('Required: --report CURRENT_AUDIT_JSON --output-dir NEW_DIRECTORY');
  const bytes = fs.readFileSync(values.report);
  const report = JSON.parse(bytes.toString('utf8'));
  const snapshot = () => audit({ root: report.repositories[0].root, peerRoot: report.repositories[1].root, policy: report.policy, scope: report.scope });
  assertSameSourceSnapshot(report, snapshot());
  const area = report.areas.find(area => area.kind === 'generated-client' && area.name === 'client');
  if (!area) throw new Error('A scope=all source report with both generated clients is required.');
  const input = {};
  for (const repo of report.repositories) {
    const directory = inside(repo.root, area.roots[repo.brand]);
    const modules = {};
    for (const name of fs.readdirSync(path.join(directory, 'modules')).filter(name => name.endsWith('.gen.ts')).sort())
      modules[name] = fs.readFileSync(inside(repo.root, path.relative(repo.root, path.join(directory, 'modules', name))), 'utf8');
    input[repo.brand] = { modules, types: fs.readFileSync(inside(repo.root, path.relative(repo.root, path.join(directory, 'types.gen.ts'))), 'utf8') };
  }
  const prefixes = commonPrefixes(report.inventory.modules.filter(module => module.classification === 'common').map(module => module.name));
  const result = { schemaVersion: 1, createdAt: new Date().toISOString(), sourceAudit: path.resolve(values.report),
    sourceAuditSha256: createHash('sha256').update(bytes).digest('hex'), prefixes,
    limitations: [
      'Compares existing generated common module files and the transitive type/schema declarations they reference. Does not modify generated source.',
      'Not a TypeScript compiler or proof that both generated clients reflect the current live API. Endpoints missing from both generated clients require comparison against fresh OpenAPI artifacts.',
      'Branding, BOM and line endings are normalized. Module files otherwise retain source differences. Declaration comparison ignores surrounding whitespace but retains bodies and export modifiers.',
    ], ...compareGeneratedContracts(input.ModuEstate, input.GameGuild, prefixes) };
  assertSameSourceSnapshot(report, snapshot());
  const output = path.resolve(values['output-dir']);
  if (fs.existsSync(output)) throw new Error('Refusing to replace a previous contract report.');
  fs.mkdirSync(output, { recursive: true });
  fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(result, null, 2) + '\n', { flag: 'wx' });
  const lines = ['# Generated common API contract audit', '', `Result: ${result.passed ? 'pass' : 'drift'}. ${result.moduleFiles} module files; ${result.referencedTypes} referenced declarations; ${result.findings.length} findings.`, '',
    ...result.limitations.map(line => `- ${line}`), '', '| Kind | Name | Status |', '| --- | --- | --- |',
    ...result.findings.map(finding => `| ${finding.kind} | ${finding.name} | ${finding.status} |`)];
  fs.writeFileSync(path.join(output, 'report.md'), lines.join('\n') + '\n', { flag: 'wx' });
  console.log(`${result.passed ? 'PASS' : 'DRIFT'}: ${result.moduleFiles} common generated modules; ${result.referencedTypes} declarations; ${result.findings.length} findings. ${path.join(output, 'report.json')}`);
  process.exitCode = result.passed ? 0 : 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { run(); } catch (error) { console.error(error.message); process.exitCode = 2; }
}
