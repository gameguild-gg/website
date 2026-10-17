#!/usr/bin/env node
import { parseArgs } from "node:util";
import {
  getValidationCase,
  selectValidationCases,
} from "./capability-catalog.mjs";
import {
  assertApplicableCheckpoint,
  getScenarioDefinition,
  listScenarioDefinitions,
  selectQuizDefinitions,
} from "./definitions.mjs";
import {
  assertLocalUrl,
  readManifest,
  withScenarioLock,
  writeReport,
} from "./manifest.mjs";

const { positionals, values } = parseArgs({
  args: process.argv.slice(2),
  allowPositionals: true,
  strict: true,
  options: {
    scenario: { type: "string", short: "s" },
    quiz: { type: "string", multiple: true },
    case: { type: "string", multiple: true },
    "question-type": { type: "string", multiple: true },
    suite: { type: "string", multiple: true },
    checkpoint: { type: "string", short: "c" },
    fresh: { type: "boolean", default: false },
    headed: { type: "boolean", default: false },
    cleanup: { type: "boolean", default: false },
    json: { type: "boolean", default: false },
    help: { type: "boolean", short: "h", default: false },
    "api-base-url": { type: "string" },
    "web-base-url": { type: "string" },
  },
});

if (values.help || positionals.length === 0) {
  printHelp();
  process.exit(values.help ? 0 : 2);
}

const command = positionals[0];
const selectedQuizKeys = parseQuizKeys(values.quiz);
const selectedCaseIds = parseQuizKeys(values.case);
const selectedQuestionTypes = parseQuizKeys(values["question-type"]);
const selectedSuites = parseQuizKeys(values.suite);
const apiBaseUrl = assertLocalUrl(
  values["api-base-url"] ??
    process.env.QUIZ_GRADING_API_BASE_URL ??
    process.env.NEXT_PUBLIC_API_URL ??
    "http://localhost:8080",
  "API base URL",
);
const webBaseUrl = assertLocalUrl(
  values["web-base-url"] ??
    process.env.QUIZ_GRADING_WEB_BASE_URL ??
    process.env.PLAYWRIGHT_WEB_BASE_URL ??
    "http://localhost:3000",
  "web base URL",
);

try {
  if (command === "list") {
    await list();
  } else if (command === "catalog") {
    await catalog();
  } else {
    const scenarioKey = requireScenario(values.scenario);
    if (command === "prepare") {
      await prepare(scenarioKey, values.checkpoint ?? "learner-ready");
    } else if (command === "status") {
      await status(scenarioKey);
    } else if (command === "verify") {
      await verify(scenarioKey);
    } else if (command === "reset") {
      await reset(scenarioKey);
    } else if (command === "test") {
      await test(scenarioKey, values.checkpoint ?? "learner-ready");
    } else {
      throw new Error(`Unknown command "${command}".`);
    }
  }
} catch (error) {
  console.error(
    values.json
      ? JSON.stringify({ ok: false, error: error.message })
      : `\n[grading:scenario] ${error.message}`,
  );
  process.exitCode = 1;
}

async function prepare(
  scenarioKey,
  checkpoint,
  quizKeys = selectedQuizKeys,
  { applyCatalogFilters = true } = {},
) {
  const definition = getScenarioDefinition(scenarioKey);
  const resolvedQuizKeys = applyCatalogFilters
    ? resolveQuizKeys(definition, quizKeys)
    : quizKeys;
  const quizzes = selectQuizDefinitions(definition, resolvedQuizKeys);
  assertApplicableCheckpoint(definition, checkpoint, quizzes);
  if (values.fresh) {
    await reset(scenarioKey);
  }
  const { prepareScenario } = await import("./provision.mjs");
  const { verifyScenario } = await import("./verify.mjs");
  progress(`Preparing "${scenarioKey}" through ${checkpoint}.`);
  const manifest = await withCliScenarioLock(scenarioKey, () =>
    prepareScenario({
      scenarioKey,
      checkpoint,
      quizKeys: resolvedQuizKeys,
      apiBaseUrl,
      webBaseUrl,
      onProgress: progress,
    }),
  );
  progress("Verifying the persisted scenario state.");
  const { report } = await verifyScenario(scenarioKey);
  await writeReport(scenarioKey, report);
  const validationArtifacts = await writeCandidateValidationReport(
    manifest,
    report,
  );
  progress("Scenario is ready. Use the URLs and persona credentials below.");
  outputScenario(manifest, report, {
    credentials: true,
    validationArtifacts,
  });
}

