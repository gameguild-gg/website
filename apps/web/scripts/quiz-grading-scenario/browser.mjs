import { spawn } from "node:child_process";
import { REPO_ROOT, manifestPath } from "./manifest.mjs";

export async function runBrowserScenario({
  scenarioKey,
  quizKeys = [],
  headed = false,
}) {
  const specs = browserSpecsForScenario(scenarioKey);
  const args = ["exec", "playwright", "test", ...specs, "--workers=1"];
  if (headed) args.push("--headed");
  const child = spawn("pnpm", args, {
    cwd: REPO_ROOT,
    stdio: "inherit",
    env: {
      ...process.env,
      E2E_RUN: "1",
      QUIZ_GRADING_SCENARIO: scenarioKey,
      QUIZ_GRADING_QUIZZES: quizKeys.join(","),
      QUIZ_GRADING_SCENARIO_MANIFEST: manifestPath(scenarioKey),
    },
  });
  const exitCode = await new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (signal) reject(new Error(`Playwright exited from signal ${signal}.`));
      else resolve(code ?? 1);
    });
  });
  if (exitCode !== 0)
    throw new Error(`Playwright failed with exit code ${exitCode}.`);
}

export function browserSpecsForScenario(scenarioKey) {
  if (scenarioKey === "question-type-matrix") {
    return [
      "e2e/learning/quiz-grading/authoring-roundtrip.spec.mjs",
      "e2e/learning/quiz-grading/question-types.spec.mjs",
    ];
  }
  if (scenarioKey === "acceptance-lab") {
    return [
      "e2e/learning/quiz-grading-scenario.spec.mjs",
      "e2e/learning/quiz-grading/authoring-roundtrip.spec.mjs",
      "e2e/learning/quiz-grading/question-types.spec.mjs",
    ];
  }
  return ["e2e/learning/quiz-grading-scenario.spec.mjs"];
}
