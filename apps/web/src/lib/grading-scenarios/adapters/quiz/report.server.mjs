import { execFileSync } from "node:child_process";
import { mkdir, readdir, writeFile } from "node:fs/promises";
import { relative, resolve, sep } from "node:path";
import {
  VALIDATION_RESULT_STATUSES,
  getValidationCase,
} from "./capability-catalog.mjs";
import { REPO_ROOT, WEB_ROOT, writeJsonAtomic } from "./manifest.server.mjs";

export const VALIDATION_RESULTS_ROOT = resolve(
  WEB_ROOT,
  "test-results/quiz-grading-validation",
);

const SENSITIVE_KEY =
  /(?:access|refresh)?token|authorization|cookie|password|secret|answer.?key/i;
const PRIVATE_AUTHORING_KEY =
  /^(?:correctAnswer|correctAnswerPlain|correctOptionId|correctOptionIds|acceptedAnswers|correctRating|correctValue|correctCategoryIds|correctPosition|highlights|hotspots|formula)$/i;

export function validationRunDirectory(runId) {
  assertSafeSegment(runId, "validation run ID");
  return resolve(VALIDATION_RESULTS_ROOT, runId);
}

export function validationEvidenceDirectory(runId, caseId) {
  getValidationCase(caseId);
  return resolve(validationRunDirectory(runId), "evidence", caseId);
}

export async function listValidationEvidence(runId, caseId) {
  const runDirectory = validationRunDirectory(runId);
  const evidenceDirectory = validationEvidenceDirectory(runId, caseId);
  const files = await walkFiles(evidenceDirectory);
  return files
    .map((path) => relative(runDirectory, path).split(sep).join("/"))
    .sort();
}

export function createValidationReport({
  manifest,
  scenarioReport,
  cases,
  results = [],
  completedAutomations = ["api"],
  now = new Date(),
}) {
  const resultByCase = new Map(
    results.map((result) => [result.caseId, result]),
  );
  const normalizedCases = cases.map((candidate) => {
    const observed = resultByCase.get(candidate.id);
    const quizKey = candidate.quizKey ?? candidate.key;
    const relevantChecks = (scenarioReport?.checks ?? []).filter((check) => {
      const scopedCheck = check.name.match(/^\[([^\]]+)\]/);
      return !scopedCheck || !quizKey || scopedCheck[1] === quizKey;
    });
    const failedChecks = relevantChecks.filter((check) => !check.ok);
    const automationDeclaredComplete = completedAutomations.includes(
      candidate.automation,
    );
    const automationCompleted =
      candidate.automation === "browser"
        ? automationDeclaredComplete && Boolean(observed)
        : automationDeclaredComplete;
    const missingBrowserEvidence =
      candidate.automation === "browser" &&
      observed?.status === "pass" &&
      (observed.evidence?.length ?? 0) === 0;
    const status =
      failedChecks.length > 0
        ? "bug"
        : missingBrowserEvidence
          ? "harness-gap"
          : (observed?.status ?? inferStatus(candidate, automationCompleted));
    if (!VALIDATION_RESULT_STATUSES.includes(status)) {
      throw new Error(
        `Validation result ${candidate.id} has unknown status "${status}".`,
      );
    }
    return {
      caseId: candidate.id,
      suite: candidate.suite,
      capability: candidate.capability,
      requirement: candidate.requirement,
      automation: candidate.automation,
      status,
      owner: candidate.baseline.owner,
      reason:
        failedChecks.length > 0
          ? failedChecks
              .map((check) => `${check.name}: ${check.detail ?? "failed"}`)
              .join("; ")
          : missingBrowserEvidence
            ? "Browser automation completed without preserving evidence for this case."
            : observed
              ? (observed.reason ?? null)
              : !automationCompleted
                ? `The ${candidate.automation} automation required by this case was not executed by this command.`
                : candidate.baseline.reason,
      tracking: observed
        ? (observed.tracking ?? null)
        : candidate.baseline.tracking,
      evidence: observed?.evidence ?? [],
      checks: relevantChecks,
      execution: {
        requiredAutomation: candidate.automation,
        completedAutomations: completedAutomations.filter(
          (automation) =>
            automation !== candidate.automation || automationCompleted,
        ),
        automationCompleted,
      },
    };
  });

  const report = {
    schemaVersion: 1,
    program: "quiz-grading-capability-audit",
    runId: manifest.runId,
    scenario: manifest.definitionKey,
    commit: readGitCommit(),
    generatedAt: now.toISOString(),
    environment: {
      node: process.version,
      platform: process.platform,
      apiBaseUrl: manifest.environment.apiBaseUrl,
      webBaseUrl: manifest.environment.webBaseUrl,
      definitionSchemaVersion: manifest.definitionSchemaVersion,
      manifestSchemaVersion: manifest.schemaVersion,
    },
    summary: summarize(normalizedCases),
    cases: normalizedCases,
  };
  return sanitizeEvidence(report);
}