async function status(scenarioKey) {
  const manifest = await readManifest(scenarioKey, { optional: true });
  if (!manifest) {
    output({ scenario: scenarioKey, status: "not-prepared" });
    return;
  }
  try {
    const { verifyScenario } = await import("./verify.mjs");
    const { report } = await verifyScenario(scenarioKey, { strict: false });
    outputScenario(manifest, report, { credentials: false });
  } catch (error) {
    output({
      scenario: scenarioKey,
      declaredCheckpoint: manifest.checkpoint,
      healthy: false,
      diagnostic: error.message,
      urls: manifest.urls,
    });
  }
}

async function verify(
  scenarioKey,
  { completedAutomations = ["api"], results = [] } = {},
) {
  const { verifyScenario } = await import("./verify.mjs");
  const { manifest, report } = await verifyScenario(scenarioKey);
  await writeReport(scenarioKey, report);
  const validationArtifacts = await writeCandidateValidationReport(
    manifest,
    report,
    {
      completedAutomations,
      results,
    },
  );
  outputScenario(manifest, report, {
    credentials: false,
    validationArtifacts,
  });
}

async function reset(scenarioKey) {
  const { resetScenario } = await import("./cleanup.mjs");
  const report = await withCliScenarioLock(scenarioKey, () =>
    resetScenario(scenarioKey),
  );
  output(report);
}

async function test(scenarioKey, checkpoint) {
  const definition = getScenarioDefinition(scenarioKey);
  const resolvedQuizKeys = resolveQuizKeys(definition, selectedQuizKeys);
  const quizzes = selectQuizDefinitions(definition, resolvedQuizKeys);
  assertApplicableCheckpoint(definition, checkpoint, quizzes);
  await prepare(scenarioKey, checkpoint, resolvedQuizKeys, {
    applyCatalogFilters: false,
  });
  const { runBrowserScenario } = await import("./browser.mjs");
  await withCliScenarioLock(scenarioKey, () =>
    runBrowserScenario({
      scenarioKey,
      quizKeys: resolvedQuizKeys,
      headed: values.headed,
    }),
  );
  const manifest = await readManifest(scenarioKey);
  const passedCaseIds = validationCasesForManifest(manifest)
    .filter((candidate) => isBrowserPassCandidate(candidate, resolvedQuizKeys))
    .map((candidate) => candidate.id);
  const { listValidationEvidence } = await import("./report.mjs");
  const results = await Promise.all(
    passedCaseIds.map(async (caseId) => ({
      caseId,
      status: "pass",
      evidence: await listValidationEvidence(manifest.runId, caseId),
    })),
  );
  await verify(scenarioKey, {
    completedAutomations: ["api", "browser"],
    results,
  });
  if (values.cleanup) await reset(scenarioKey);
}

async function list() {
  const entries = [];
  for (const definition of listScenarioDefinitions()) {
    let manifest = null;
    let diagnostic = null;
    try {
      manifest = await readManifest(definition.key, { optional: true });
    } catch (error) {
      diagnostic = error.message;
    }
    entries.push({
      key: definition.key,
      subject: definition.subject,
      quizzes: definition.quizzes.map((quiz) => ({
        key: quiz.key,
        workflow: quiz.reviewMethods.join(" + "),
        validationCaseId: quiz.validationCaseId ?? null,
        capability: quiz.officialCapability ?? null,
        maxSupportedCheckpoint: quiz.maxSupportedCheckpoint,
      })),
      prepared: Boolean(manifest) || Boolean(diagnostic),
      checkpoint: manifest?.checkpoint ?? null,
      ...(diagnostic ? { healthy: false } : {}),
      ...(diagnostic ? { diagnostic } : {}),
      urls: manifest
        ? {
            learnerCourses: manifest.urls.learnerCourses,
            quizzes: manifest.urls.quizzes,
          }
        : {},
    });
  }
  output(entries);
}

async function catalog() {
  const cases = selectValidationCases({
    suites: selectedSuites,
    caseIds: selectedCaseIds,
    questionTypes: selectedQuestionTypes,
  });
  output(
    cases.map((candidate) => ({
      id: candidate.id,
      key: candidate.key ?? null,
      suite: candidate.suite,
      capability: candidate.capability,
      automation: candidate.automation,
      requirement: candidate.requirement,
      baseline: candidate.baseline,
    })),
  );
}

function outputScenario(
  manifest,
  report,
  { credentials, validationArtifacts = null },
) {
  output({
    scenario: manifest.definitionKey,
    runId: manifest.runId,
    marker: manifest.marker,
    declaredCheckpoint: manifest.checkpoint,
    confirmedCheckpoint: report.confirmedCheckpoint,
    healthy: report.healthy,
    nextCheckpoint: report.nextCheckpoint,
    checkpoints: report.checkpoints,
    urls: manifest.urls,
    ...(credentials
      ? {
          personas: Object.fromEntries(
            Object.entries(manifest.personas).map(([key, persona]) => [
              key,
              {
                email: persona.email,
                username: persona.username,
                password: persona.password,
                userId: persona.userId,
              },
            ]),
          ),
        }
      : {}),
    checks: report.checks,
    validationArtifacts,
  });
}

