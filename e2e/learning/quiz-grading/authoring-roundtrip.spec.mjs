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
        await expect(
          instructor.page
            .getByRole("heading", {
              name: new RegExp(quiz.title, "i"),
            })
            .first(),
        ).toBeVisible({ timeout: 60_000 });
        await expect(
          instructor.page
            .getByDisplayValue(validationCase.fixture.entry.stem, {
              exact: true,
            })
            .first(),
        ).toBeVisible({ timeout: 60_000 });

        await instructor.page.reload({ waitUntil: "domcontentloaded" });
        await expect(
          instructor.page
            .getByDisplayValue(validationCase.fixture.entry.stem, {
              exact: true,
            })
            .first(),
        ).toBeVisible({ timeout: 60_000 });
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
