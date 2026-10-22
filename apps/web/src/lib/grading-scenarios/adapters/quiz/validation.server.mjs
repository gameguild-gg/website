import {
  getValidationCase,
  selectValidationCases,
} from "./capability-catalog.mjs";
import { getScenarioDefinition } from "./definitions.mjs";

export function validationCasesForManifest(manifest) {
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

export function isBrowserPassCandidate(candidate, selectedKeys) {
  const quizKey = candidate.quizKey ?? candidate.key;
  if (quizKey && selectedKeys.length > 0 && !selectedKeys.includes(quizKey)) {
    return false;
  }
  if (candidate.automation !== "browser") return false;
  return !candidate.browser || candidate.browser.status === "required";
}

export async function writeCandidateValidationReport(
  manifest,
  scenarioReport,
  { completedAutomations = ["api"], results = [] } = {},
) {
  const { createValidationReport, writeValidationReport } =
    await import("./report.server.mjs");
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
