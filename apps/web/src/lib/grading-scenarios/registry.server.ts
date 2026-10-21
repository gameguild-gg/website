import "server-only";

import type { GradingScenarioAdapter } from "./contracts";
import { quizGradingScenarioAdapter } from "./adapters/quiz/adapter.server";

// Composition root: domain adapters are registered here. The grading scenario
// contracts and UI remain independent of quiz, code, or future assessment types.
const adapters: readonly GradingScenarioAdapter[] = [
  quizGradingScenarioAdapter,
];

export function listGradingScenarioAdapters(): readonly GradingScenarioAdapter[] {
  return adapters;
}

export function getGradingScenarioAdapter(key: string): GradingScenarioAdapter {
  const adapter = adapters.find((candidate) => candidate.summary().key === key);
  if (!adapter) {
    throw new Error(
      `Unknown grading scenario adapter "${key}". Available adapters: ${adapters.map((candidate) => candidate.summary().key).join(", ")}.`,
    );
  }
  return adapter;
}
