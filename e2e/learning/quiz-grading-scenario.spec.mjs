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
  expectedFinalScore,
  getScenarioDefinition,
  hasInstructorReview,
  hasPeerReview,
  hasSelfReview,
  selectQuizDefinitions,
} from "../../apps/web/scripts/quiz-grading-scenario/definitions.mjs";
import {
  evidenceDirectory,
  readManifest,
  recomputeManifestCheckpoint,
  storageStatePath,
  writeManifest,
} from "../../apps/web/scripts/quiz-grading-scenario/manifest.mjs";
import { buildScenarioUrls } from "../../apps/web/scripts/quiz-grading-scenario/urls.mjs";

test.use({ trace: "retain-on-failure", screenshot: "only-on-failure" });
test.skip(!process.env.E2E_RUN, "Set E2E_RUN=1 to execute the local scenario.");

const scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
const requestedQuizKeys = (process.env.QUIZ_GRADING_QUIZZES ?? "")
  .split(",")
  .map((value) => value.trim())
  .filter(Boolean);

if (!scenarioKey) {
  test("quiz grading scenario requires a scenario key", () => {
    throw new Error("QUIZ_GRADING_SCENARIO is required.");
  });
} else {
  const definition = getScenarioDefinition(scenarioKey);
  const quizzes = selectQuizDefinitions(definition, requestedQuizKeys);

  for (const quiz of quizzes) {
    test(`${quiz.key}: ${quiz.reviewMethods.join(" -> ")} across its personas`, async ({
      browser,
    }) => {
      test.setTimeout(360_000);
      let manifest = await readManifest(scenarioKey);
      if (!manifest.quizKeys.includes(quiz.key)) {
        throw new Error(
          `Quiz "${quiz.key}" was not provisioned in scenario "${scenarioKey}". Reset and prepare it with that quiz selected.`,
        );
      }
      const resources = manifest.resources.quizzes[quiz.key];
      if (
        checkpointIndex(resources.checkpoint) < checkpointIndex("learner-ready")
      ) {
        throw new Error(
          `Browser execution for "${quiz.key}" requires learner-ready; current checkpoint is ${resources.checkpoint}.`,
        );
      }
      if (quiz.subject === "collective") {
        test.skip(
          true,
          "The collective scenario remains covered through API verification.",
        );
      }

      await mkdir(evidenceDirectory(scenarioKey), {
        recursive: true,
        mode: 0o700,
      });
      const instructor = await createPersonaPage(
        browser,
        manifest,
        "instructor",
      );
      const learnerA = await createPersonaPage(browser, manifest, "learnerA");
      const learnerB = hasPeerReview(quiz)
        ? await createPersonaPage(browser, manifest, "learnerB")
        : null;
      const outsider = await createPersonaPage(browser, manifest, "outsider");
      try {
        await verifyAuthoringAndTestRun(instructor.page, manifest, quiz);
        await verifyOutsiderGate(outsider.page, manifest, quiz);
        await verifyLearnerCourseDiscovery(learnerA.page, manifest);

        if (resources.checkpoint === "learner-ready") {
          await ensureLearnerSubmission(
            learnerA.page,
            manifest,
            quiz,
            "learnerA",
          );
          if (learnerB) {
            await ensureLearnerSubmission(
              learnerB.page,
              manifest,
              quiz,
              "learnerB",
            );
            await reviewAsPeer(learnerA.page, manifest, quiz, "learnerA");
            await reviewAsPeer(learnerB.page, manifest, quiz, "learnerB");
            await synchronizeSubmission(manifest, quiz, "learnerA");
            await synchronizeSubmission(manifest, quiz, "learnerB");
          }
          advanceQuizCheckpoint(
            manifest,
            quiz.key,
            hasInstructorReview(quiz) ? "review-ready" : "release-ready",
          );
          await persist(manifest);
        }

        if (
          hasInstructorReview(quiz) &&
          resources.checkpoint === "review-ready"
        ) {
          for (const personaKey of Object.keys(resources.submissions)) {
            await reviewAsInstructor(
              instructor.page,
              manifest,
              quiz,
              personaKey,
            );
            await synchronizeSubmission(manifest, quiz, personaKey);
          }
          advanceQuizCheckpoint(manifest, quiz.key, "release-ready");
          await persist(manifest);
        }

        if (resources.checkpoint === "release-ready") {
          for (const personaKey of Object.keys(resources.submissions)) {
            await releaseAsInstructor(
              instructor.page,
              manifest,
              quiz,
              personaKey,
            );
            await synchronizeSubmission(manifest, quiz, personaKey);
          }
          advanceQuizCheckpoint(manifest, quiz.key, "released");
          await persist(manifest);
        }

        await verifyLearnerResult(learnerA.page, manifest, quiz);
        if (learnerB) {
          await verifyLearnerResult(learnerB.page, manifest, quiz);
        }
        instructor.failures.assertNone(`${quiz.key} instructor scenario`);
        learnerA.failures.assertNone(`${quiz.key} learner A scenario`);
        learnerB?.failures.assertNone(`${quiz.key} learner B scenario`);
        outsider.failures.assertNone(`${quiz.key} outsider scenario`);
      } finally {
        await Promise.allSettled(
          [instructor, learnerA, learnerB, outsider]
            .filter(Boolean)
            .map((persona) => persona.context.close()),
        );
      }
    });
  }
}

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
  const request = page.context().request;
  const csrfResponse = await request.get(`${webBaseUrl}/api/auth/csrf`);
  const csrf = await csrfResponse.json().catch(() => null);
  const result =
    csrfResponse.ok() && typeof csrf?.csrfToken === "string"
      ? await request
          .post(`${webBaseUrl}/api/auth/signin/credentials`, {
            headers: { "Content-Type": "application/json" },
            data: {
              email,
              password,
              csrfToken: csrf.csrfToken,
              redirect: false,
              redirectTo: "/",
            },
          })
          .then((response) => ({
            ok: response.ok(),
            stage: "credentials",
            status: response.status(),
          }))
      : { ok: false, stage: "csrf", status: csrfResponse.status() };
  if (!result.ok) {
    throw new Error(
      `Browser sign-in failed at ${result.stage} (HTTP ${result.status}).`,
    );
  }
  assertSharedAuthCookie(await page.context().cookies([webBaseUrl]));
}

