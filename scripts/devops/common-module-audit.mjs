import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

const brands = ['ModuEstate', 'GameGuild'];
const comparatorVersion = 2;
const slugs = { ModuEstate: 'modu-estate', GameGuild: 'game-guild' };
const ignoredDirectories = new Set(['.git', '.idea', '.vs', 'bin', 'obj', 'node_modules', 'dist', '.next', '.turbo', 'coverage', 'testresults', '__pycache__']);
const sha = (value) => createHash('sha256').update(value).digest('hex');
const sortedUnion = (...items) => [...new Set(items.flat())].sort();
const signature = (stat) => `${stat.size}:${stat.mtimeMs}:${stat.ctimeMs}`;

// Retain comments, whitespace and literals. Textual parity is not a claim of
// semantic equivalence; collapsing whitespace could hide changed string values.
function normalize(value) {
  return value
    .replace(/^\uFEFF/, '')
    .replace(/\r\n?/g, '\n')
    .replace(/ModuEstate|GameGuild/g, '__PROJECT__')
    .replace(/modu-estate|game-guild/g, '__project-slug__')
    .replace(/modu_estate|game_guild/g, '__project_snake__')
    .replace(/moduestate|gameguild/g, '__projectlower__')
    .replace(/MODUESTATE|GAMEGUILD/g, '__PROJECT_UPPER__')
    .replace(/Modu Estate|Game Guild/g, '__Project Words__');
}

function git(root, args, optional = false) {
  try {
    return execFileSync('git', ['--no-optional-locks', '-C', root, ...args], {
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
      stdio: ['ignore', 'pipe', 'pipe'],
    });
  } catch (error) {
    if (optional) return null;
    throw new Error(`Git failed at ${root}: ${error.stderr?.toString().trim() || error.message}`);
  }
}

function metadata(root, brand) {
  const status = git(root, ['status', '--porcelain=v1', '-z']);
  const upstream = git(root, ['rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{upstream}'], true)?.trim() || null;
  const counts = upstream ? git(root, ['rev-list', '--left-right', '--count', 'HEAD...@{upstream}']).trim().split(/\s+/).map(Number) : null;
  return {
    brand,
    root,
    commit: git(root, ['rev-parse', 'HEAD']).trim(),
    branch: git(root, ['rev-parse', '--abbrev-ref', 'HEAD']).trim(),
    dirty: status.length > 0,
    statusEntries: status.split('\0').filter(Boolean),
    upstream,
    ahead: counts?.[0] ?? null,
    behind: counts?.[1] ?? null,
  };
}

function excluded(file) {
  const parts = file.toLowerCase().split('/');
  if (parts.slice(0, -1).some((p) => ignoredDirectories.has(p) || p.startsWith('testresults-'))) return 'build/cache/test-output';
  if (parts.includes('migrations') || /\.designer\.cs$/i.test(file)) return 'generated-database';
  if (/\.(trx|lcov|tsbuildinfo|pyc)$/i.test(file) || /(^|\/)(coverage\.(json|xml)|coverage\.cobertura\.xml)$/i.test(file)) return 'test-output';
  if (/(^|\/)(\.env($|\.)|.*\.(pem|key)$)/i.test(file)) return 'local-environment/key';
  return null;
}

function repository(root) {
  root = fs.realpathSync.native(root);
  if (git(root, ['rev-parse', '--show-prefix']).trim()) throw new Error(`Expected a repository root: ${root}`);
  const name = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8').replace(/^\uFEFF/, '')).name;
  const brand = brands.find((b) => slugs[b] === name);
  if (!brand) throw new Error(`Unsupported repository package name: ${name}`);
  const meta = metadata(root, brand);
  const listed = () =>
    sortedUnion(git(root, ['ls-files', '--cached', '--others', '--exclude-standard', '-z', '--', 'apps/api', 'packages']).split('\0').filter(Boolean));
  const initialList = listed();
  const files = new Map();
  const exclusions = [];
  for (const file of initialList) {
    const reason = excluded(file);
    if (reason) {
      exclusions.push({ path: file, reason });
      continue;
    }
    const absolute = path.join(root, file);
    if (!fs.existsSync(absolute)) continue;
    const stat = fs.lstatSync(absolute);
    if (stat.isSymbolicLink()) throw new Error(`Symlink in audit input requires explicit handling: ${absolute}`);
    if (!stat.isFile()) continue;
    files.set(file, { absolute, signature: signature(stat) });
  }
  return { root, brand, meta, files, exclusions, initialList, listed, cache: new Map() };
}

