import { chmod, mkdir } from "node:fs/promises";
import { expect } from "playwright/test";
import {
  assertSharedAuthCookie,
  trackAppHttpFailures,
} from "../../../apps/web/scripts/learning-browser-e2e-support.mjs";
import {
  authenticatePersona,
  request,
} from "../../../apps/web/scripts/quiz-grading-scenario/api.mjs";
import { checkpointIndex } from "../../../apps/web/scripts/quiz-grading-scenario/definitions.mjs";
import {
  recomputeManifestCheckpoint,
  storageStatePath,
  writeManifest,
} from "../../../apps/web/scripts/quiz-grading-scenario/manifest.mjs";
import { validationEvidenceDirectory } from "../../../apps/web/scripts/quiz-grading-scenario/report.mjs";
import { buildScenarioUrls } from "../../../apps/web/scripts/quiz-grading-scenario/urls.mjs";

export async function createScenarioPersona(browser, manifest, personaKey) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const failures = trackAppHttpFailures(page, [
    manifest.environment.webBaseUrl,
    manifest.environment.apiBaseUrl,
  ]);
  await signInScenarioPersona(page, manifest, personaKey);
  const statePath = storageStatePath(manifest.definitionKey, personaKey);
  await context.storageState({ path: statePath });
  await chmod(statePath, 0o600).catch(() => {});
  return { context, page, failures };
}

export async function signInScenarioPersona(page, manifest, personaKey) {
  const persona = manifest.personas[personaKey];
  if (!persona) throw new Error(`Scenario persona "${personaKey}" is missing.`);
  const http = page.context().request;
  const csrfResponse = await http.get(
    `${manifest.environment.webBaseUrl}/api/auth/csrf`,
  );
  const csrf = await csrfResponse.json().catch(() => null);
  const response =
    csrfResponse.ok() && typeof csrf?.csrfToken === "string"
      ? await http.post(
          `${manifest.environment.webBaseUrl}/api/auth/signin/credentials`,
          {
            headers: { "Content-Type": "application/json" },
            data: {
              email: persona.email,
              password: persona.password,
              csrfToken: csrf.csrfToken,
              redirect: false,
              redirectTo: "/",
            },
          },
        )
      : null;
  if (!response?.ok()) {
    throw new Error(
      `Browser sign-in failed for ${personaKey} (HTTP ${response?.status() ?? csrfResponse.status()}).`,
    );
  }
  assertSharedAuthCookie(
    await page.context().cookies([manifest.environment.webBaseUrl]),
  );
}

export async function synchronizeScenarioSubmission(
  manifest,
  quiz,
  personaKey = "learnerA",
) {
  const resources = manifest.resources.quizzes[quiz.key];
  const instructor = await authenticatePersona(manifest, "instructor", {
    create: false,
  });
  const deadline = Date.now() + 30_000;
  let submissionId = resources.submissions[personaKey]?.submissionId;
  while (!submissionId) {
    const queue = await request(instructor.client, "Read grading queue", {
      method: "GET",
      path: `/v1/assessments/${resources.assessmentId}/grading-queue`,
    });
    submissionId = queue.items?.find(
      (item) => item.userId === manifest.personas[personaKey].userId,
    )?.submissionId;
    if (submissionId) break;
    if (Date.now() >= deadline) {
      throw new Error(
        `${quiz.validationCaseId ?? quiz.key}: ${personaKey} did not reach the grading queue.`,
      );
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  const submission = await request(
    instructor.client,
    "Synchronize runtime submission",
    {
      method: "GET",
      path: `/v1/assessments/runtime-submissions/${submissionId}`,
    },
  );
  resources.submissions[personaKey] = {
    ...resources.submissions[personaKey],
    submissionId: submission.submissionId,
    gradingExecutionId: submission.execution?.executionId ?? null,
    gradeResultId: submission.execution?.activeRoundId ?? null,
  };
  manifest.urls = buildScenarioUrls(manifest);
  return submission;
}

export function advanceScenarioQuiz(manifest, quizKey, checkpoint) {
  const resources = manifest.resources.quizzes[quizKey];
  if (checkpointIndex(checkpoint) > checkpointIndex(resources.checkpoint)) {
    resources.checkpoint = checkpoint;
  }
  recomputeManifestCheckpoint(manifest);
}

export async function persistScenarioManifest(manifest) {
  manifest.urls = buildScenarioUrls(manifest);
  await writeManifest(manifest);
}

export async function captureValidationEvidence(page, manifest, caseId, name) {
  const directory = `${validationEvidenceDirectory(manifest.runId, caseId)}/screenshots`;
  await mkdir(directory, { recursive: true, mode: 0o700 });
  const path = `${directory}/${name}`;
  await page.screenshot({ path, fullPage: true });
  return path;
}

export async function expectQuestionVisible(page, validationCase) {
  const { entry } = validationCase.fixture;
  const prompt = entry.stem
    .split(/___|\b_[^_]+_\b/)
    .map((value) => value.trim())
    .find((value) => value.length > 0);
  await expect(
    page.getByText(prompt ?? entry.stem, { exact: false }).first(),
  ).toBeVisible({ timeout: 60_000 });
}
