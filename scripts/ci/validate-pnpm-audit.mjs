import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const severityLevels = ['info', 'low', 'moderate', 'high', 'critical'];

const bracesAdvisory = {
  moduleName: 'braces',
  id: 'GHSA-vfj7-8cjw-p6xm',
  url: 'https://github.com/advisories/GHSA-vfj7-8cjw-p6xm',
  cve: 'CVE-2026-93687',
  patchedVersion: '3.0.3',
};
const sprintfAdvisory = {
  moduleName: 'sprintf-js',
  id: 'GHSA-hp3w-g68c-fv3c',
  url: 'https://github.com/advisories/GHSA-hp3w-g68c-fv3c',
  cve: 'CVE-2026-97058',
  patchedVersion: '1.1.3',
};

const readCounts = (report) => report?.metadata?.vulnerabilities;
const readAdvisories = (report) => report?.advisories;
const hasReportError = (report) => report?.error;
const isAdvisoryCollection = (advisories) => advisories && typeof advisories === 'object' && !Array.isArray(advisories);

const requireCompleteReport = (report, counts, advisories) => {
  if (hasReportError(report) || !counts || !isAdvisoryCollection(advisories)) {
    throw new Error('pnpm audit did not return a complete advisory report');
  }
};

const requireValidSeverityCount = (counts, level) => {
  if (!Number.isSafeInteger(counts[level]) || counts[level] < 0) {
    throw new Error(`Invalid pnpm audit vulnerability count: ${level}`);
  }
};

const requireValidSeverityCounts = (counts) => {
  for (const level of severityLevels) {
    requireValidSeverityCount(counts, level);
  }
};

const matchesAdvisoryIdentity = (advisory, expected) =>
  advisory.module_name === expected.moduleName && advisory.github_advisory_id === expected.id && advisory.url === expected.url;

const hasSingleExpectedCve = (advisory, expected) => Array.isArray(advisory.cves) && advisory.cves.length === 1 && advisory.cves[0] === expected.cve;

const hasOnlyPatchedFindings = (advisory, expected) =>
  Array.isArray(advisory.findings) && advisory.findings.length > 0 && advisory.findings.every((finding) => finding.version === expected.patchedVersion);

const matchesPatchedAdvisory = (advisory, expected) =>
  matchesAdvisoryIdentity(advisory, expected) && hasSingleExpectedCve(advisory, expected) && hasOnlyPatchedFindings(advisory, expected);

const isLocallyPatchedAdvisory = (advisory, verifiedBracesPatch, verifiedSprintfPatch) => {
  const knownBraces = verifiedBracesPatch && matchesPatchedAdvisory(advisory, bracesAdvisory);
  const knownSprintf = verifiedSprintfPatch && matchesPatchedAdvisory(advisory, sprintfAdvisory);
  return knownBraces || knownSprintf;
};

const requireLocallyPatchedAdvisory = (advisory, verifiedBracesPatch, verifiedSprintfPatch) => {
  if (!isLocallyPatchedAdvisory(advisory, verifiedBracesPatch, verifiedSprintfPatch)) {
    throw new Error(`Unmitigated dependency advisory: ${advisory.github_advisory_id ?? advisory.id ?? 'unknown'}`);
  }
};

const requireAllAdvisoriesPatched = (items, verifiedBracesPatch, verifiedSprintfPatch) => {
  for (const advisory of items) {
    requireLocallyPatchedAdvisory(advisory, verifiedBracesPatch, verifiedSprintfPatch);
  }
};

const requireConsistentCounts = (reportedCount, findingCount) => {
  if (reportedCount !== findingCount) {
    throw new Error('pnpm audit metadata and advisory details disagree');
  }
};

const requireConsistentExitCode = (exitCode, advisoryCount) => {
  if (exitCode !== (advisoryCount === 0 ? 0 : 1)) {
    throw new Error(`pnpm audit failed or returned an inconsistent exit code: ${exitCode}`);
  }
};

const validateAuditReport = (report, exitCode, verifiedBracesPatch = false, verifiedSprintfPatch = false) => {
  const counts = readCounts(report),
    advisories = readAdvisories(report);
  requireCompleteReport(report, counts, advisories);
  requireValidSeverityCounts(counts);
  const items = Object.values(advisories);
  const reportedCount = severityLevels.reduce((sum, level) => sum + counts[level], 0);
  requireAllAdvisoriesPatched(items, verifiedBracesPatch, verifiedSprintfPatch);
  // pnpm counts each workspace finding, while advisories are deduplicated.
  // Only verified advisories reach this point, so every findings array exists.
  const findingCount = items.reduce((sum, advisory) => sum + advisory.findings.length, 0);
  requireConsistentCounts(reportedCount, findingCount);
  requireConsistentExitCode(exitCode, items.length);
  return { advisories: items.length, verifiedLocalPatches: items.length };
};

const invokedDirectly = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invokedDirectly) {
  try {
    const report = JSON.parse(readFileSync(process.argv[2], 'utf8'));
    const patchCheck = spawnSync(
      process.execPath,
      [
        '--test',
        resolve(dirname(fileURLToPath(import.meta.url)), 'tests/braces-security.test.mjs'),
        resolve(dirname(fileURLToPath(import.meta.url)), 'tests/sprintf-security.test.mjs'),
      ],
      { stdio: 'inherit' },
    );
    if (patchCheck.error || patchCheck.status !== 0) throw new Error('Installed dependency security patch verification failed');
    const result = validateAuditReport(report, Number(process.argv[3]), true, true);
    console.log(`Dependency audit passed: ${result.advisories} reported advisories, ${result.verifiedLocalPatches} verified local patches`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

export { validateAuditReport };