function read(repo, file) {
  if (repo.cache.has(file)) return repo.cache.get(file);
  const info = repo.files.get(file);
  const bytes = fs.readFileSync(info.absolute);
  let content;
  let comparison = 'normalized-text';
  try {
    if (bytes.includes(0)) throw new Error('binary');
    let text = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    if (file.startsWith('packages/') && file.endsWith('/package.json')) {
      // npm's repository.directory is project identity only when it truthfully
      // points to this very package. Do not normalize arbitrary paths in code,
      // scripts, exports, dependencies, or incorrect package metadata.
      const manifest = JSON.parse(text.replace(/^\uFEFF/, ''));
      const directory = path.posix.dirname(file);
      if (manifest.repository?.directory === directory) {
        text = text.replace(/("repository"\s*:\s*\{[^{}]*"directory"\s*:\s*)"([^"]*)"/, (match, prefix, value) =>
          value === directory ? `${prefix}"__PACKAGE_DIRECTORY__"` : match);
      }
    }
    content = normalize(text);
  } catch {
    content = bytes;
    comparison = 'binary';
  }
  const result = { path: file, rawSha256: sha(bytes), normalizedSha256: sha(content), bytes: bytes.length, comparison };
  repo.cache.set(file, result);
  return result;
}

function modules(repo) {
  const found = new Map();
  const prefix = `apps/api/Source/Modules/${repo.brand}.`;
  for (const file of repo.files.keys()) {
    if (!file.startsWith(prefix)) continue;
    const name = file.slice(prefix.length).split('/')[0];
    const root = `${prefix}${name}`;
    found.set(name, { root, hasProject: repo.files.has(`${root}/${repo.brand}.${name}.csproj`) });
  }
  if (found.size === 0) throw new Error(`No API modules found at ${repo.root}; refusing an empty audit.`);
  return found;
}