async function verifyAuthoringAndTestRun(page, manifest, quiz) {
  const resources = manifest.resources.quizzes[quiz.key];
  const urls = manifest.urls.quizzes[quiz.key];
  const api = await authenticatePersona(manifest, "instructor", {
    create: false,
  });
  const before = await readGradingQueue(api, resources.assessmentId);
  await refreshPersonaPage(page, manifest, "instructor");
  await page.goto(urls.assessmentEditor, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByRole("heading", { name: new RegExp(quiz.title, "i") }).first(),
  ).toBeVisible({ timeout: 60_000 });
  await page.getByRole("button", { name: "Test assessment" }).click();
  await expect(
    page.getByRole("heading", { name: "Assessment test run" }),
  ).toBeVisible({ timeout: 60_000 });
  await page.getByRole("button", { name: "Start test run" }).click();

  if (hasPeerReview(quiz)) {
    const tabs = page.getByRole("tab");
    await expect(tabs).toHaveCount(2);
    for (let index = 0; index < 2; index += 1) {
      await selectSyntheticSubject(tabs, index);
      await answerTestRun(page, quiz);
    }
    for (let index = 1; index >= 0; index -= 1) {
      await selectSyntheticSubject(tabs, index);
      await page
        .getByRole("button", { name: "Claim anonymous response" })
        .click();
      await page
        .locator('input[id^="peer-score-"]')
        .fill(String(quiz.expected.peerReviewScore / 100));
      await page
        .locator('textarea[id^="peer-feedback-"]')
        .fill(`Synthetic peer feedback ${index + 1}.`);
      await page.getByRole("button", { name: "Submit peer review" }).click();
      await expect(
        page.getByRole("button", { name: "Submit peer review" }),
      ).toHaveCount(0, { timeout: 30_000 });
    }
    if (hasInstructorReview(quiz)) {
      for (let index = 0; index < 2; index += 1) {
        await selectSyntheticSubject(tabs, index);
        await completeTestInstructorReview(page, quiz);
      }
    }
  } else {
    await answerTestRun(page, quiz);
    if (hasSelfReview(quiz)) {
      await completeSelfReview(page, quiz);
    }
    if (hasInstructorReview(quiz)) {
      await completeTestInstructorReview(page, quiz);
    }
  }

  await expect(page.getByText("Diagnostic result").first()).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(page, manifest, quiz.key, "01-test-run.png");
  const after = await readGradingQueue(api, resources.assessmentId);
  expect(after.total ?? 0).toBe(before.total ?? 0);
}

async function selectSyntheticSubject(tabs, index) {
  const tab = tabs.nth(index);
  await tab.click();
  await expect(tab).toHaveAttribute("aria-selected", "true");
}

async function answerTestRun(page, quiz) {
  const first = page
    .getByText(quiz.document.blocks.q1.stem, { exact: true })
    .locator("..");
  await first.getByRole("button", { name: "True", exact: true }).click();
  await first.getByRole("button", { name: "Submit answer" }).click();
  const second = page
    .getByText(quiz.document.blocks.q2.stem, { exact: true })
    .locator("..");
  await second.getByRole("button", { name: "Four", exact: true }).click();
  await second.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Run grading workflow" }).click();
  await expect(
    page.getByRole("button", { name: "Run grading workflow" }),
  ).toHaveCount(0, { timeout: 30_000 });
}

