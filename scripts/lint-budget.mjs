#!/usr/bin/env node
/**
 * Suppression-count ratchet: fails if any package's total suppressed-violation
 * count grew vs the checked-in budget (lint-budgets.json).
 *
 * Usage:
 *   node scripts/lint-budget.mjs            # check mode (exit 1 on growth)
 *   node scripts/lint-budget.mjs --update   # rewrite lint-budgets.json with current counts
 */
import { readFile, writeFile, access } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const budgetFile = resolve(root, 'lint-budgets.json');

const PACKAGES = [
  'apps/web',
  'packages/ui',
  'packages/infrastructure/client',
];

async function countPackage(pkgDir) {
  const file = resolve(root, pkgDir, 'eslint-suppressions.json');
  const content = await readFile(file, 'utf8').catch((error) => {
    if (error.code === 'ENOENT') {
      throw new Error(`missing suppression file: ${pkgDir}/eslint-suppressions.json`, { cause: error });
    }
    throw error;
  });
  const suppressions = JSON.parse(content);
  // shape: { file: { rule: { count: N } } }
  let total = 0;
  for (const rules of Object.values(suppressions)) {
    for (const { count } of Object.values(rules)) {
      total += count;
    }
  }
  return total;
}

const current = Object.fromEntries(
  await Promise.all(PACKAGES.map(async (pkg) => [pkg, await countPackage(pkg)])),
);

if (process.argv.includes('--update')) {
  await writeFile(budgetFile, `${JSON.stringify(current, null, 2)}\n`);
  console.log('lint-budgets.json updated:');
  for (const [pkg, count] of Object.entries(current)) {
    console.log(`  ${pkg}: ${count}`);
  }
  process.exit(0);
}

const budgetExists = await access(budgetFile).then(() => true, (error) => {
  if (error.code === 'ENOENT') return false;
  throw error;
});
if (!budgetExists) {
  console.error('lint-budgets.json not found. Run: node scripts/lint-budget.mjs --update');
  process.exit(1);
}

const budget = JSON.parse(await readFile(budgetFile, 'utf8'));

const rows = [];
let failed = false;
for (const pkg of PACKAGES) {
  const now = current[pkg];
  const budgeted = budget[pkg] ?? Infinity;
  const delta = now - budgeted;
  if (delta > 0) {
    failed = true;
    rows.push(`FAIL  ${pkg}: ${budgeted} -> ${now} (+${delta}) — new suppressed violations`);
  } else {
    rows.push(`OK    ${pkg}: ${budgeted} -> ${now} (${delta})`);
  }
}

console.log('package                               budget  current  delta');
console.log('-'.repeat(64));
for (const pkg of PACKAGES) {
  const now = current[pkg];
  const budgeted = budget[pkg] ?? 'n/a';
  const delta = now - budgeted;
  const mark = delta > 0 ? 'GREW' : 'ok';
  console.log(`${pkg.padEnd(38)}${String(budgeted).padStart(6)} ${String(now).padStart(8)}  ${delta >= 0 ? '+' : ''}${delta}  ${mark}`);
}

if (failed) {
  console.error('\nLint budget exceeded. Fix the new violations or shrink an existing suppression — do not raise the budget.');
  process.exit(1);
}
console.log('\nLint budgets OK.');