function projects(repo) {
  const found = new Map();
  for (const file of repo.files.keys()) {
    if (!/^apps\/api\/tests\//i.test(file) || !file.endsWith('.csproj')) continue;
    const stem = path.posix.basename(file, '.csproj');
    if (!stem.startsWith(`${repo.brand}.`)) continue;
    const name = stem.slice(repo.brand.length + 1);
    if (found.has(name)) throw new Error(`Duplicate test project ${name} at ${repo.root}`);
    found.set(name, path.posix.dirname(file));
  }
  return found;
}

function packages(repo) {
  const found = new Map();
  for (const file of repo.files.keys()) {
    if (!file.startsWith('packages/') || !file.endsWith('/package.json')) continue;
    const value = JSON.parse(fs.readFileSync(repo.files.get(file).absolute, 'utf8').replace(/^\uFEFF/, ''));
    if (!value.name?.startsWith(`@${slugs[repo.brand]}/`)) continue;
    const name = value.name.slice(slugs[repo.brand].length + 2);
    if (found.has(name)) throw new Error(`Duplicate package ${name} at ${repo.root}`);
    found.set(name, path.posix.dirname(file));
  }
  return found;
}

function tree(repo, root, excludedSubtrees = []) {
  const result = new Map();
  if (!root) return result;
  for (const file of repo.files.keys()) {
    if (file !== root && !file.startsWith(`${root}/`)) continue;
    if (excludedSubtrees.some((subtree) => file === `${root}/${subtree}` || file.startsWith(`${root}/${subtree}/`))) continue;
    const key = normalize(file === root ? path.posix.basename(file) : file.slice(root.length + 1));
    if (result.has(key)) throw new Error(`Normalized path collision: ${root}/${key}`);
    result.set(key, read(repo, file));
  }
  return result;
}

function compare(repos, kind, name, roots, advisory = false, excludedSubtrees = []) {
  const trees = repos.map((repo, i) => tree(repo, roots[i], excludedSubtrees));
  const counts = { equal: 0, different: 0, onlyModuEstate: 0, onlyGameGuild: 0 };
  const files = [];
  for (const key of sortedUnion([...trees[0].keys()], [...trees[1].keys()])) {
    const [left, right] = trees.map((t) => t.get(key));
    const status = !left
      ? 'onlyGameGuild'
      : !right
        ? 'onlyModuEstate'
        : left.normalizedSha256 === right.normalizedSha256 && left.comparison === right.comparison
          ? 'equal'
          : 'different';
    counts[status]++;
    if (status !== 'equal')
      files.push({
        path: key,
        status,
        comparison: left?.comparison === 'binary' || right?.comparison === 'binary' ? 'binary' : 'normalized-text',
        ModuEstate: left ?? null,
        GameGuild: right ?? null,
      });
  }
  const fingerprints = Object.fromEntries(
    trees.map((t, i) => [
      brands[i],
      sha(JSON.stringify([...t].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([key, value]) => [key, value.rawSha256]))),
    ]),
  );
  return { kind, name, advisory, excludedSubtrees, roots: Object.fromEntries(brands.map((b, i) => [b, roots[i] ?? null])), counts, fingerprints, files };
}

function checkPolicy(policy) {
  for (const hostPaths of [policy.requiredCommonHostPaths ?? [], policy.requiredCommonHostTestPaths ?? []]) {
  if (!Array.isArray(hostPaths) || hostPaths.some(entry => typeof entry !== 'string'
      || !/^[A-Za-z0-9_.-]+(?:\/[A-Za-z0-9_.-]+)*$/.test(entry)
      || entry.split('/').some(part => part === '.' || part === '..')))
    throw new Error('Invalid shared host policy paths.');
  if (hostPaths.some((entry, index) => hostPaths.some((other, otherIndex) => index !== otherIndex
      && (entry === other || entry.startsWith(`${other}/`)))))
    throw new Error('Overlapping shared host policy paths.');
  }
  if (
    policy.schemaVersion !== 1 ||
    !Array.isArray(policy.requiredCommonModules) ||
    !policy.requiredCommonModules.length ||
    !policy.requiredCommonModules.every((s) => typeof s === 'string' && s.length > 0) ||
    typeof policy.productModules !== 'object' ||
    !policy.productModules ||
    typeof policy.testAliases !== 'object' ||
    !policy.testAliases
  )
    throw new Error('Invalid common-module policy schema.');
  for (const brand of brands) {
    for (const [name, reason] of Object.entries(policy.productModules[brand] ?? {})) {
      if (!reason || typeof reason !== 'string' || policy.requiredCommonModules.includes(name))
        throw new Error(`Invalid product-only policy: ${brand}.${name}`);
    }
  }
}

