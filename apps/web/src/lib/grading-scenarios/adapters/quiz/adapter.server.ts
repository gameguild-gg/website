import "server-only";

import { readFile } from "node:fs/promises";
import type {
  GradingScenarioAdapter,
  GradingScenarioAdapterSummary,
  GradingScenarioCheck,
  GradingScenarioDefinitionSummary,
  GradingScenarioLink,
  GradingScenarioOperationResult,
  GradingScenarioPersonaSummary,
  GradingScenarioResetResult,
  GradingScenarioState,
  GradingScenarioTargetState,
  PrepareGradingScenarioInput,
} from "../../contracts";
import type {
  QuizDefinition,
  QuizManifest,
  QuizOperationResult,
  QuizResetReport,
  QuizScenarioReport,
  QuizTargetDefinition,
  QuizValidationCase,
} from "./contracts";
import {
  getValidationCase,
  selectValidationCases,
} from "./capability-catalog.mjs";
import {
  CHECKPOINTS,
  getScenarioDefinition,
  listScenarioDefinitions,
  selectQuizDefinitions,
} from "./definitions.mjs";
import { readManifest, reportPath } from "./manifest.server.mjs";
import {
  prepareQuizGradingScenario,
  resetQuizGradingScenario,
  verifyQuizGradingScenario,
} from "./operations.server.mjs";

const ADAPTER_KEY = "quiz";
const DEFAULT_SCENARIO_KEY = "review-matrix";

function definitionSummary(
  definition: QuizDefinition,
): GradingScenarioDefinitionSummary {
  return {
    key: definition.key,
    title: definition.title,
    subject: String(definition.subject),
    targets: definition.quizzes.map((quiz) => ({
      key: quiz.key,
      title: quiz.title,
      kind: "quiz",
      workflow: [...quiz.reviewMethods],
      capability: quiz.officialCapability ?? null,
      validationCaseId: (
        getValidationCase(
          quiz.validationCaseId ?? quiz.key,
        ) as QuizValidationCase
      ).id,
      maxCheckpoint: quiz.maxSupportedCheckpoint ?? "released",
    })),
  };
}

function adapterSummary(): GradingScenarioAdapterSummary {
  return {
    key: ADAPTER_KEY,
    title: "Quiz grading",
    targetLabel: "quiz",
    targetLabelPlural: "quizzes",
    defaultScenarioKey: DEFAULT_SCENARIO_KEY,
    checkpoints: CHECKPOINTS.filter((checkpoint) => checkpoint !== "empty"),
    scenarios: (listScenarioDefinitions() as QuizDefinition[]).map(
      definitionSummary,
    ),
    capabilityCatalog: (selectValidationCases() as QuizValidationCase[]).map(
      (candidate) => ({
        id: candidate.id,
        suite: candidate.suite,
        capability: candidate.capability,
        automation: candidate.automation,
        requirement: candidate.requirement,
        status: candidate.baseline.status,
        owner: candidate.baseline.owner,
        reason: candidate.baseline.reason ?? null,
        tracking: candidate.baseline.tracking ?? null,
        targetKey: candidate.quizKey ?? candidate.key ?? null,
      }),
    ),
  };
}

async function readPersistedReport(
  scenarioKey: string,
): Promise<QuizScenarioReport | null> {
  try {
    return JSON.parse(
      await readFile(reportPath(scenarioKey), "utf8"),
    ) as QuizScenarioReport;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") return null;
    if (error instanceof SyntaxError) return null;
    throw error;
  }
}

function personaLabel(key: string): string {
  switch (key) {
    case "learnerA":
      return "Learner A";
    case "learnerB":
      return "Learner B";
    default:
      return key.charAt(0).toUpperCase() + key.slice(1);
  }
}

function mapPersonas(manifest: QuizManifest): GradingScenarioPersonaSummary[] {
  return Object.entries(manifest.personas).map(([key, persona]) => ({
    key,
    label: personaLabel(key),
    email: persona.email,
    username: persona.username,
    password: persona.password,
    userId: persona.userId ?? null,
  }));
}

function addLink(
  links: GradingScenarioLink[],
  key: string,
  label: string,
  href: unknown,
  audience: string,
  targetKey: string | null = null,
) {
  if (typeof href !== "string" || href.length === 0) return;
  links.push({ key, label, href, audience, targetKey });
}

function mapLinks(manifest: QuizManifest): GradingScenarioLink[] {
  const links: GradingScenarioLink[] = [];
  const urls = manifest.urls as Record<string, unknown> & {
    quizzes?: Record<string, Record<string, unknown>>;
  };

  addLink(links, "course", "Course workspace", urls.course, "Instructor");
  addLink(
    links,
    "learner-course",
    "Learner course",
    urls.learnerCourse,
    "Learner A",
  );
  addLink(
    links,
    "learner-grades",
    "Learner grades",
    urls.learnerGrades,
    "Learner A",
  );
  addLink(
    links,
    "learner-reviews",
    "Learner reviews",
    urls.learnerReviews,
    "Learners",
  );

  for (const targetKey of manifest.quizKeys) {
    const targetUrls = urls.quizzes?.[targetKey] ?? {};
    addLink(
      links,
      `${targetKey}:content`,
      "Content editor",
      targetUrls.contentEditor,
      "Instructor",
      targetKey,
    );
    addLink(
      links,
      `${targetKey}:assessment`,
      "Assessment editor",
      targetUrls.assessmentEditor,
      "Instructor",
      targetKey,
    );
    addLink(
      links,
      `${targetKey}:test-run`,
      "Test run",
      targetUrls.testRun,
      "Instructor",
      targetKey,
    );
    addLink(
      links,
      `${targetKey}:activity`,
      "Learner activity",
      targetUrls.learnerActivity,
      "Learner A",
      targetKey,
    );
    addLink(
      links,
      `${targetKey}:speed-grader`,
      "SpeedGrader",
      targetUrls.speedGrader,
      "Instructor",
      targetKey,
    );
  }

  return links;
}