async function completeSelfReview(page, quiz) {
  await page.locator("#self-score-q1").fill("1");
  await page
    .locator("#self-score-q2")
    .fill(String((quiz.expected.selfReviewScore - 100) / 100));
  await page
    .getByLabel("Overall self-review feedback")
    .fill("Scenario self review.");
  await page.getByRole("button", { name: "Submit self review" }).click();
  await expect(page.getByText("Self review submitted.")).toBeVisible({
    timeout: 30_000,
  });
}

async function completeTestInstructorReview(page, quiz) {
  await page.locator("#test-score-q1").fill("1");
  await page
    .locator("#test-score-q2")
    .fill(String((quiz.expected.instructorScore - 100) / 100));
  await page
    .getByRole("button", { name: "Complete instructor review" })
    .click();
  await expect(
    page.getByRole("button", { name: "Complete instructor review" }),
  ).toHaveCount(0, { timeout: 30_000 });
}

async function verifyOutsiderGate(page, manifest, quiz) {
  await page.goto(manifest.urls.quizzes[quiz.key].learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByRole("heading", { name: "Join this course" }),
  ).toBeVisible({ timeout: 60_000 });
  await expect(
    page.getByRole("button", { name: "Start Activity" }),
  ).toHaveCount(0);
  await screenshot(page, manifest, quiz.key, "02-outsider-denied.png");
}