function delta(current, previous) {
  if (
    previous.schemaVersion !== 1 ||
    !Array.isArray(previous.areas) ||
    !Array.isArray(previous.issues) ||
    previous.comparatorVersion !== current.comparatorVersion ||
    previous.scope !== current.scope ||
    previous.policySha256 !== current.policySha256
  )
    throw new Error('Baseline schema, comparator version, scope or policy differs; use a report produced with the same audit policy and scope.');
  function findings(report) {
    const result = new Map();
    for (const issue of report.issues) result.set(`issue:${issue.code}:${issue.name}:${issue.brand ?? ''}`, sha(JSON.stringify(issue)));
    for (const area of report.areas)
      for (const file of area.files)
        result.set(
          `${area.kind}:${area.name}:${file.path}`,
          sha(JSON.stringify([file.status, file.ModuEstate?.normalizedSha256, file.GameGuild?.normalizedSha256])),
        );
    return result;
  }
  const now = findings(current);
  const before = findings(previous);
  const result = { added: [], resolved: [], changed: [], unchanged: [] };
  for (const key of sortedUnion([...now.keys()], [...before.keys()])) {
    const group = !now.has(key) ? 'resolved' : !before.has(key) ? 'added' : now.get(key) === before.get(key) ? 'unchanged' : 'changed';
    result[group].push(key);
  }
  return result;
}