function mapChecks(report: QuizScenarioReport | null): GradingScenarioCheck[] {
  return (report?.checks ?? []).map((check) => ({
    name: String(check.name ?? "Unnamed check"),
    ok: check.ok === true,
    detail: check.detail ?? null,
  }));
}

function mapTargets(
  manifest: QuizManifest,
  report: QuizScenarioReport | null,
): GradingScenarioTargetState[] {
  const definition = getScenarioDefinition(
    manifest.definitionKey,
  ) as QuizDefinition;
  return (
    selectQuizDefinitions(
      definition,
      manifest.quizKeys,
    ) as QuizTargetDefinition[]
  ).map((quiz) => ({
    key: quiz.key,
    title: quiz.title,
    declaredCheckpoint: manifest.resources.quizzes[quiz.key].checkpoint,
    confirmedCheckpoint: report?.checkpoints?.[quiz.key]?.confirmed ?? null,
  }));
}

async function stateFromManifest(
  scenarioKey: string,
  manifest: QuizManifest | null,
  diagnostic: string | null = null,
): Promise<GradingScenarioState> {
  if (!manifest) {
    return {
      adapterKey: ADAPTER_KEY,
      scenarioKey,
      health: "not-prepared",
      prepared: false,
      runId: null,
      marker: null,
      declaredCheckpoint: null,
      confirmedCheckpoint: null,
      nextCheckpoint: null,
      updatedAt: null,
      diagnostic,
      targets: [],
      personas: [],
      links: [],
      checks: [],
    };
  }

  const report = await readPersistedReport(scenarioKey);
  const healthy = report?.healthy;
  return {
    adapterKey: ADAPTER_KEY,
    scenarioKey,
    health: diagnostic || healthy === false ? "unhealthy" : "ready",
    prepared: true,
    runId: manifest.runId,
    marker: manifest.marker,
    declaredCheckpoint: manifest.checkpoint,
    confirmedCheckpoint: report?.confirmedCheckpoint ?? null,
    nextCheckpoint: report?.nextCheckpoint ?? null,
    updatedAt: manifest.updatedAt ?? null,
    diagnostic,
    targets: mapTargets(manifest, report),
    personas: mapPersonas(manifest),
    links: mapLinks(manifest),
    checks: mapChecks(report),
  };
}

async function inspect(scenarioKey: string): Promise<GradingScenarioState> {
  getScenarioDefinition(scenarioKey);
  try {
    const manifest = (await readManifest(scenarioKey, {
      optional: true,
    })) as QuizManifest | null;
    return stateFromManifest(scenarioKey, manifest);
  } catch (error) {
    return stateFromManifest(
      scenarioKey,
      null,
      error instanceof Error
        ? error.message
        : "The quiz scenario state could not be read.",
    );
  }
}

async function prepare(
  input: PrepareGradingScenarioInput,
): Promise<GradingScenarioOperationResult> {
  const progress: string[] = [];
  const operation = (await prepareQuizGradingScenario({
    scenarioKey: input.scenarioKey,
    checkpoint: input.checkpoint,
    quizKeys: input.targetKeys,
    fresh: input.fresh,
    apiBaseUrl: input.apiBaseUrl,
    webBaseUrl: input.webBaseUrl,
    onProgress: (message: string) => {
      progress.push(message);
      input.onProgress?.(message);
    },
    onStaleLock: (owner: { pid: number }) => {
      progress.push(`Recovered stale scenario lock from process ${owner.pid}.`);
    },
  })) as QuizOperationResult;
  return {
    state: await stateFromManifest(input.scenarioKey, operation.manifest),
    progress,
  };
}

async function verify(
  scenarioKey: string,
): Promise<GradingScenarioOperationResult> {
  const progress = [
    `Verifying "${scenarioKey}" against the persisted API state.`,
  ];
  const operation = (await verifyQuizGradingScenario({
    scenarioKey,
    onStaleLock: (owner: { pid: number }) => {
      progress.push(`Recovered stale scenario lock from process ${owner.pid}.`);
    },
  })) as QuizOperationResult;
  return {
    state: await stateFromManifest(scenarioKey, operation.manifest),
    progress,
  };
}

async function reset(scenarioKey: string): Promise<GradingScenarioResetResult> {
  const report = (await resetQuizGradingScenario({
    scenarioKey,
  })) as QuizResetReport;
  return {
    scenario: report.scenario,
    removed: report.removed ?? [],
    skipped: report.skipped ?? [],
    failures: report.failures ?? [],
  };
}

export const quizGradingScenarioAdapter: GradingScenarioAdapter = {
  summary: adapterSummary,
  inspect,
  prepare,
  verify,
  reset,
};
