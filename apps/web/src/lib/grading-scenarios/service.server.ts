import "server-only";

import type {
  GradingScenarioLabModel,
  GradingScenarioOperationResult,
  GradingScenarioResetResult,
  PrepareGradingScenarioInput,
} from "./contracts";
import {
  getGradingScenarioAdapter,
  listGradingScenarioAdapters,
} from "./registry.server";

export async function getGradingScenarioLabModel(): Promise<GradingScenarioLabModel> {
  const adapters = listGradingScenarioAdapters();
  const summaries = adapters.map((adapter) => adapter.summary());
  const states = await Promise.all(
    adapters.flatMap((adapter) =>
      adapter
        .summary()
        .scenarios.map((scenario) => adapter.inspect(scenario.key)),
    ),
  );
  return { adapters: summaries, states };
}

export function prepareGradingScenario(
  adapterKey: string,
  input: PrepareGradingScenarioInput,
): Promise<GradingScenarioOperationResult> {
  return getGradingScenarioAdapter(adapterKey).prepare(input);
}

export function verifyGradingScenario(
  adapterKey: string,
  scenarioKey: string,
): Promise<GradingScenarioOperationResult> {
  return getGradingScenarioAdapter(adapterKey).verify(scenarioKey);
}

export function inspectGradingScenario(
  adapterKey: string,
  scenarioKey: string,
) {
  return getGradingScenarioAdapter(adapterKey).inspect(scenarioKey);
}

export function resetGradingScenario(
  adapterKey: string,
  scenarioKey: string,
): Promise<GradingScenarioResetResult> {
  return getGradingScenarioAdapter(adapterKey).reset(scenarioKey);
}
