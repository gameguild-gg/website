import { expect, test } from "playwright/test";
import { getValidationCase } from "../../../apps/web/scripts/quiz-grading-scenario/capability-catalog.mjs";
import {
  getScenarioDefinition,
  selectQuizDefinitions,
} from "../../../apps/web/scripts/quiz-grading-scenario/definitions.mjs";
import { readManifest } from "../../../apps/web/scripts/quiz-grading-scenario/manifest.mjs";
import {
  captureValidationEvidence,
  createScenarioPersona,
} from "./browser-support.mjs";

test.use({ trace: "retain-on-failure", screenshot: "only-on-failure" });
test.skip(!process.env.E2E_RUN, "Set E2E_RUN=1 to execute the local scenario.");

const scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
const requestedQuizKeys = (process.env.QUIZ_GRADING_QUIZZES ?? "")
  .split(",")
  .map((value) => value.trim())
  .filter(Boolean);

if (!scenarioKey) {
  test("authoring round-trip requires a scenario key", () => {
    throw new Error("QUIZ_GRADING_SCENARIO is required.");
  });
} else {
  const definition = getScenarioDefinition(scenarioKey);
  const quizzes = selectQuizDefinitions(definition, requestedQuizKeys).filter(
    (quiz) => quiz.validationCaseId,
  );

  for (const quiz of quizzes) {
    const validationCase = getValidationCase(quiz.validationCaseId);
    test(`${validationCase.id}: authored projection reopens`, async ({
      browser,
    }) => {
      test.setTimeout(120_000);
      const manifest = await readManifest(scenarioKey);
      if (!manifest.resources.quizzes[quiz.key]) {
        throw new Error(
          `${validationCase.id}: quiz "${quiz.key}" is not provisioned.`,
        );
      }
      const instructor = await createScenarioPersona(
        browser,
        manifest,
        "instructor",
      );
      try {
        const url = manifest.urls.quizzes[quiz.key].contentEditor;
        await instructor.page.goto(url, { waitUntil: "domcontentloaded" });
        await expectAuthoredProjection(
          instructor.page,
          manifest,
          quiz,
          validationCase,
        );

        await instructor.page.reload({ waitUntil: "domcontentloaded" });
        await expectAuthoredProjection(
          instructor.page,
          manifest,
          quiz,
          validationCase,
        );
        await captureValidationEvidence(
          instructor.page,
          manifest,
          validationCase.id,
          "authoring-reopened.png",
        );
        instructor.failures.assertNone(`${validationCase.id} authoring`);
      } finally {
        await instructor.context.close();
      }
    });
  }
}

async function expectAuthoredProjection(page, manifest, quiz, validationCase) {
  await expect(page.getByRole("textbox", { name: "Lesson title" })).toHaveValue(
    `[${manifest.marker}] ${quiz.title}`,
    { timeout: 60_000 },
  );

  await page.getByRole("button", { name: "Edit question 1" }).click();
  const editor = page.getByRole("dialog", { name: "Quiz Builder" });
  await expect(editor).toBeVisible({ timeout: 60_000 });
  await expect(editor.getByLabel("Question")).toHaveValue(
    validationCase.fixture.entry.stem,
  );
  await editor.getByRole("button", { name: "Close quiz editor" }).click();
  await expect(editor).toHaveCount(0);
}