export function audit({ root, peerRoot, policy, scope = 'api', baseline }) {
  checkPolicy(policy);
  const repos = [repository(root), repository(peerRoot)].sort((a, b) => brands.indexOf(a.brand) - brands.indexOf(b.brand));
  if (repos[0].brand === repos[1].brand) throw new Error('Expected one ModuEstate and one GameGuild repository.');
  const report = {
    schemaVersion: 1,
    comparatorVersion,
    generatedAt: new Date().toISOString(),
    mode: 'working-tree',
    scope,
    policySha256: sha(JSON.stringify(policy)),
    policy,
    repositories: repos.map((r) => r.meta),
    normalization:
      'Repository branding, truthful npm repository.directory, UTF-8 BOM, CRLF/CR only. Other paths, comments, whitespace, string literals and dependency versions are retained. Binary files use raw SHA-256.',
    limitations: [
      'Static source parity only; no build, test execution, runtime or provider integration claim.',
      'Upstream counts use local remote-tracking refs; no fetch or pull is performed.',
      'Required shared host paths are blocking. Remaining product host composition and product-generated API clients are advisory; drift does not select a source of truth.',
    ],
    inventory: { modules: [], tests: [], packages: [] },
    issues: [],
    areas: [],
    exclusions: Object.fromEntries(repos.map((r) => [r.brand, r.exclusions])),
  };
  const maps = repos.map(modules);
  const common = new Set(policy.requiredCommonModules);
  for (const name of sortedUnion(...maps.map((m) => [...m.keys()]), policy.requiredCommonModules)) {
    const presence = maps.map((m) => m.has(name));
    if (presence.every(Boolean)) common.add(name);
    const isCommon = common.has(name);
    const product = repos.some((r, i) => presence[i] && policy.productModules[r.brand]?.[name]);
    const classification = isCommon ? 'common' : product ? 'product-only' : 'unclassified';
    report.inventory.modules.push({ name, classification, presentIn: brands.filter((_, i) => presence[i]) });
    if (isCommon && !presence.every(Boolean)) report.issues.push({ code: 'missing-common-module', name, missingIn: brands.filter((_, i) => !presence[i]) });
    if (classification === 'unclassified') report.issues.push({ code: 'unclassified-module', name, presentIn: brands.filter((_, i) => presence[i]) });
    for (const [i, map] of maps.entries())
      if (map.has(name) && !map.get(name).hasProject) report.issues.push({ code: 'missing-module-project-file', name, brand: brands[i] });
    if (isCommon)
      report.areas.push(
        compare(
          repos,
          'module',
          name,
          maps.map((m) => m.get(name)?.root),
        ),
      );
  }
  const tests = repos.map(projects);
  for (const name of sortedUnion(...tests.map((m) => [...m.keys()]))) {
    const base = name.replace(/\.(UnitTests|IntegrationTests|PerformanceTests)$/, '');
    const module = policy.testAliases[base] ?? base;
    const classification = common.has(module)
      ? 'common'
      : module === 'API'
        ? 'host'
        : report.inventory.modules.some((m) => m.name === module)
          ? 'product'
          : 'unclassified';
    const presence = tests.map((m) => m.has(name));
    report.inventory.tests.push({ name, module, classification, presentIn: brands.filter((_, i) => presence[i]) });
    if (classification === 'unclassified') report.issues.push({ code: 'unclassified-test-project', name, presentIn: brands.filter((_, i) => presence[i]) });
    if (classification !== 'common') continue;
    if (!presence.every(Boolean)) report.issues.push({ code: 'missing-test-project', name, module, missingIn: brands.filter((_, i) => !presence[i]) });
    report.areas.push(
      compare(
        repos,
        'test',
        name,
        tests.map((m) => m.get(name)),
      ),
    );
  }
  for (const name of policy.requiredCommonHostPaths ?? []) {
    const roots = repos.map(repo => `apps/api/Source/${repo.brand}.API/${name}`);
    const presence = repos.map((repo, index) => [...repo.files.keys()].some(file => file === roots[index] || file.startsWith(`${roots[index]}/`)));
    if (!presence.every(Boolean)) report.issues.push({ code: 'missing-common-host-path', name, missingIn: brands.filter((_, index) => !presence[index]) });
    report.areas.push(compare(repos, 'shared-host', name, roots));
  }
  for (const name of policy.requiredCommonHostTestPaths ?? []) {
    const roots = repos.map((repo, index) => `${tests[index].get('API.UnitTests') ?? `apps/api/Tests/${repo.brand}.API.UnitTests`}/${name}`);
    const presence = repos.map((repo, index) => [...repo.files.keys()].some(file => file === roots[index] || file.startsWith(`${roots[index]}/`)));
    if (!presence.every(Boolean)) report.issues.push({ code: 'missing-common-host-test-path', name, missingIn: brands.filter((_, index) => !presence[index]) });
    report.areas.push(compare(repos, 'shared-host-test', name, roots));
  }
  if (scope === 'all') {
    const npm = repos.map(packages);
    for (const name of sortedUnion(...npm.map((m) => [...m.keys()]), policy.requiredCommonPackages ?? [])) {
      const presence = npm.map((m) => m.has(name));
      const isCommon = presence.every(Boolean) || (policy.requiredCommonPackages ?? []).includes(name);
      report.inventory.packages.push({ name, classification: isCommon ? 'common' : 'one-sided', presentIn: brands.filter((_, i) => presence[i]) });
      if (!isCommon) continue;
      if (!presence.every(Boolean)) report.issues.push({ code: 'missing-common-package', name, missingIn: brands.filter((_, i) => !presence[i]) });
      const roots = npm.map((m) => m.get(name));
      const generated = name === 'client' ? ['src/generated'] : [];
      report.areas.push(compare(repos, 'package', name, roots, false, generated));
      if (generated.length) {
        const generatedRoots = roots.map((r) => (r ? `${r}/src/generated` : undefined));
        if (repos.some((r, i) => [...r.files.keys()].some((p) => p.startsWith(`${generatedRoots[i]}/`))))
          report.areas.push(compare(repos, 'generated-client', name, generatedRoots, true));
      }
    }
    for (const component of ['API', 'ArchitectureAnalyzers']) {
      const roots = repos.map((r) => `apps/api/Source/${r.brand}.${component}`);
      if (repos.some((r, i) => [...r.files.keys()].some((p) => p.startsWith(`${roots[i]}/`))))
        report.areas.push(compare(repos, 'host', component, roots, true, component === 'API' ? policy.requiredCommonHostPaths ?? [] : []));
    }
    for (const name of ['Directory.Build.props', 'Directory.Packages.props', 'Directory.Build.targets']) {
      const file = `apps/api/${name}`;
      if (repos.some((r) => r.files.has(file))) report.areas.push(compare(repos, 'host', name, [file, file], true));
    }
  }
  report.summary = {
    areas: report.areas.length,
    commonModules: common.size,
    driftFiles: 0,
    advisoryDriftFiles: 0,
    equalFiles: 0,
    inventoryIssues: report.issues.length,
  };
  for (const area of report.areas) {
    report.summary.equalFiles += area.counts.equal;
    report.summary[area.advisory ? 'advisoryDriftFiles' : 'driftFiles'] += area.files.length;
  }
  report.result = report.summary.driftFiles || report.issues.length ? 'drift' : 'pass';
  if (baseline) report.delta = delta(report, baseline);
  // Refuse a mixed snapshot if a source, HEAD, index, or file inventory moved.
  for (const repo of repos) {
    if (JSON.stringify(repo.initialList) !== JSON.stringify(repo.listed()) || JSON.stringify(repo.meta) !== JSON.stringify(metadata(repo.root, repo.brand)))
      throw new Error(`Repository changed during audit: ${repo.root}. Rerun when edits settle.`);
    for (const info of repo.files.values()) {
      if (!fs.existsSync(info.absolute) || signature(fs.statSync(info.absolute)) !== info.signature)
        throw new Error(`Source changed during audit: ${info.absolute}. Rerun when edits settle.`);
    }
  }
  report.consistentSnapshot = true;
  return report;
}