async function writeCandidateValidationReport(
  manifest,
  scenarioReport,
  { completedAutomations = ["api"], results = [] } = {},
) {
  const { createValidationReport, writeValidationReport } =
    await import("./report.mjs");
  const cases = validationCasesForManifest(manifest);
  if (cases.length === 0) return null;
  return writeValidationReport(
    createValidationReport({
      manifest,
      scenarioReport,
      cases,
      results,
      completedAutomations,
    }),
  );
}

function isBrowserPassCandidate(candidate, selectedKeys) {
  const quizKey = candidate.quizKey ?? candidate.key;
  if (quizKey && selectedKeys.length > 0 && !selectedKeys.includes(quizKey)) {
    return false;
  }
  if (candidate.automation !== "browser") return false;
  return !candidate.browser || candidate.browser.status === "required";
}

function validationCasesForManifest(manifest) {
  if (manifest.definitionKey === "lifecycle-access-matrix") {
    return selectValidationCases({ suites: ["lifecycle-access"] });
  }
  const definition = getScenarioDefinition(manifest.definitionKey);
  return manifest.quizKeys.map((quizKey) => {
    const quiz = definition.quizzes.find(
      (candidate) => candidate.key === quizKey,
    );
    return quiz?.validationCaseId
      ? getValidationCase(quiz.validationCaseId)
      : getValidationCase(quizKey);
  });
}

function resolveQuizKeys(definition, quizKeys) {
  const hasCatalogFilter =
    selectedCaseIds.length > 0 || selectedQuestionTypes.length > 0;
  if (!hasCatalogFilter) return quizKeys;
  const catalogCases = selectValidationCases({
    caseIds: selectedCaseIds,
    questionTypes: selectedQuestionTypes,
  });
  const available = new Set(definition.quizzes.map((quiz) => quiz.key));
  const mapped = catalogCases
    .map((candidate) => candidate.quizKey ?? candidate.key)
    .filter((key) => key && available.has(key));
  if (mapped.length === 0) {
    throw new Error(
      `The selected validation cases are not executable quizzes in scenario "${definition.key}".`,
    );
  }
  if (quizKeys.length === 0) return [...new Set(mapped)];
  const mappedSet = new Set(mapped);
  const intersection = quizKeys.filter((key) => mappedSet.has(key));
  if (intersection.length === 0) {
    throw new Error("--quiz and validation case filters do not overlap.");
  }
  return intersection;
}

function output(value) {
  if (values.json) {
    console.log(JSON.stringify(value, null, 2));
    return;
  }
  console.log(JSON.stringify(value, null, 2));
}

function progress(message) {
  console.error(`[grading:scenario] ${message}`);
}

function withCliScenarioLock(scenarioKey, work) {
  return withScenarioLock(scenarioKey, work, {
    onStaleLock: (owner) =>
      progress(
        `Recovered stale lock from process ${owner.pid} (acquired ${owner.acquiredAt ?? "at an unknown time"}).`,
      ),
  });
}

function requireScenario(value) {
  if (!value) throw new Error("--scenario is required for this command.");
  getScenarioDefinition(value);
  return value;
}

function parseQuizKeys(values) {
  return [
    ...new Set(
      (values ?? [])
        .flatMap((value) => value.split(","))
        .map((value) => value.trim())
        .filter(Boolean),
    ),
  ];
}

function printHelp() {
  console.log(`Quiz Grading Scenario Runner

Usage:
  pnpm grading:scenario list
  pnpm grading:scenario catalog [--suite <suite>] [--case <id>]
  pnpm grading:scenario prepare --scenario <key> --checkpoint <checkpoint>
  pnpm grading:scenario status --scenario <key>
  pnpm grading:scenario verify --scenario <key>
  pnpm grading:scenario test --scenario <key> [--quiz <key>] [--headed] [--cleanup]
  pnpm grading:scenario reset --scenario <key>

Options:
  --fresh                 Reset the owned execution before preparing it
  --quiz <key>            Select one or more quizzes (repeat or comma-separate)
  --case <id>             Select by stable validation case ID or key
  --question-type <type>  Select by QuizEntryType value or question case key
  --suite <suite>         Filter the catalog by validation suite
  --api-base-url <url>    Defaults to http://localhost:8080
  --web-base-url <url>    Defaults to http://localhost:3000
  --json                  Machine-readable output
`);
}
