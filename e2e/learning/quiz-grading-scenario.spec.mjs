import { chmod, mkdir } from "node:fs/promises";
import { expect, test } from "playwright/test";
import {
  assertSharedAuthCookie,
  trackAppHttpFailures,
} from "../../apps/web/scripts/learning-browser-e2e-support.mjs";
import {
  authenticatePersona,
  request,
} from "../../apps/web/scripts/quiz-grading-scenario/api.mjs";
import {
  checkpointIndex,
  getScenarioDefinition,
  hasInstructorReview,
} from "../../apps/web/scripts/quiz-grading-scenario/definitions.mjs";
import {
  evidenceDirectory,
  readManifest,
  storageStatePath,
  writeManifest,
} from "../../apps/web/scripts/quiz-grading-scenario/manifest.mjs";
import { buildScenarioUrls } from "../../apps/web/scripts/quiz-grading-scenario/urls.mjs";

test.use({ trace: "retain-on-failure", screenshot: "only-on-failure" });
test.skip(!process.env.E2E_RUN, "Set E2E_RUN=1 to execute the local scenario.");

test("quiz grading scenario follows the author, learner, review, and release journey", async ({
  browser,
}) => {
  test.setTimeout(240_000);
  const scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
  if (!scenarioKey) throw new Error("QUIZ_GRADING_SCENARIO is required.");
  const definition = getScenarioDefinition(scenarioKey);
  const manifest = await readManifest(scenarioKey);
  if (definition.subject === "collective") {
    test.skip(
      true,
      "The collective scenario is covered through API verification in the MVP.",
    );
  }
  if (checkpointIndex(manifest.checkpoint) < checkpointIndex("learner-ready")) {
    throw new Error(
      "Browser execution requires a scenario prepared through learner-ready.",
    );
  }

  await mkdir(evidenceDirectory(scenarioKey), { recursive: true, mode: 0o700 });
  const instructor = await createPersonaPage(browser, manifest, "instructor");
  const learner = await createPersonaPage(browser, manifest, "learnerA");
  const outsider = await createPersonaPage(browser, manifest, "outsider");
  try {
    await verifyAuthoringAndTestRun(instructor.page, manifest, definition);
    await verifyOutsiderGate(outsider.page, manifest);
    await verifyLearnerCourseDiscovery(learner.page, manifest);

    if (manifest.checkpoint === "learner-ready") {
      await submitAsLearner(learner.page, manifest);
      await synchronizeSubmission(manifest);
      manifest.checkpoint = hasInstructorReview(definition)
        ? "review-ready"
        : "release-ready";
      manifest.urls = buildScenarioUrls(manifest);
      await writeManifest(manifest);
    }

    if (
      hasInstructorReview(definition) &&
      manifest.checkpoint === "review-ready"
    ) {
      await reviewAsInstructor(instructor.page, manifest, definition);
      manifest.checkpoint = "release-ready";
      await synchronizeSubmission(manifest);
      manifest.urls = buildScenarioUrls(manifest);
      await writeManifest(manifest);
    }

    if (manifest.checkpoint === "release-ready") {
      await releaseAsInstructor(instructor.page, manifest);
      manifest.checkpoint = "released";
      await synchronizeSubmission(manifest);
      manifest.urls = buildScenarioUrls(manifest);
      await writeManifest(manifest);
    }

    await verifyLearnerResult(learner.page, manifest, definition);
    instructor.failures.assertNone("instructor scenario");
    learner.failures.assertNone("learner scenario");
    outsider.failures.assertNone("outsider scenario");
  } finally {
    await Promise.allSettled([
      instructor.context.close(),
      learner.context.close(),
      outsider.context.close(),
    ]);
  }
});

async function verifyLearnerCourseDiscovery(page, manifest) {
  await page.goto(manifest.urls.learnerCourses, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByRole("heading", { name: "My courses" })).toBeVisible({
    timeout: 60_000,
  });
  await expect(page.getByText(manifest.marker).first()).toBeVisible();
}

async function createPersonaPage(browser, manifest, personaKey) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const failures = trackAppHttpFailures(page, [
    manifest.environment.webBaseUrl,
    manifest.environment.apiBaseUrl,
  ]);
  const persona = manifest.personas[personaKey];
  await browserSignIn(
    page,
    manifest.environment.webBaseUrl,
    persona.email,
    persona.password,
  );
  const statePath = storageStatePath(manifest.definitionKey, personaKey);
  await context.storageState({ path: statePath });
  await chmod(statePath, 0o600).catch(() => {});
  return { context, page, failures };
}

async function browserSignIn(page, webBaseUrl, email, password) {
  await page.goto(`${webBaseUrl}/sign-in`, { waitUntil: "domcontentloaded" });
  const result = await page.evaluate(
    async ({ emailValue, passwordValue }) => {
      const csrfResponse = await fetch("/api/auth/csrf", {
        credentials: "include",
      });
      const csrf = await csrfResponse.json().catch(() => null);
      if (!csrfResponse.ok || typeof csrf?.csrfToken !== "string") {
        return { ok: false, stage: "csrf", status: csrfResponse.status };
      }
      const response = await fetch("/api/auth/signin/credentials", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email: emailValue,
          password: passwordValue,
          csrfToken: csrf.csrfToken,
          redirect: false,
          redirectTo: "/",
        }),
      });
      return { ok: response.ok, stage: "credentials", status: response.status };
    },
    { emailValue: email, passwordValue: password },
  );
  if (!result.ok) {
    throw new Error(
      `Browser sign-in failed at ${result.stage} (HTTP ${result.status}).`,
    );
  }
  assertSharedAuthCookie(await page.context().cookies([webBaseUrl]));
}

