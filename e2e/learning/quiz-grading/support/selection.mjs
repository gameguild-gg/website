import { selectValidationCases } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs";
import {
  getScenarioDefinition,
  selectQuizDefinitions,
} from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/definitions.mjs";

export function resolveScenarioTargetKeys(
  scenarioKey,
  {
    quizKeys = csv(process.env.QUIZ_GRADING_QUIZZES),
    caseIds = csv(process.env.QUIZ_GRADING_CASES),
  } = {},
) {
  const definition = getScenarioDefinition(scenarioKey);
  const fromCases = caseIds.length
    ? selectValidationCases({ caseIds })
        .map((entry) => entry.quizKey ?? entry.key)
        .filter(Boolean)
    : [];
  const requested = [...new Set([...quizKeys, ...fromCases])];
  return selectQuizDefinitions(definition, requested).map(
    (target) => target.key,
  );
}

export function csv(value) {
  return (value ?? "")
    .split(",")
    .map((entry) => entry.trim())
    .filter(Boolean);
}