async function submitAsLearner(page, manifest, quiz, personaKey) {
  await page.goto(manifest.urls.quizzes[quiz.key].learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await page.getByRole("button", { name: "Start Activity" }).click();
  await page.getByRole("button", { name: "True", exact: true }).click();
  await page.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("button", { name: "Four", exact: true }).click();
  await page.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Submit Quiz" }).click();
  if (hasSelfReview(quiz)) {
    await completeSelfReview(page, quiz);
  }
  await expect(
    page
      .getByText(
        /Submitted|result has not been released|instructor review|Self review submitted/i,
      )
      .first(),
  ).toBeVisible({ timeout: 60_000 });
  await screenshot(page, manifest, quiz.key, `03-${personaKey}-submitted.png`);
}

async function ensureLearnerSubmission(page, manifest, quiz, personaKey) {
  const alreadySubmitted = await synchronizeSubmission(
    manifest,
    quiz,
    personaKey,
    { wait: false },
  );
  if (!alreadySubmitted) {
    await submitAsLearner(page, manifest, quiz, personaKey);
    await synchronizeSubmission(manifest, quiz, personaKey);
  }
  await persist(manifest);
}

async function reviewAsPeer(page, manifest, quiz, personaKey) {
  const resources = manifest.resources.quizzes[quiz.key];
  const assessmentId = resources.assessmentId;
  const claimedReviewId = resources.peerReviewIds[personaKey];
  if (claimedReviewId) {
    await page.goto(`${manifest.urls.learnerReviews}/${claimedReviewId}`, {
      waitUntil: "domcontentloaded",
    });
  } else {
    await page.goto(manifest.urls.learnerReviews, {
      waitUntil: "domcontentloaded",
    });
    const task = page.getByTestId(`review-task-${assessmentId}`);
    await expect(task).toBeVisible({ timeout: 60_000 });
    await Promise.all([
      page.waitForURL(
        (url) => /\/learn\/reviews\/[0-9a-f-]+$/i.test(url.pathname),
        { timeout: 60_000 },
      ),
      task.getByRole("button", { name: "Review a peer" }).click(),
    ]);
    resources.peerReviewIds[personaKey] = new URL(page.url()).pathname
      .split("/")
      .at(-1);
    await persist(manifest);
  }
  await expect(page.getByTestId("anonymous-header")).toBeVisible({
    timeout: 60_000,
  });
  if (
    await page
      .getByText("Review already submitted.", { exact: true })
      .isVisible()
  ) {
    return;
  }
  await page
    .getByTestId("peer-score-input")
    .fill(String(quiz.expected.peerReviewScore / 100));
  await page
    .getByTestId("peer-feedback")
    .fill(`Peer feedback from ${personaKey}.`);
  await page.getByTestId("submit-review").click();
  await expect(page.getByRole("heading", { name: "Peer reviews" })).toBeVisible(
    {
      timeout: 60_000,
    },
  );
  await persist(manifest);
  await screenshot(
    page,
    manifest,
    quiz.key,
    `04-${personaKey}-peer-review.png`,
  );
}

async function reviewAsInstructor(page, manifest, quiz, personaKey) {
  const urls = manifest.urls.quizzes[quiz.key];
  await refreshPersonaPage(page, manifest, "instructor");
  await page.goto(urls.speedGraders[personaKey], {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("grading-panel")).toBeVisible({
    timeout: 60_000,
  });
  await page.getByTestId("item-score-q1").fill("1");
  await page
    .getByTestId("item-score-q2")
    .fill(String((quiz.expected.instructorScore - 100) / 100));
  await page.getByTestId("resolve-instructor-review").click();
  await expect(page.getByText("Ready to release")).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(
    page,
    manifest,
    quiz.key,
    `05-${personaKey}-instructor-reviewed.png`,
  );
}

async function releaseAsInstructor(page, manifest, quiz, personaKey) {
  const urls = manifest.urls.quizzes[quiz.key];
  await refreshPersonaPage(page, manifest, "instructor");
  await page.goto(urls.speedGraders[personaKey], {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("release-result")).toBeVisible({
    timeout: 60_000,
  });
  await page.getByTestId("release-result").click();
  await expect(page.getByText("Released to learner")).toBeVisible({
    timeout: 30_000,
  });
  await screenshot(page, manifest, quiz.key, `06-${personaKey}-released.png`);
}

async function verifyLearnerResult(page, manifest, quiz) {
  await page.goto(manifest.urls.learnerGrades, {
    waitUntil: "domcontentloaded",
  });
  const gradeHeading = page.getByRole("heading", {
    name: `[${manifest.marker}] ${quiz.title}`,
    exact: true,
  });
  await expect(gradeHeading).toBeVisible({ timeout: 60_000 });
  const gradeRow = gradeHeading.locator("xpath=ancestor::article");
  await expect(
    gradeRow.getByText(
      `${expectedFinalScore(quiz) / 100} / ${quiz.expected.maxScore / 100}`,
      { exact: true },
    ),
  ).toBeVisible();
  await screenshot(page, manifest, quiz.key, "07-learner-gradebook.png");

  await page.goto(manifest.urls.quizzes[quiz.key].learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText(quiz.document.blocks.q1.stem, { exact: true }).first(),
  ).toBeVisible({ timeout: 60_000 });
  await expect(
    page.getByRole("button", { name: "Start Activity" }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Submit Quiz" }),
  ).toBeDisabled();
  await screenshot(page, manifest, quiz.key, "08-learner-restored-result.png");
}

async function synchronizeSubmission(
  manifest,
  quiz,
  personaKey,
  { wait = true } = {},
) {
  const resources = manifest.resources.quizzes[quiz.key];
  const instructor = await authenticatePersona(manifest, "instructor", {
    create: false,
  });
  const deadline = Date.now() + 30_000;
  let submissionId = resources.submissions[personaKey].submissionId;
  while (!submissionId) {
    const queue = await readGradingQueue(instructor, resources.assessmentId);
    submissionId = queue.items?.find(
      (item) =>
        item.userId === manifest.personas[personaKey].userId || item.isGroup,
    )?.submissionId;
    if (submissionId) break;
    if (!wait) return false;
    if (Date.now() >= deadline) {
      throw new Error(
        `The ${personaKey} attempt for "${quiz.key}" did not reach the grading queue.`,
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
  const target = resources.submissions[personaKey];
  target.submissionId = submission.submissionId;
  target.gradingExecutionId = submission.execution?.executionId ?? null;
  target.gradeResultId = submission.execution?.activeRoundId ?? null;
  manifest.urls = buildScenarioUrls(manifest);
  return true;
}

async function refreshPersonaPage(page, manifest, personaKey) {
  const persona = manifest.personas[personaKey];
  await browserSignIn(
    page,
    manifest.environment.webBaseUrl,
    persona.email,
    persona.password,
  );
}

function advanceQuizCheckpoint(manifest, quizKey, checkpoint) {
  const resources = manifest.resources.quizzes[quizKey];
  if (checkpointIndex(checkpoint) > checkpointIndex(resources.checkpoint)) {
    resources.checkpoint = checkpoint;
  }
  recomputeManifestCheckpoint(manifest);
}

async function persist(manifest) {
  manifest.urls = buildScenarioUrls(manifest);
  await writeManifest(manifest);
}

async function screenshot(page, manifest, quizKey, name) {
  const directory = `${evidenceDirectory(manifest.definitionKey)}/${quizKey}`;
  await mkdir(directory, { recursive: true, mode: 0o700 });
  await page.screenshot({ path: `${directory}/${name}`, fullPage: true });
}

function readGradingQueue(session, assessmentId) {
  return request(session.client, "Read grading queue", {
    method: "GET",
    path: `/v1/assessments/${assessmentId}/grading-queue`,
  });
}