async function verifyAuthoringAndTestRun(page, manifest, definition) {
  const api = await authenticatePersona(manifest, "instructor", {
    create: false,
  });
  const before = await readGradingQueue(api, manifest.resources.assessmentId);
  await page.goto(manifest.urls.assessmentEditor, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page
      .getByRole("heading", { name: new RegExp(definition.quiz.title, "i") })
      .first(),
  ).toBeVisible({ timeout: 60_000 });
  await page.getByRole("button", { name: "Test assessment" }).click();
  await expect(
    page.getByRole("heading", { name: "Assessment test run" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Start test run" }).click();
  await page.getByRole("button", { name: "True", exact: true }).click();
  await page.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Run grading workflow" }).click();
  if (hasInstructorReview(definition)) {
    const score = page.locator("#test-score-q1");
    await score.fill(String(definition.expected.instructorScore / 100));
    await page
      .getByRole("button", { name: "Complete instructor review" })
      .click();
  }
  await expect(page.getByText("Diagnostic result")).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(page, manifest, "01-test-run.png");

  const after = await readGradingQueue(api, manifest.resources.assessmentId);
  expect(after.total ?? 0).toBe(before.total ?? 0);
}

async function verifyOutsiderGate(page, manifest) {
  await page.goto(manifest.urls.learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByRole("heading", { name: "Join this course" }),
  ).toBeVisible({
    timeout: 60_000,
  });
  await expect(
    page.getByRole("button", { name: "Start Activity" }),
  ).toHaveCount(0);
  await screenshot(page, manifest, "02-outsider-denied.png");
}

async function submitAsLearner(page, manifest) {
  await page.goto(manifest.urls.learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await page.getByRole("button", { name: "Start Activity" }).click();
  await page.getByRole("button", { name: "True", exact: true }).click();
  await page.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Submit Quiz" }).click();
  await expect(
    page
      .getByText(/Submitted|result has not been released|instructor review/i)
      .first(),
  ).toBeVisible({ timeout: 60_000 });
  await screenshot(page, manifest, "03-learner-submitted.png");
}

async function reviewAsInstructor(page, manifest, definition) {
  await page.goto(manifest.urls.speedGrader, { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("grading-panel")).toBeVisible({
    timeout: 60_000,
  });
  await page
    .getByTestId("item-score-q1")
    .fill(String(definition.expected.instructorScore / 100));
  await page.getByTestId("resolve-instructor-review").click();
  await expect(page.getByText(/Final result:/)).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(page, manifest, "04-instructor-reviewed.png");
}

async function releaseAsInstructor(page, manifest) {
  await page.goto(manifest.urls.speedGrader, { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("release-result")).toBeVisible({
    timeout: 60_000,
  });
  await page.getByTestId("release-result").click();
  await expect(page.getByText("Released", { exact: true })).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(page, manifest, "05-result-released.png");
}

async function verifyLearnerResult(page, manifest, definition) {
  await page.goto(manifest.urls.learnerGrades, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText(new RegExp(definition.quiz.title, "i")).first(),
  ).toBeVisible({ timeout: 60_000 });
  await screenshot(page, manifest, "06-learner-gradebook.png");

  await page.goto(manifest.urls.learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText(definition.quiz.document.blocks.q1.stem),
  ).toBeVisible({ timeout: 60_000 });
  await expect(page.getByRole("button", { name: "Start Activity" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Submit Quiz" })).toBeDisabled();
  const expectedScore = hasInstructorReview(definition)
    ? definition.expected.instructorScore
    : definition.expected.automatedScore;
  await expect(
    page.getByText(
      expectedScore === definition.expected.maxScore
        ? "Excellent! That's correct!"
        : "Not quite right. Try again!",
    ),
  ).toBeVisible();
  await screenshot(page, manifest, "07-learner-restored-result.png");
}

async function synchronizeSubmission(manifest) {
  const instructor = await authenticatePersona(manifest, "instructor", {
    create: false,
  });
  const deadline = Date.now() + 30_000;
  let submissionId = manifest.resources.submissionId;
  while (!submissionId && Date.now() < deadline) {
    const queue = await readGradingQueue(
      instructor,
      manifest.resources.assessmentId,
    );
    submissionId = queue.items?.find(
      (item) =>
        item.userId === manifest.personas.learnerA.userId || item.isGroup,
    )?.submissionId;
    if (!submissionId) await new Promise((resolve) => setTimeout(resolve, 500));
  }
  if (!submissionId)
    throw new Error(
      "The submitted learner attempt did not reach the grading queue.",
    );
  const submission = await request(
    instructor.client,
    "Synchronize runtime submission",
    {
      method: "GET",
      path: `/v1/assessments/runtime-submissions/${submissionId}`,
    },
  );
  manifest.resources.submissionId = submission.submissionId;
  manifest.resources.gradingExecutionId =
    submission.execution?.executionId ?? null;
  manifest.resources.gradeResultId =
    submission.execution?.activeRoundId ?? null;
}

async function screenshot(page, manifest, name) {
  await page.screenshot({
    path: `${evidenceDirectory(manifest.definitionKey)}/${name}`,
    fullPage: true,
  });
}

function readGradingQueue(session, assessmentId) {
  return request(session.client, "Read grading queue", {
    method: "GET",
    path: `/v1/assessments/${assessmentId}/grading-queue`,
  });
}
