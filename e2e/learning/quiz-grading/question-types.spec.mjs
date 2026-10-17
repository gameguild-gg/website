import { expect, test } from "playwright/test";
import { getValidationCase } from "../../../apps/web/scripts/quiz-grading-scenario/capability-catalog.mjs";
import {
  checkpointIndex,
  expectedFinalScore,
  getScenarioDefinition,
  hasInstructorReview,
  selectQuizDefinitions,
} from "../../../apps/web/scripts/quiz-grading-scenario/definitions.mjs";
import { readManifest } from "../../../apps/web/scripts/quiz-grading-scenario/manifest.mjs";
import { answerQuestionTypeCase } from "../../../apps/web/scripts/quiz-grading-scenario/question-type-drivers.mjs";
import {
  advanceScenarioQuiz,
  captureValidationEvidence,
  createScenarioPersona,
  expectQuestionVisible,
  persistScenarioManifest,
  signInScenarioPersona,
  synchronizeScenarioSubmission,
} from "./browser-support.mjs";

test.use({ trace: "retain-on-failure", screenshot: "only-on-failure" });
test.skip(!process.env.E2E_RUN, "Set E2E_RUN=1 to execute the local scenario.");

const scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
const requestedQuizKeys = (process.env.QUIZ_GRADING_QUIZZES ?? "")
  .split(",")
  .map((value) => value.trim())
  .filter(Boolean);

if (!scenarioKey) {
  test("question-type matrix requires a scenario key", () => {
    throw new Error("QUIZ_GRADING_SCENARIO is required.");
  });
} else {
  const definition = getScenarioDefinition(scenarioKey);
  const quizzes = selectQuizDefinitions(definition, requestedQuizKeys).filter(
    (quiz) => quiz.validationCaseId,
  );

  for (const quiz of quizzes) {
    const validationCase = getValidationCase(quiz.validationCaseId);
    test(`${validationCase.id}: ${validationCase.capability}`, async ({
      browser,
    }) => {
      test.setTimeout(240_000);
      test.skip(
        validationCase.browser.status !== "required",
        `${validationCase.id} [${validationCase.browser.status}] ${validationCase.browser.reason} (${validationCase.baseline.tracking})`,
      );

      const manifest = await readManifest(scenarioKey);
      const resources = manifest.resources.quizzes[quiz.key];
      if (!resources) {
        throw new Error(
          `${validationCase.id}: quiz "${quiz.key}" is not provisioned in ${scenarioKey}.`,
        );
      }
      if (
        checkpointIndex(resources.checkpoint) < checkpointIndex("learner-ready")
      ) {
        throw new Error(
          `${validationCase.id}: learner-ready is required; current checkpoint is ${resources.checkpoint}.`,
        );
      }

      const learner = await createScenarioPersona(
        browser,
        manifest,
        "learnerA",
      );
      const instructor = await createScenarioPersona(
        browser,
        manifest,
        "instructor",
      );
      try {
        if (resources.checkpoint === "learner-ready") {
          await submitQuestionCase(
            learner.page,
            manifest,
            quiz,
            validationCase,
          );
          await synchronizeScenarioSubmission(manifest, quiz);
          advanceScenarioQuiz(
            manifest,
            quiz.key,
            hasInstructorReview(quiz) ? "review-ready" : "release-ready",
          );
          await persistScenarioManifest(manifest);
        }

        if (
          hasInstructorReview(quiz) &&
          resources.checkpoint === "review-ready"
        ) {
          await completeInstructorReview(
            instructor.page,
            manifest,
            quiz,
            validationCase,
          );
          await synchronizeScenarioSubmission(manifest, quiz);
          advanceScenarioQuiz(manifest, quiz.key, "release-ready");
          await persistScenarioManifest(manifest);
        }

        if (resources.checkpoint === "release-ready") {
          await releaseResult(instructor.page, manifest, quiz);
          await synchronizeScenarioSubmission(manifest, quiz);
          advanceScenarioQuiz(manifest, quiz.key, "released");
          await persistScenarioManifest(manifest);
        }

        await verifyReleasedResult(
          learner.page,
          manifest,
          quiz,
          validationCase,
        );
        learner.failures.assertNone(`${validationCase.id} learner`);
        instructor.failures.assertNone(`${validationCase.id} instructor`);
      } finally {
        await Promise.allSettled([
          learner.context.close(),
          instructor.context.close(),
        ]);
      }
    });
  }
}

async function submitQuestionCase(page, manifest, quiz, validationCase) {
  await page.goto(manifest.urls.quizzes[quiz.key].learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await page.getByRole("button", { name: "Start Activity" }).click();
  await expectQuestionVisible(page, validationCase);
  await answerQuestionTypeCase(page, validationCase, "correct");
  await page.getByRole("button", { name: "Submit answer" }).click();
  await page.getByRole("button", { name: "Submit Quiz" }).click();
  await expect(
    page
      .getByText(
        /Submitted|result has not been released|instructor review|Answer recorded/i,
      )
      .first(),
  ).toBeVisible({ timeout: 60_000 });
  await captureValidationEvidence(
    page,
    manifest,
    validationCase.id,
    "learner-submitted.png",
  );
}

async function completeInstructorReview(page, manifest, quiz, validationCase) {
  await signInScenarioPersona(page, manifest, "instructor");
  await page.goto(manifest.urls.quizzes[quiz.key].speedGraders.learnerA, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("grading-panel")).toBeVisible({
    timeout: 60_000,
  });
  await page
    .getByTestId("item-score-q1")
    .fill(String(quiz.expected.instructorScore / 100));
  await page.getByTestId("resolve-instructor-review").click();
  await expect(page.getByText("Ready to release")).toBeVisible({
    timeout: 30_000,
  });
  await captureValidationEvidence(
    page,
    manifest,
    validationCase.id,
    "instructor-reviewed.png",
  );
}

async function releaseResult(page, manifest, quiz) {
  await signInScenarioPersona(page, manifest, "instructor");
  await page.goto(manifest.urls.quizzes[quiz.key].speedGraders.learnerA, {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByTestId("release-result")).toBeVisible({
    timeout: 60_000,
  });
  await page.getByTestId("release-result").click();
  await expect(page.getByText("Released to learner")).toBeVisible({
    timeout: 30_000,
  });
}

async function verifyReleasedResult(page, manifest, quiz, validationCase) {
  await page.goto(manifest.urls.learnerGrades, {
    waitUntil: "domcontentloaded",
  });
  const heading = page.getByRole("heading", {
    name: `[${manifest.marker}] ${quiz.title}`,
    exact: true,
  });
  await expect(heading).toBeVisible({ timeout: 60_000 });
  await expect(
    heading
      .locator("xpath=ancestor::article")
      .getByText(
        `${expectedFinalScore(quiz) / 100} / ${quiz.expected.maxScore / 100}`,
        { exact: true },
      ),
  ).toBeVisible();

  await page.goto(manifest.urls.quizzes[quiz.key].learnerActivity, {
    waitUntil: "domcontentloaded",
  });
  await expectQuestionVisible(page, validationCase);
  await expect(
    page.getByRole("button", { name: "Start Activity" }),
  ).toHaveCount(0);
  await captureValidationEvidence(
    page,
    manifest,
    validationCase.id,
    "learner-released.png",
  );
}
