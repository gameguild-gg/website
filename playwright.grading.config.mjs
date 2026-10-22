import { defineConfig } from "playwright/test";
import { resolveScenarioTargetKeys } from "./e2e/learning/quiz-grading/support/selection.mjs";
import { browserSpecsForScenario } from "./e2e/learning/quiz-grading/support/spec-selection.mjs";

const scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
if (!scenarioKey) {
  throw new Error("QUIZ_GRADING_SCENARIO is required by grading Playwright.");
}
process.env.QUIZ_GRADING_QUIZZES =
  resolveScenarioTargetKeys(scenarioKey).join(",");

export default defineConfig({
  testDir: "./e2e",
  testMatch: browserSpecsForScenario(scenarioKey).map((path) =>
    path.replace(/^e2e\//, ""),
  ),
  globalSetup: "./e2e/learning/quiz-grading/support/lab-lifecycle.mjs",
  timeout: 360_000,
  fullyParallel: false,
  workers: 1,
  reporter: [
    ["line"],
    ["./e2e/learning/quiz-grading/support/validation-reporter.mjs"],
  ],
  use: {
    baseURL: (
      process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://localhost:3000"
    ).replace(/\/$/, ""),
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
});