export async function writeValidationReport(report) {
  const directory = validationRunDirectory(report.runId);
  assertUnderRoot(directory, VALIDATION_RESULTS_ROOT);
  await mkdir(resolve(directory, "evidence"), { recursive: true, mode: 0o700 });
  await writeJsonAtomic(resolve(directory, "manifest.json"), {
    schemaVersion: report.schemaVersion,
    program: report.program,
    runId: report.runId,
    scenario: report.scenario,
    commit: report.commit,
    generatedAt: report.generatedAt,
    environment: report.environment,
    caseIds: report.cases.map((candidate) => candidate.caseId),
  });
  await writeJsonAtomic(resolve(directory, "report.json"), report);
  await writeFile(
    resolve(directory, "report.md"),
    renderValidationReport(report),
    {
      encoding: "utf8",
      mode: 0o600,
    },
  );
  return {
    directory,
    manifest: resolve(directory, "manifest.json"),
    json: resolve(directory, "report.json"),
    markdown: resolve(directory, "report.md"),
  };
}

export function renderValidationReport(report) {
  const lines = [
    "# Quiz grading validation report",
    "",
    `- Run: \`${report.runId}\``,
    `- Scenario: \`${report.scenario}\``,
    `- Commit: \`${report.commit ?? "unknown"}\``,
    `- Generated: ${report.generatedAt}`,
    "",
    "## Summary",
    "",
    "| Status | Cases |",
    "| --- | ---: |",
    ...VALIDATION_RESULT_STATUSES.filter(
      (status) => (report.summary.byStatus[status] ?? 0) > 0,
    ).map((status) => `| \`${status}\` | ${report.summary.byStatus[status]} |`),
    "",
    "## Cases",
    "",
    "| Case | Suite | Status | Owner | Evidence |",
    "| --- | --- | --- | --- | --- |",
    ...report.cases.map(
      (candidate) =>
        `| \`${candidate.caseId}\` | ${candidate.suite} | \`${candidate.status}\` | ${candidate.owner} | ${candidate.evidence.length} |`,
    ),
  ];
  const diagnostics = report.cases.filter(
    (candidate) => candidate.status !== "pass" || candidate.reason,
  );
  if (diagnostics.length > 0) {
    lines.push("", "## Diagnostics", "");
    for (const candidate of diagnostics) {
      lines.push(
        `### ${candidate.caseId}`,
        "",
        `Status: \`${candidate.status}\`.`,
        "",
        candidate.reason ?? "No additional diagnostic was recorded.",
        "",
        ...(candidate.tracking
          ? [`Tracking: \`${candidate.tracking}\`.`, ""]
          : []),
      );
    }
  }
  return `${lines.join("\n")}\n`;
}

export function sanitizeEvidence(value, seen = new WeakSet()) {
  if (Array.isArray(value)) {
    return value.map((entry) => sanitizeEvidence(entry, seen));
  }
  if (!value || typeof value !== "object") return value;
  if (seen.has(value)) return "[circular]";
  seen.add(value);
  const result = {};
  for (const [key, child] of Object.entries(value)) {
    if (SENSITIVE_KEY.test(key)) {
      result[key] = "[redacted]";
      continue;
    }
    if (PRIVATE_AUTHORING_KEY.test(key)) {
      result[key] = "[authoring-private]";
      continue;
    }
    result[key] = sanitizeEvidence(child, seen);
  }
  seen.delete(value);
  return result;
}

function inferStatus(candidate, automationCompleted) {
  if (candidate.baseline.status === "pass" && !automationCompleted) {
    return "partial";
  }
  return candidate.baseline.status;
}

function summarize(cases) {
  return {
    total: cases.length,
    byStatus: Object.fromEntries(
      VALIDATION_RESULT_STATUSES.map((status) => [
        status,
        cases.filter((candidate) => candidate.status === status).length,
      ]),
    ),
    passing: cases.filter((candidate) => candidate.status === "pass").length,
    actionable: cases.filter((candidate) =>
      ["bug", "partial", "missing", "harness-gap"].includes(candidate.status),
    ).length,
  };
}

function readGitCommit() {
  try {
    return execFileSync("git", ["rev-parse", "HEAD"], {
      cwd: REPO_ROOT,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
  } catch {
    return null;
  }
}

function assertSafeSegment(value, label) {
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(value)) {
    throw new Error(`${label} contains unsafe path characters.`);
  }
}

function assertUnderRoot(target, root) {
  const normalizedRoot = `${resolve(root)}${sep}`;
  if (!`${resolve(target)}${sep}`.startsWith(normalizedRoot)) {
    throw new Error("Validation report target escapes the local results root.");
  }
}

async function walkFiles(directory) {
  let entries;
  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch (error) {
    if (error?.code === "ENOENT") return [];
    throw error;
  }
  const files = [];
  for (const entry of entries) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...(await walkFiles(path)));
    else if (entry.isFile()) files.push(path);
  }
  return files;
}
