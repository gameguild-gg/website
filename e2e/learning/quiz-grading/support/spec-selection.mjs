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