const cell = (value) =>
  String(value ?? '')
    .replaceAll('|', '\\|')
    .replace(/[\r\n]/g, ' ');

export function markdown(report) {
  const lines = [
    '# Common module audit',
    '',
    `Generated: ${report.generatedAt}. Result: **${report.result}**. Scope: ${report.scope}.`,
    '',
    '## Repositories',
    '',
    '| Product | Branch | Commit | Local changes | Ahead / behind |',
    '| --- | --- | --- | --- | --- |',
  ];
  for (const repo of report.repositories)
    lines.push(`| ${repo.brand} | ${cell(repo.branch)} | ${repo.commit} | ${repo.dirty ? 'yes' : 'no'} | ${repo.ahead ?? '-'} / ${repo.behind ?? '-'} |`);
  lines.push(
    '',
    '## Interpretation',
    '',
    report.normalization,
    '',
    ...report.limitations.map((s) => `- ${s}`),
    '',
    `Compared ${report.summary.commonModules} common API modules. ${report.summary.driftFiles} common-file findings; ${report.summary.advisoryDriftFiles} advisory-file findings (host/generated client); ${report.summary.inventoryIssues} inventory findings.`,
    '',
  );
  if (report.delta)
    lines.push(
      `Baseline: ${report.delta.added.length} added, ${report.delta.resolved.length} resolved, ${report.delta.changed.length} changed, ${report.delta.unchanged.length} unchanged findings.`,
      '',
    );
  lines.push('## Inventory findings', '', '| Code | Name | Details |', '| --- | --- | --- |');
  for (const issue of report.issues) lines.push(`| ${issue.code} | ${cell(issue.name)} | ${cell(JSON.stringify(issue))} |`);
  lines.push('', '## Areas', '', '| Kind | Area | Equal | Different | Only ModuEstate | Only GameGuild |', '| --- | --- | ---: | ---: | ---: | ---: |');
  for (const area of report.areas)
    lines.push(
      `| ${area.kind}${area.advisory ? ' (advisory)' : ''} | ${cell(area.name)} | ${area.counts.equal} | ${area.counts.different} | ${area.counts.onlyModuEstate} | ${area.counts.onlyGameGuild} |`,
    );
  lines.push(
    '',
    '## Complete file findings',
    '',
    'Paths below are relative to the area roots listed in report.json. All file findings and SHA-256 evidence are retained in report.json.',
    '',
  );
  for (const area of report.areas.filter((a) => a.files.length)) {
    lines.push(`### ${area.kind}: ${cell(area.name)}`, '', '| Status | File |', '| --- | --- |');
    for (const file of area.files) lines.push(`| ${file.status} | ${cell(file.path)} |`);
    lines.push('');
  }
  return `${lines.join('\n')}\n`;
}
