import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import ts from 'typescript';
import prettier from 'prettier';

const sha = (v) => createHash('sha256').update(v).digest('hex');
const normalize = (v) =>
  v
    .replace(/^\uFEFF/, '')
    .replace(/\r\n?/g, '\n')
    .replace(/ModuEstate|GameGuild/g, '__PROJECT__')
    .replace(/modu-estate|game-guild/g, '__project-slug__')
    .replace(/modu_estate|game_guild/g, '__project_snake__')
    .replace(/moduestate|gameguild/g, '__projectlower__')
    .replace(/MODUESTATE|GAMEGUILD/g, '__PROJECT_UPPER__')
    .replace(/Modu Estate|Game Guild/g, '__Project Words__');
const stable = (v) =>
  Array.isArray(v)
    ? v.map(stable)
    : v && typeof v === 'object'
      ? Object.fromEntries(
          Object.keys(v)
            .sort()
            .map((k) => [k, stable(v[k])]),
        )
      : v;

export async function classifyTextPair(file, left, right) {
  const ext = path.extname(file).toLowerCase();
  if (ext === '.json') {
    try {
      if (JSON.stringify(stable(JSON.parse(left))) === JSON.stringify(stable(JSON.parse(right))))
        return { category: 'data-equivalent', reason: 'JSON values match after object-key ordering; array order and values retained.' };
    } catch {
      /* Invalid/non-JSON data stays reviewable. */
    }
  }
  if (['.ts', '.tsx', '.js', '.mjs'].includes(ext)) {
    const kind = ext === '.tsx' ? ts.ScriptKind.TSX : ext === '.ts' ? ts.ScriptKind.TS : ts.ScriptKind.JS;
    const sources = [left, right].map((text) => ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, kind));
    if (sources.every((s) => s.parseDiagnostics.length === 0)) {
      // Compiler, bundler and type-reference comments can affect behavior.
      const directives = (text) =>
        (text.match(/\/\/[^\n]*|\/\*[\s\S]*?\*\//g) ?? [])
          .filter((c) => /@ts-|@jsx|@vite|@license|eslint|istanbul|#__PURE__|#?sourceMappingURL|<reference\b/i.test(c))
          .join('\n');
      const printer = ts.createPrinter({ removeComments: true });
      const printed = await Promise.all(
        sources.map((s) =>
          prettier.format(printer.printFile(s), { parser: ext === '.tsx' ? 'typescript' : ext === '.ts' ? 'typescript' : 'babel', singleQuote: true }),
        ),
      );
      // Directive placement matters (e.g. @ts-ignore applies to the next line).
      // Never auto-approve a changed source containing compiler/tool directives.
      if (printed[0] === printed[1] && !directives(left) && !directives(right))
        return { category: 'syntax-equivalent', reason: 'TypeScript parsed/printer output matches after formatting; no tool directives.' };
    }
  }
  return { category: 'review-required', reason: 'Changed source/configuration requires review.' };
}

export function verifyInput(report) {
  if (report.schemaVersion !== 1 || !report.consistentSnapshot || !Array.isArray(report.areas) || report.repositories?.length !== 2)
    throw new Error('Invalid/incomplete source audit.');
  const roots = Object.fromEntries(report.repositories.map((r) => [r.brand, r.root]));
  const rows = [];
  for (const area of report.areas.filter((a) => !a.advisory))
    for (const file of area.files) {
      const texts = {};
      for (const brand of ['ModuEstate', 'GameGuild']) {
        const evidence = file[brand];
        if (!evidence) {
          if (area.roots[brand]) {
            const otherBrand = brand === 'ModuEstate' ? 'GameGuild' : 'ModuEstate';
            const otherRoot = area.roots[otherBrand];
            const rel = file[otherBrand].path.slice(otherRoot.length + 1).replaceAll(otherBrand, brand);
            if (fs.existsSync(path.join(roots[brand], area.roots[brand], rel)))
              throw new Error(`Previously absent counterpart now exists: ${area.name}/${rel}`);
          }
          texts[brand] = '';
          continue;
        }
        const absolute = path.resolve(roots[brand], evidence.path);
        const relative = path.relative(path.resolve(roots[brand]), absolute);
        if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Input path escapes repository.');
        const bytes = fs.readFileSync(absolute);
        if (sha(bytes) !== evidence.rawSha256) throw new Error(`Stale source audit: ${absolute}`);
        texts[brand] = normalize(bytes.toString('utf8'));
      }
      rows.push({
        id: `${area.kind}:${area.name}:${file.path}`,
        kind: area.kind,
        area: area.name,
        path: file.path,
        status: file.status,
        fingerprint: sha(JSON.stringify([file.status, file.ModuEstate?.rawSha256, file.GameGuild?.rawSha256])),
        evidence: file,
        texts,
      });
    }
  if (rows.length !== report.summary.driftFiles) throw new Error('Audit count mismatch.');
  if (new Set(rows.map((r) => r.id)).size !== rows.length) throw new Error('Duplicate audit row.');
  return rows;
}

export function csharp(pairs) {
  const project = fileURLToPath(new URL('../analysis/ParitySyntax/ParitySyntax.csproj', import.meta.url));
  execFileSync('dotnet', ['restore', project, '--source', path.dirname(project), '--nologo'], { stdio: ['ignore', 'pipe', 'pipe'] });
  execFileSync('dotnet', ['build', project, '-c', 'Release', '--no-restore', '--nologo', '-p:UseSharedCompilation=false'], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const dll = path.join(path.dirname(project), 'bin/Release/net10.0/ParitySyntax.dll');
  return JSON.parse(execFileSync('dotnet', [dll], { input: JSON.stringify(pairs), encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 }));
}

// Triage is a routing decision, NOT merge approval or a correctness proof.
export function triageDifference(row) {
  const { area, path: file, kind } = row;
  const make = (category, action) => ({ category, action, method: 'scripted-routing', approvedForSync: false });
  if (kind === 'test' || /(^|\/)tests\//.test(file))
    return make('test-contract-drift', 'Review assertions alongside the corresponding source; passing divergent suites does not prove parity.');
  if (/\.md$/.test(file)) return make('documentation-drift', 'Reconcile documentation against each product and package contract.');
  if (file === '.gitignore' || /\.(?:json|csproj)$/.test(file) || /config\.(?:m?js|ts)$/.test(file))
    return make(
      'build-configuration-drift',
      'Compare dependencies, exports and compiler options; update with lockfiles and consumer checks, not file copying.',
    );
  if (kind === 'package') {
    if (area === 'ui')
      return make(
        'ui-contract-migration',
        'Base UI/GameGuild and Radix/ModuEstate have different props, composition and styling; migrate consumers explicitly.',
      );
    if (area === 'client')
      return make('client-contract-drift', 'Coordinate API routes, auth callbacks, response metadata and dependency versions with the matching backend.');
    return make('tooling-drift', 'Review shared tool behavior against both projects.');
  }
  if (file.startsWith('Architecture/'))
    return make(
      'event-contract-addition',
      'Preserve ModuEstate event declarations; port only together with durable pipeline, analyzer, outbox/inbox and host wiring.',
    );
  if (area === 'Identity.Authorization' || area === 'Commerce.Products')
    return make('authorization-boundary-review', 'Review fail-closed, tenant/creator and permission checks; do not replace with the weaker variant.');
  if (area === 'Identity.Authentication')
    return make('authentication-contract-review', 'Reconcile step-up, claims, password/session behavior, OAuth routes and durable email listeners.');
  if (area === 'Assets')
    return make(
      'asset-lifecycle-review',
      'Preserve secure upload, signed transformation binding and physical/logical event accounting; validate storage and scan failure paths.',
    );
  if (area === 'Resources' || area === 'SharedKernel')
    return make(
      'durable-accounting-infrastructure',
      'Preserve durable events, quota ledger and internal costing; requires transactional host wiring and database tests, not shared files alone.',
    );
  if (area === 'Notifications' || area === 'Commerce.Subscriptions' || area === 'Identity.Tenants')
    return make(
      'notification-delivery-migration',
      'Queue/digest/suppression and direct email delivery differ; coordinate schema, renderers, consumers, retries and host configuration.',
    );
  if (area === 'Compliance.KYC' || area.startsWith('Commerce'))
    return make(
      'product-capability-extension',
      'Assess GameGuild provider/marketplace dependencies; keep product-specific adapters separate from shared contracts.',
    );
  if (/Commands\//.test(file) || /Controllers\//.test(file))
    return make(
      'cqrs-mutation-routing',
      'Review controller-to-command relocation with existing authorization and error behavior; retain event production guards.',
    );
  return make('functional-review', 'Functional difference not automatically approved; review the linked source evidence.');
}

export function verifyReviews(rows, review) {
  const ids = new Map(rows.map((r) => [r.id, r]));
  for (const [id, decision] of Object.entries(review.entries ?? {})) {
    if (!ids.has(id)) throw new Error(`Unknown review row: ${id}`);
    if (decision.fingerprint !== ids.get(id).fingerprint) throw new Error(`Stale review: ${id}`);
    if (!decision.category?.trim() || !decision.reason?.trim()) throw new Error(`Incomplete review: ${id}`);
  }
}

export async function validate(report, output, review = {}) {
  const rows = verifyInput(report);
  verifyReviews(rows, review);
  const csharpPairs = rows.filter((r) => r.path.endsWith('.cs')).map((r) => ({ Id: r.id, Left: r.texts.ModuEstate, Right: r.texts.GameGuild }));
  const syntax = new Map(csharp(csharpPairs).map((r) => [r.Id, r]));
  if (fs.existsSync(output)) throw new Error(`Output exists: ${output}`);
  fs.mkdirSync(path.join(output, 'evidence'), { recursive: true });
  const reviewed = [];
  for (const [i, row] of rows.entries()) {
    let classification;
    const parsed = syntax.get(row.id);
    if (row.status !== 'different')
      classification = { category: 'one-sided-source', reason: `Source exists only in ${row.status === 'onlyModuEstate' ? 'ModuEstate' : 'GameGuild'}.` };
    else if (parsed?.Equal && !parsed.LeftErrors.length && !parsed.RightErrors.length)
      classification = {
        category: 'syntax-equivalent',
        reason: 'Roslyn token kinds/values match; directives and disabled code retained. Differences are trivia.',
      };
    else classification = await classifyTextPair(row.path, row.texts.ModuEstate, row.texts.GameGuild);
    const decision = review.entries?.[row.id];
    if (decision && decision.fingerprint !== row.fingerprint) throw new Error(`Stale review: ${row.id}`);
    const evidenceDir = path.join(output, 'evidence', String(i + 1).padStart(3, '0'));
    fs.mkdirSync(evidenceDir);
    for (const brand of ['ModuEstate', 'GameGuild']) fs.writeFileSync(path.join(evidenceDir, brand + path.extname(row.path)), row.texts[brand]);
    const result = spawnSync(
      'git',
      [
        'diff',
        '--no-index',
        '--no-ext-diff',
        '--no-color',
        '--unified=2',
        '--',
        path.join(evidenceDir, 'GameGuild' + path.extname(row.path)),
        path.join(evidenceDir, 'ModuEstate' + path.extname(row.path)),
      ],
      { encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 },
    );
    if (![0, 1].includes(result.status)) throw new Error(`Diff failed: ${row.id}: ${result.stderr}`);
    fs.writeFileSync(path.join(evidenceDir, 'change.diff'), result.stdout);
    const { texts, ...publicRow } = row;
    let syntaxDiagnostics = parsed ? { ModuEstate: parsed.LeftErrors, GameGuild: parsed.RightErrors } : null;
    if (/\.(?:tsx?|m?js)$/.test(row.path)) {
      syntaxDiagnostics = Object.fromEntries(
        ['ModuEstate', 'GameGuild'].map((brand) => [
          brand,
          ts
            .createSourceFile(
              row.path,
              row.texts[brand],
              ts.ScriptTarget.Latest,
              true,
              row.path.endsWith('.tsx') ? ts.ScriptKind.TSX : row.path.endsWith('.ts') ? ts.ScriptKind.TS : ts.ScriptKind.JS,
            )
            .parseDiagnostics.map((d) => ts.flattenDiagnosticMessageText(d.messageText, '\n')),
        ]),
      );
    }
    reviewed.push({
      ...publicRow,
      classification,
      triage: triageDifference(row),
      syntaxDiagnostics,
      review: decision ?? null,
      diff: `evidence/${String(i + 1).padStart(3, '0')}/change.diff`,
    });
  }
  verifyInput(report);
  const counts = {};
  for (const row of reviewed)
    counts[row.review?.category ?? row.classification.category] = (counts[row.review?.category ?? row.classification.category] ?? 0) + 1;
  const pending = reviewed.filter((r) => !r.review && !['syntax-equivalent', 'data-equivalent'].includes(r.classification.category));
  const triageCounts = {};
  for (const row of reviewed) triageCounts[row.triage.category] = (triageCounts[row.triage.category] ?? 0) + 1;
  const equivalent = reviewed.filter((r) => ['syntax-equivalent', 'data-equivalent'].includes(r.classification.category)).length;
  const result = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    sourceAuditGeneratedAt: report.generatedAt,
    repositories: report.repositories,
    summary: {
      total: rows.length,
      triaged: reviewed.length,
      equivalent,
      requiresDecision: rows.length - equivalent,
      pending: pending.length,
      categories: counts,
      triage: triageCounts,
      syntaxCheckedFiles: reviewed.filter((r) => r.syntaxDiagnostics).length,
      syntaxErrorFiles: reviewed.filter((r) => Object.values(r.syntaxDiagnostics ?? {}).some((e) => e.length)).length,
    },
    rows: reviewed,
  };
  fs.writeFileSync(path.join(output, 'validation.json'), JSON.stringify(result, null, 2) + '\n');
  const lines = [
    '# Validated common differences',
    '',
    `Source differences: ${rows.length}. Pending static review: ${pending.length}.`,
    '',
    'All rows are hash-checked and routed by script. Triage is not manual review or approval to synchronize. Syntax checking is not type checking. Formatting equivalence does not establish complete runtime correctness.',
    '',
    '| # | Area / file | Classification | Triage / next action | Evidence |',
    '| ---: | --- | --- | --- | --- |',
  ];
  reviewed.forEach((r, i) =>
    lines.push(
      `| ${i + 1} | ${r.id.replaceAll('|', '\\|')} | ${r.review?.category ?? r.classification.category} | ${r.triage.category}: ${(r.review?.reason ?? r.triage.action).replaceAll('|', '\\|')} | [diff](${r.diff}) |`,
    ),
  );
  fs.writeFileSync(path.join(output, 'validation.md'), lines.join('\n') + '\n');
  return result;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try {
    const { values } = parseArgs({ options: { report: { type: 'string' }, output: { type: 'string' }, review: { type: 'string' } } });
    if (!values.report || !values.output) throw new Error('Usage: --report report.json --output NEW_DIRECTORY [--review review.json]');
    const result = await validate(
      JSON.parse(fs.readFileSync(values.report, 'utf8')),
      path.resolve(values.output),
      values.review ? JSON.parse(fs.readFileSync(values.review, 'utf8')) : {},
    );
    console.log(JSON.stringify(result.summary, null, 2));
    process.exitCode = result.summary.pending ? 1 : 0;
  } catch (error) {
    console.error(error.message, error.stderr?.toString() ?? '');
    process.exitCode = 2;
  }
}
